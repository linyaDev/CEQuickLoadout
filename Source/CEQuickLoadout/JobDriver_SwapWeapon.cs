using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace CEQuickLoadout;

// TargetA = new (better) weapon on the map, TargetB = old (worse) weapon carried by pawn.
// Walks to TargetA, picks it up into inventory, drops TargetB on the ground.
// CE loadout system handles equipping from inventory on its own.
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

        // 1. Walk to the new weapon
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(TargetIndex.A);

        // 2. Pick up new, drop old
        var swap = ToilMaker.MakeToil("swap");
        swap.initAction = () =>
        {
            var newWeapon = NewWeapon;
            var oldWeapon = OldWeapon;
            if (newWeapon == null || newWeapon.Destroyed) return;

            // Pick up new weapon from the ground into inventory
            if (newWeapon.Spawned)
                newWeapon.DeSpawn();
            pawn.inventory.innerContainer.TryAdd(newWeapon);

            // Drop old weapon from equipment or inventory
            if (oldWeapon != null && !oldWeapon.Destroyed)
            {
                if (pawn.equipment?.Primary == oldWeapon)
                    pawn.equipment.TryDropEquipment(oldWeapon as ThingWithComps, out _, pawn.Position);
                else
                    pawn.inventory.innerContainer.TryDrop(oldWeapon, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
            }
        };
        swap.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return swap;
    }
}
