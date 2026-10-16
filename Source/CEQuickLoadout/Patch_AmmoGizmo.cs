using System.Collections.Generic;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace CEQuickLoadout;

// CE adds an ammo status gizmo (GizmoAmmoStatus) per armed pawn, so selecting a crowd
// floods the gizmo bar with ammo bars. Hide it when more than 3 colonists are selected.
[HarmonyPatch(typeof(CompAmmoUser), nameof(CompAmmoUser.CompGetGizmosExtra))]
public static class Patch_HideAmmoGizmoOnMultiSelect
{
    private const int MaxSelectedForAmmoGizmo = 3;

    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos)
    {
        if (!CEQuickLoadoutMod.Settings.hideAmmoGizmoOnMultiSelect || !TooManyColonistsSelected())
        {
            foreach (var g in gizmos)
                yield return g;
            yield break;
        }

        foreach (var g in gizmos)
        {
            if (g is GizmoAmmoStatus) continue;
            yield return g;
        }
    }

    private static bool TooManyColonistsSelected()
    {
        var selected = Find.Selector?.SelectedObjects;
        if (selected == null) return false;

        int count = 0;
        foreach (var obj in selected)
        {
            if (obj is Pawn p && p.IsColonistPlayerControlled && ++count > MaxSelectedForAmmoGizmo)
                return true;
        }
        return false;
    }
}
