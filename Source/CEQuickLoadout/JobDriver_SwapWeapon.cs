using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CEQuickLoadout;

// TargetA = new (better) weapon on the map, TargetB = old (worse) weapon carried by pawn.
// Walks to TargetA, picks it up into inventory, drops TargetB on the ground (forbidden).
// Notifies CE (CompInventory) and Simple Sidearms (if loaded) about the change.
public class JobDriver_SwapWeapon : JobDriver
{
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

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(TargetIndex.A);

        var swap = ToilMaker.MakeToil("swap");
        swap.initAction = () =>
        {
            var newWeapon = NewWeapon;
            var oldWeapon = OldWeapon;
            if (newWeapon == null || newWeapon.Destroyed) return;

            // Forbid all other items of this def on the map
            pawn.Map.GetComponent<WeaponUpgradeChecker>()?.ForbidAllOfDef(newWeapon.def, newWeapon.thingIDNumber);

            // Drop old weapon
            if (oldWeapon != null && !oldWeapon.Destroyed)
            {
                Thing droppedThing = null;
                if (pawn.equipment?.Primary == oldWeapon)
                {
                    pawn.equipment.TryDropEquipment(oldWeapon as ThingWithComps, out ThingWithComps droppedEq, pawn.Position);
                    droppedThing = droppedEq;
                }
                else if (pawn.inventory.innerContainer.Contains(oldWeapon))
                {
                    pawn.inventory.innerContainer.TryDrop(oldWeapon, pawn.Position, pawn.Map, ThingPlaceMode.Near, out droppedThing);
                }

                if (droppedThing != null)
                {
                    droppedThing.SetForbidden(true);
                    pawn.Map.GetComponent<WeaponUpgradeChecker>()?.TrackTempForbidden(droppedThing);
                }
            }

            // Pick up new weapon into inventory
            if (newWeapon.Spawned)
                newWeapon.DeSpawn();
            pawn.inventory.innerContainer.TryAdd(newWeapon);

            // Notify CE and Simple Sidearms
            pawn.TryGetComp<CombatExtended.CompInventory>()?.UpdateInventory();
            SidearmsHelper.NotifySwap(pawn, oldWeapon, newWeapon);
        };
        swap.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return swap;
    }
}
