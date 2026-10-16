using System.Collections.Generic;
using CombatExtended;
using CombatExtended.ExtendedLoadout;
using RimWorld;
using Verse;
using Verse.AI;

namespace CEQuickLoadout;

// Every 8 in-game hours, checks if colonists can upgrade their loadout weapons
// to a better version of the same def found on the map (by config stat or quality).
// Queues the job instead of interrupting — pawn picks it up when idle.
// Pawns that were busy/asleep during the check get retried every hour.
public class WeaponUpgradeChecker : MapComponent
{
    private const int CheckIntervalTicks = 20000; // 8 in-game hours
    private const int RetryIntervalTicks = 2500;  // 1 in-game hour
    private const int ForbidDurationTicks = 7;  // ~10 RimWorld seconds

    private readonly HashSet<int> deferred = new HashSet<int>();
    private readonly HashSet<int> claimedItems = new HashSet<int>();

    // Temporarily forbidden items: thingIDNumber -> tick when to unforbid
    private readonly Dictionary<int, int> tempForbidden = new Dictionary<int, int>();

    private static JobDef swapJobDef;
    private static JobDef SwapJobDef => swapJobDef ??= DefDatabase<JobDef>.GetNamed("CEQL_SwapWeapon");

    public WeaponUpgradeChecker(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (!CEQuickLoadoutMod.Settings.enableWeaponUpgradeSearch) return;

        int tick = Find.TickManager.TicksGame;

        if (tempForbidden.Count > 0 && tick % 5 == 0)
            UnforbidExpired(tick);

        if (tick % CheckIntervalTicks == 0)
        {
            CheckUpgrades();
            return;
        }

        if (deferred.Count > 0 && tick % RetryIntervalTicks == 0)
            RetryDeferred();
    }

    private void UnforbidExpired(int tick)
    {
        var toRemove = new List<int>();
        foreach (var kv in tempForbidden)
        {
            if (tick < kv.Value) continue;
            toRemove.Add(kv.Key);

            foreach (var thing in map.listerThings.AllThings)
            {
                if (thing.thingIDNumber == kv.Key)
                {
                    thing.SetForbidden(false);
                    break;
                }
            }
        }
        foreach (var id in toRemove)
            tempForbidden.Remove(id);
    }

    public void TrackTempForbidden(Thing thing)
    {
        tempForbidden[thing.thingIDNumber] = Find.TickManager.TicksGame + ForbidDurationTicks;
    }

    // Forbid all map items of the given def (except the one being picked up).
    // Only forbids items that are NOT already forbidden (to preserve player forbids).
    public void ForbidAllOfDef(ThingDef def, int exceptId)
    {
        int expireTick = Find.TickManager.TicksGame + ForbidDurationTicks;
        foreach (var thing in map.listerThings.ThingsOfDef(def))
        {
            if (thing.thingIDNumber == exceptId) continue;
            if (!thing.Spawned) continue;
            if (thing.IsForbidden(Faction.OfPlayer)) continue;
            thing.SetForbidden(true);
            tempForbidden[thing.thingIDNumber] = expireTick;
        }
    }

    private void CheckUpgrades()
    {
        deferred.Clear();
        claimedItems.Clear();
        foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
        {
            if (pawn.IsSlave || pawn.Downed) continue;
            if (pawn.Drafted || pawn.InBed() || pawn.InMentalState || pawn.IsFighting())
            {
                deferred.Add(pawn.thingIDNumber);
                continue;
            }
            TryUpgradeWeapon(pawn);
        }
    }

    private void RetryDeferred()
    {
        var ids = new List<int>(deferred);
        foreach (var id in ids)
        {
            var pawn = FindPawnById(id);
            if (pawn == null) { deferred.Remove(id); continue; }
            if (pawn.Drafted || pawn.InBed() || pawn.InMentalState || pawn.Downed || pawn.IsFighting()) continue;
            deferred.Remove(id);
            TryUpgradeWeapon(pawn);
        }
    }

    private Pawn FindPawnById(int id)
    {
        foreach (var p in map.mapPawns.FreeColonistsSpawned)
            if (p.thingIDNumber == id) return p;
        return null;
    }

