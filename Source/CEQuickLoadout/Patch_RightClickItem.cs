using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEQuickLoadout;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.TryMakeFloatMenu_NonPawn))]
public static class Patch_RightClickItem
{
    public static bool Prefix(Thing selectedThing)
    {
        if (selectedThing?.Map != Find.CurrentMap) return true;
        if (selectedThing.def.category != ThingCategory.Item) return true;

        var loadouts = LoadoutManager.Loadouts;
        if (loadouts == null) return true;

        var options = new List<FloatMenuOption>();
        var thingDef = selectedThing.def;
        string itemLabel = thingDef.LabelCap;

        // Vanilla options from things at cursor
        IntVec3 c = IntVec3.FromVector3(UI.MouseMapPosition());
        if (c.InBounds(Find.CurrentMap))
        {
            foreach (var item in selectedThing.Map.thingGrid.ThingsAt(c))
            {
                if (item != selectedThing)
                    options.AddRange(item.GetFloatMenuOptions_NonPawn(selectedThing));
            }
        }

        // 1. Add to colonist's loadout — submenu
        options.Add(new FloatMenuOption(
            "CEQL_AddToColonistMenu".Translate(),
            () => ShowColonistSubmenu(thingDef, itemLabel)));

        // 2. Create new loadout
        options.Add(new FloatMenuOption(
            "CEQL_CreateLoadout".Translate(itemLabel),
            () => CreateLoadout(thingDef, itemLabel)));

        Find.WindowStack.Add(new FloatMenu(options));
        return false;
    }

    private static string BuildLoadoutTooltip(Pawn pawn, Loadout loadout)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(pawn.LabelCap);

        // Loadout slots
        if (loadout != null && !loadout.defaultLoadout)
        {
            sb.AppendLine("— " + "CEQL_Loadout".Translate() + ": " + loadout.label + " —");
            foreach (var slot in loadout.Slots)
            {
                string name = slot.thingDef != null ? slot.thingDef.LabelCap : slot.genericDef?.LabelCap ?? "?";
                sb.AppendLine("  " + name + " x" + slot.count);
            }
        }
        else
        {
            sb.AppendLine("— " + "CEQL_NoLoadout".Translate() + " —");
        }

        // HoldTracker records
        var holds = LoadoutManager.GetHoldRecords(pawn);
        if (holds != null && holds.Count > 0)
        {
            sb.AppendLine("— " + "CEQL_HoldItems".Translate() + " —");
            foreach (var hr in holds)
                sb.AppendLine("  " + hr.thingDef.LabelCap + " x" + hr.count);
        }

        return sb.ToString().TrimEnd();
    }

    private static void ShowColonistSubmenu(ThingDef def, string itemLabel)
    {
        var subOptions = new List<FloatMenuOption>();
        foreach (var pawn in PawnsFinder.AllMaps_FreeColonists)
        {
            if (pawn.IsSlave) continue;
            var p = pawn;
            var loadout = p.GetLoadout();
            string tooltip = BuildLoadoutTooltip(p, loadout);

            subOptions.Add(new FloatMenuOption(
                p.LabelShortCap,
                () => AddToColonistLoadout(p, def, itemLabel),
                mouseoverGuiAction: rect => TooltipHandler.TipRegion(rect, tooltip)));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void AddToColonistLoadout(Pawn pawn, ThingDef def, string itemLabel)
    {
        // Get Loadout_Multi directly — allowNull=false ensures it's created
        var multi = CombatExtended.ExtendedLoadout.LoadoutMulti_Manager.GetLoadout(pawn, allowNull: false)
            as CombatExtended.ExtendedLoadout.Loadout_Multi;

        if (multi != null)
        {
            var personal = multi.PersonalLoadout;
            if (personal != null)
            {
                personal.AddSlot(new LoadoutSlot(def, 1));
                multi.NotifyLoadoutChanged();
                Find.WindowStack.Add(new Dialog_ManageLoadouts(personal));
            }
        }
        else
        {
            // Fallback for vanilla CE without ExtendedLoadout
            var loadout = pawn.GetLoadout();
            if (loadout != null && !loadout.defaultLoadout)
            {
                loadout.AddSlot(new LoadoutSlot(def, 1));
            }
            else
            {
                var newLoadout = new Loadout(pawn.LabelShortCap);
                newLoadout.AddSlot(new LoadoutSlot(def, 1));
                LoadoutManager.AddLoadout(newLoadout);
                pawn.SetLoadout(newLoadout);
            }
        }
        Messages.Message("CEQL_ItemAddedToPawn".Translate(itemLabel, pawn.LabelShortCap),
            MessageTypeDefOf.PositiveEvent, false);
    }

    private static void CreateLoadout(ThingDef def, string label)
    {
        var newLoadout = new Loadout(label);
        newLoadout.AddSlot(new LoadoutSlot(def, 1));
        LoadoutManager.AddLoadout(newLoadout);
        Find.WindowStack.Add(new Dialog_ManageLoadouts(newLoadout));
    }
}
