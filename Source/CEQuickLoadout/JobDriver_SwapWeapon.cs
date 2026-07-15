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
            NotifySidearms(pawn, oldWeapon, newWeapon);
        };
        swap.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return swap;
    }

    private static bool sidearmsChecked;
    private static System.Type sidearmMemoryType;
    private static System.Reflection.MethodInfo getMemoryMethod;
    private static System.Reflection.MethodInfo informDropMethod;
    private static System.Reflection.MethodInfo informAddMethod;

    private static void NotifySidearms(Pawn pawn, Thing oldWeapon, Thing newWeapon)
    {
        if (!sidearmsChecked)
        {
            sidearmsChecked = true;
            sidearmMemoryType = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.CompSidearmMemory");
            if (sidearmMemoryType != null)
            {
                getMemoryMethod = sidearmMemoryType.GetMethod("GetMemoryCompForPawn", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                informDropMethod = sidearmMemoryType.GetMethod("InformOfDroppedSidearm", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                informAddMethod = sidearmMemoryType.GetMethod("InformOfAddedSidearm", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }
        }

        if (sidearmMemoryType == null || getMemoryMethod == null) return;

        try
        {
            var memory = getMemoryMethod.Invoke(null, new object[] { pawn, true });
            if (memory == null) return;

            if (oldWeapon != null && informDropMethod != null)
                informDropMethod.Invoke(memory, new object[] { oldWeapon, true });

            if (newWeapon != null && informAddMethod != null)
                informAddMethod.Invoke(memory, new object[] { newWeapon });
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[CEQL] SimpleSidearms integration error: {ex.Message}");
        }
    }
}