    private void TryUpgradeWeapon(Pawn pawn)
    {
        var multiBase = LoadoutMulti_Manager.GetLoadout(pawn, allowNull: true);
        var multi = multiBase as Loadout_Multi;
        var allSlots = multi?.Slots ?? pawn.GetLoadout()?.Slots;
        if (allSlots == null) return;

        if (HasSwapJobQueued(pawn)) return;

        foreach (var slot in allSlots)
        {
            var def = slot.thingDef;
            if (def == null || !def.IsWeapon) continue;

            // Compare by the stat from the XML config if this weapon has a rule,
            // otherwise by quality.
            var compareStat = WeaponUpgradeRules.StatFor(def);

            var slotLoadout = multi?.FindLoadoutWithSlot(slot) ?? pawn.GetLoadout();
            if (slotLoadout == null || slotLoadout.defaultLoadout) continue;
            var slotExt = Loadout_Extended.Get(slotLoadout);

            bool TryGetScore(Thing t, out float score)
            {
                if (compareStat != null)
                {
                    score = ScoreByStat(t, compareStat);
                    return true;
                }
                if (t.TryGetQuality(out var q))
                {
                    score = (float)q;
                    return true;
                }
                score = 0f;
                return false;
            }

            float currentBest = 0f;
            Thing currentWeapon = null;

            var primary = pawn.equipment?.Primary;
            if (primary != null && primary.def == def && TryGetScore(primary, out var ps))
            {
                currentBest = ps;
                currentWeapon = primary;
            }

            var inv = pawn.inventory?.innerContainer;
            if (inv != null)
            {
                foreach (var item in inv)
                {
                    if (item.def == def && TryGetScore(item, out var iScore))
                    {
                        if (currentWeapon == null || iScore > currentBest)
                        {
                            currentBest = iScore;
                            currentWeapon = item;
                        }
                    }
                }
            }

            if (currentWeapon == null) continue;

            Thing bestWeapon = null;
            float bestScore = currentBest;

            foreach (var thing in map.listerThings.ThingsOfDef(def))
            {
                if (claimedItems.Contains(thing.thingIDNumber)) continue;
                if (thing.IsForbidden(pawn)) continue;
                if (!TryGetScore(thing, out var score)) continue;
                if (score <= bestScore) continue;
                if (thing.IsBurning()) continue;
                if (CompBiocodable.IsBiocoded(thing) && !CompBiocodable.IsBiocodedFor(thing, pawn)) continue;
                if (!slotExt.Allows(thing)) continue;
                if (!pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Deadly)) continue;

                bestScore = score;
                bestWeapon = thing;
            }

            if (bestWeapon == null) continue;

            claimedItems.Add(bestWeapon.thingIDNumber);

            var job = JobMaker.MakeJob(SwapJobDef, bestWeapon, currentWeapon);
            pawn.jobs.jobQueue.EnqueueFirst(job, JobTag.Misc);

            Messages.Message(
                "CEQL_UpgradeFound".Translate(pawn.LabelShortCap, bestWeapon.LabelCap),
                pawn, MessageTypeDefOf.PositiveEvent, false);

            break;
        }
    }

    // SurvivalToolsLite tools: the per-instance value (what the info card shows, e.g.
    // "Mining speed 162%") comes from SurvivalTool.WorkStatFactors = def base * quality * material.
    // Plain equipped-offset stats: StatWorker.StatOffsetFromGear.
    // Stats the item itself has (e.g. MarketValue): GetStatValue.
    private static float ScoreByStat(Thing t, StatDef stat)
    {
        if (SurvivalToolsHelper.TryGetWorkStatFactor(t, stat, out var toolFactor))
            return toolFactor;

        var offsets = t.def.equippedStatOffsets;
        if (offsets != null)
        {
            for (int i = 0; i < offsets.Count; i++)
            {
                if (offsets[i].stat == stat)
                    return StatWorker.StatOffsetFromGear(t, stat);
            }
        }
        return t.GetStatValue(stat);
    }

    private bool HasSwapJobQueued(Pawn pawn)
    {
        if (pawn.CurJob?.def == SwapJobDef) return true;

        foreach (var qj in pawn.jobs.jobQueue)
            if (qj.job.def == SwapJobDef) return true;

        return false;
    }
}
