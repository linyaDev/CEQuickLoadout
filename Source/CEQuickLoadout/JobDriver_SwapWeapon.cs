using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CEQuickLoadout;

// TargetA = new (better) weapon on the map, TargetB = old (worse) weapon carried by pawn.
// Walks to TargetA, picks it up into inventory, drops TargetB on the ground (forbidden).
// CE loadout system handles equipping from inventory on its own.
public class JobDriver_SwapWeapon : JobDriver
{
    private const string Tag = "[CEQL Swap]";

    private Thing NewWeapon => TargetThingA;
    private Thing OldWeapon => TargetThingB;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(NewWeapon, job, 1, -1, null, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnBurningImmobile(TargetIndex.A);

        // 1. Walk to the new weapon
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(TargetIndex.A);

        // 2. Pick up new, drop old
        var swap = ToilMaker.MakeToil("swap");
        swap.initAction = () =>
        {
            var newWeapon = NewWeapon;
            var oldWeapon = OldWeapon;

            Log.Message($"{Tag} {pawn.LabelShortCap} starting swap. New={newWeapon?.LabelCap} (id={newWeapon?.thingIDNumber}, spawned={newWeapon?.Spawned}), Old={oldWeapon?.LabelCap} (id={oldWeapon?.thingIDNumber})");
            Log.Message($"{Tag} {pawn.LabelShortCap} before swap — Primary={pawn.equipment?.Primary?.LabelCap} (id={pawn.equipment?.Primary?.thingIDNumber}), Inventory=[{InventoryList(pawn)}]");

            if (newWeapon == null || newWeapon.Destroyed)
            {
                Log.Warning($"{Tag} {pawn.LabelShortCap} new weapon null or destroyed, aborting");
                return;
            }

            // Forbid all other items of this def on the map right now
            var checker = pawn.Map.GetComponent<WeaponUpgradeChecker>();
            checker?.ForbidAllOfDef(newWeapon.def, newWeapon.thingIDNumber);

            // Drop old weapon FIRST, then pick up new
            if (oldWeapon != null && !oldWeapon.Destroyed)
            {
                Thing droppedThing = null;
                if (pawn.equipment?.Primary == oldWeapon)
                {
                    bool dropped = pawn.equipment.TryDropEquipment(oldWeapon as ThingWithComps, out ThingWithComps droppedEq, pawn.Position);
                    droppedThing = droppedEq;
                    Log.Message($"{Tag} {pawn.LabelShortCap} dropped primary old weapon: {dropped}, droppedThing={droppedThing?.LabelCap}");
                }
                else if (pawn.inventory.innerContainer.Contains(oldWeapon))
                {
                    bool dropped = pawn.inventory.innerContainer.TryDrop(oldWeapon, pawn.Position, pawn.Map, ThingPlaceMode.Near, out droppedThing);
                    Log.Message($"{Tag} {pawn.LabelShortCap} dropped inventory old weapon: {dropped}, droppedThing={droppedThing?.LabelCap}");
                }
                else
                {
                    Log.Warning($"{Tag} {pawn.LabelShortCap} old weapon not found in equipment or inventory! id={oldWeapon.thingIDNumber}");
                }

                // Forbid and register with temp tracker so it gets unforbidden later
                if (droppedThing != null)
                {
                    droppedThing.SetForbidden(true);
                    pawn.Map.GetComponent<WeaponUpgradeChecker>()?.TrackTempForbidden(droppedThing);
                }
            }

            // Pick up new weapon from the ground into inventory
            if (newWeapon.Spawned)
                newWeapon.DeSpawn();
            bool added = pawn.inventory.innerContainer.TryAdd(newWeapon);
            Log.Message($"{Tag} {pawn.LabelShortCap} TryAdd new weapon: {added}");

            // Notify CE that inventory changed so it updates its cache
            pawn.TryGetComp<CombatExtended.CompInventory>()?.UpdateInventory();

            Log.Message($"{Tag} {pawn.LabelShortCap} after swap — Primary={pawn.equipment?.Primary?.LabelCap} (id={pawn.equipment?.Primary?.thingIDNumber}), Inventory=[{InventoryList(pawn)}]");
        };
        swap.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return swap;
    }

    private static string InventoryList(Pawn pawn)
    {
        var inv = pawn.inventory?.innerContainer;
        if (inv == null || inv.Count == 0) return "empty";
        var sb = new System.Text.StringBuilder();
        foreach (var t in inv)
        {
            if (sb.Length > 0) sb.Append(", ");
            t.TryGetQuality(out var q);
            sb.Append($"{t.LabelCap}(id={t.thingIDNumber}, q={q})");
        }
        return sb.ToString();
    }
}
