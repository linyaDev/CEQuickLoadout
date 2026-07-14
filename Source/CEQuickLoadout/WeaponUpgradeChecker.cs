using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using Verse;
using Verse.AI;

namespace CEQuickLoadout;

// Every 8 in-game hours, checks if colonists can upgrade their loadout weapons
// to a higher-quality version of the same def found on the map.
// Queues the job instead of interrupting — pawn picks it up when idle.
// Pawns that were busy/asleep during the check get retried every hour.
public class WeaponUpgradeChecker : MapComponent
{
    private const int CheckIntervalTicks = 20000; // 8 in-game hours
    private const int RetryIntervalTicks = 2500;  // 1 in-game hour

    private readonly HashSet<int> deferred = new HashSet<int>();
    private readonly HashSet<int> claimedItems = new HashSet<int>();

    private static JobDef swapJobDef;
    private static JobDef SwapJobDef => swapJobDef ??= DefDatabase<JobDef>.GetNamed("CEQL_SwapWeapon");

    public WeaponUpgradeChecker(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (!CEQuickLoadoutMod.Settings.enableWeaponUpgradeSearch) return;

        int tick = Find.TickManager.TicksGame;

        if (tick % CheckIntervalTicks == 0)
        {
            CheckUpgrades();
            return;
        }

        if (deferred.Count > 0 && tick % RetryIntervalTicks == 0)
            RetryDeferred();
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
        var loadout = pawn.GetLoadout();
        if (loadout == null || loadout.defaultLoadout) return;

        // Skip if pawn already has a swap job queued
        if (HasSwapJobQueued(pawn)) return;

        foreach (var slot in loadout.Slots)
        {
            var def = slot.thingDef;
            if (def == null || !def.IsWeapon) continue;

            // Find the best quality this pawn currently carries of this def
            QualityCategory currentBest = QualityCategory.Awful;
            Thing currentWeapon = null;

            var primary = pawn.equipment?.Primary;
            if (primary != null && primary.def == def && primary.TryGetQuality(out var pq))
            {
                currentBest = pq;
                currentWeapon = primary;
            }

            var inv = pawn.inventory?.innerContainer;
            if (inv != null)
            {
                foreach (var item in inv)
                {
                    if (item.def == def && item.TryGetQuality(out var iq))
                    {
                        if (currentWeapon == null || iq > currentBest)
                        {
                            currentBest = iq;
                            currentWeapon = item;
                        }
                    }
                }
            }

            if (currentWeapon == null) continue;

            // Search the map for a better one
            Thing bestWeapon = null;
            QualityCategory bestQuality = currentBest;

            foreach (var thing in map.listerThings.ThingsOfDef(def))
            {
                if (claimedItems.Contains(thing.thingIDNumber)) continue;
                if (thing.IsForbidden(pawn)) continue;
                if (!thing.TryGetQuality(out var q)) continue;
                if (q <= bestQuality) continue;
                if (thing.IsBurning()) continue;
                if (!pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Deadly)) continue;

                bestQuality = q;
                bestWeapon = thing;
            }

            if (bestWeapon == null) continue;

            claimedItems.Add(bestWeapon.thingIDNumber);

            currentWeapon.TryGetQuality(out var curQ);
            Log.Message($"[CEQL Check] {pawn.LabelShortCap}: {def.LabelCap} upgrade {curQ}(id={currentWeapon.thingIDNumber}) -> {bestQuality}(id={bestWeapon.thingIDNumber})");

            var job = JobMaker.MakeJob(SwapJobDef, bestWeapon, currentWeapon);
            pawn.jobs.jobQueue.EnqueueFirst(job, JobTag.Misc);

            Messages.Message(
                "CEQL_UpgradeFound".Translate(pawn.LabelShortCap, bestWeapon.LabelCap),
                pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    private bool HasSwapJobQueued(Pawn pawn)
    {
        // Check current job
        if (pawn.CurJob?.def == SwapJobDef) return true;

        // Check job queue
        foreach (var qj in pawn.jobs.jobQueue)
            if (qj.job.def == SwapJobDef) return true;

        return false;
    }
}
