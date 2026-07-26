using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEQuickLoadout;

public static class Patch_RPGInventory
{
    public static void TryPatch(Harmony harmony)
    {
        var gearTabType = AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab");
        if (gearTabType == null) return;

        var popupMenu = AccessTools.Method(gearTabType, "PopupMenu", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
        if (popupMenu == null) return;

        harmony.Patch(popupMenu,
            postfix: new HarmonyMethod(typeof(Patch_RPGInventory), nameof(PopupMenu_Postfix)));
    }

    static void PopupMenu_Postfix(List<FloatMenuOption> __result, Pawn pawn, Thing thing)
    {
        if (thing == null || !thing.def.IsApparel) return;

        var thingDef = thing.def;

        // Outfit info (non-clickable, with tooltip)
        string outfitInfo = GetOutfitInfo(thingDef);
        __result.Add(new FloatMenuOption(
            "CEQL_OutfitInfo".Translate(),
            () => {},
            mouseoverGuiAction: outfitInfo != null
                ? rect => TooltipHandler.TipRegion(rect, outfitInfo)
                : null));

        string itemLabel = thingDef.LabelCap;

        // Add to any outfit — submenu
        __result.Add(new FloatMenuOption(
            "CEQL_AddToOutfit".Translate(),
            () =>
            {
                var outfits = Current.Game?.outfitDatabase?.AllOutfits;
                if (outfits == null) return;
                var subOptions = new List<FloatMenuOption>();
                foreach (var outfit in outfits)
                {
                    if (outfit.filter.Allows(thingDef)) continue;
                    var o = outfit;
                    subOptions.Add(new FloatMenuOption(
                        o.label,
                        () =>
                        {
                            o.filter.SetAllow(thingDef, true);
                            Messages.Message("CEQL_ItemAddedToOutfit".Translate(itemLabel, o.label),
                                MessageTypeDefOf.PositiveEvent, false);
                        }));
                }
                if (subOptions.Count > 0)
                    Find.WindowStack.Add(new FloatMenu(subOptions));
            }));

        // Remove from pawn's current outfit
        var currentOutfit = pawn.outfits?.CurrentApparelPolicy;
        if (currentOutfit != null && currentOutfit.filter.Allows(thingDef))
        {
            var oRem = currentOutfit;
            __result.Add(new FloatMenuOption(
                "CEQL_RemoveFromOutfit".Translate() + ": " + oRem.label,
                () =>
                {
                    oRem.filter.SetAllow(thingDef, false);
                    Messages.Message("CEQL_ItemRemovedFromOutfit".Translate(itemLabel, oRem.label),
                        MessageTypeDefOf.NeutralEvent, false);
                }));
        }
    }

    private static string GetOutfitInfo(ThingDef def)
    {
        var outfits = Current.Game?.outfitDatabase?.AllOutfits;
        if (outfits == null) return null;

        var sb = new System.Text.StringBuilder();
        foreach (var outfit in outfits)
        {
            if (outfit.filter.Allows(def))
                sb.AppendLine("✓ " + outfit.label);
            else
                sb.AppendLine("   " + outfit.label);
        }
        return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
    }
}
