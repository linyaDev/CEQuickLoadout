using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEQuickLoadout;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ChoicesAtFor))]
public static class Patch_RightClickPawn
{
    public static void Postfix(List<FloatMenuOption> __result, Vector3 clickPos, Pawn pawn)
    {
        IntVec3 c = IntVec3.FromVector3(clickPos);
        if (!c.InBounds(pawn.Map)) return;

        foreach (var thing in pawn.Map.thingGrid.ThingsAt(c))
        {
            if (thing is Pawn target
                && target.Faction == Faction.OfPlayer && !target.IsSlave
                && target.outfits?.CurrentApparelPolicy != null)
            {
                AddOutfitOptions(__result, target);
                break;
            }
        }
    }

    private static void AddOutfitOptions(List<FloatMenuOption> options, Pawn target)
    {
        var outfits = Current.Game?.outfitDatabase?.AllOutfits;
        if (outfits == null) return;

        string currentLabel = target.outfits.CurrentApparelPolicy.label;

        options.Add(new FloatMenuOption(
            "CEQL_ChangeOutfitFor".Translate(target.LabelShortCap, currentLabel),
            () =>
            {
                var subOptions = new List<FloatMenuOption>();
                foreach (var outfit in outfits)
                {
                    var o = outfit;
                    subOptions.Add(new FloatMenuOption(
                        o.label,
                        () =>
                        {
                            target.outfits.CurrentApparelPolicy = o;
                            Messages.Message("CEQL_OutfitAssigned".Translate(o.label, target.LabelShortCap),
                                MessageTypeDefOf.PositiveEvent, false);
                        }));
                }
                if (subOptions.Count > 0)
                    Find.WindowStack.Add(new FloatMenu(subOptions));
            }));
    }
}
