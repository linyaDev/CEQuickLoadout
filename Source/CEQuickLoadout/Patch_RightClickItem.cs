using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEQuickLoadout;

#if !RIMWORLD_16
[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.TryMakeFloatMenu_NonPawn))]
#endif
public static class Patch_RightClickItem
{
#if !RIMWORLD_16
    public static bool Prefix(Thing selectedThing)
    {
        return !TryShowMenu(selectedThing);
    }
#else
    public static void HandleMapClicks_Postfix(Selector __instance)
    {
        if (Event.current.type != EventType.MouseDown || Event.current.button != 1) return;
        var thing = Find.Selector.SingleSelectedThing;
        if (thing == null || thing is Pawn) return;
        if (TryShowMenu(thing))
            Event.current.Use();
    }
#endif

    public static bool TryShowMenu(Thing selectedThing)
    {
        if (selectedThing?.Map != Find.CurrentMap) return false;

        if (selectedThing.def.category != ThingCategory.Item)
            return selectedThing is ISlotGroupParent && TryShowStorageContentsMenu(selectedThing);

        var loadouts = LoadoutManager.Loadouts;
        if (loadouts == null) return false;

        var options = new List<FloatMenuOption>();
        var thingDef = selectedThing.def;
        string itemLabel = thingDef.LabelCap;

        // Ammo info for ranged weapons
        string ammoInfo = GetAmmoInfo(thingDef);
        if (ammoInfo != null)
        {
            options.Add(new FloatMenuOption(
                "CEQL_AmmoInfo".Translate(),
                () => {},
                mouseoverGuiAction: rect => TooltipHandler.TipRegion(rect, ammoInfo)));
        }

        bool isApparel = thingDef.IsApparel;
        bool isFood = thingDef.IsIngestible;

        // Diet: info tooltip + add/remove, nested under one category entry
        if (isFood)
        {
            options.Add(new FloatMenuOption(
                "CEQL_DietMenu".Translate(),
                () => ShowDietSubmenu(thingDef, itemLabel)));
        }

        // Outfit: info tooltip + add/remove/assign, nested under one category entry
        if (isApparel)
        {
            options.Add(new FloatMenuOption(
                "CEQL_OutfitMenu".Translate(),
                () => ShowOutfitSubmenu(thingDef, itemLabel)));
        }
        else
        {
            // Loadout: add/remove, nested under one category entry
            options.Add(new FloatMenuOption(
                "CEQL_LoadoutMenu".Translate(),
                () => ShowLoadoutSubmenu(thingDef, itemLabel)));
        }

        // Storage: add/move to a stockpile zone or storage building, picked by clicking on the map
        options.Add(new FloatMenuOption(
            "CEQL_StorageMenu".Translate(),
            () => ShowStorageSubmenu(thingDef, itemLabel, selectedThing)));

        // 3. Create new loadout / outfit — flag it if one already covers this item
        if (isApparel)
        {
            var existingOutfits = FindOutfitsAllowing(thingDef);
            string createOutfitLabel = "CEQL_CreateOutfit".Translate(itemLabel);
            if (existingOutfits.Count > 0)
                createOutfitLabel += " " + "CEQL_AlreadyExists".Translate();

            options.Add(new FloatMenuOption(
                createOutfitLabel,
                () => CreateOutfit(thingDef),
                mouseoverGuiAction: existingOutfits.Count > 0
                    ? rect => TooltipHandler.TipRegion(rect, "CEQL_ExistingContainers".Translate(string.Join(", ", existingOutfits)))
                    : null));
        }
        else
        {
            var existingLoadouts = FindLoadoutsContaining(thingDef);
            string createLoadoutLabel = "CEQL_CreateLoadout".Translate(itemLabel);
            if (existingLoadouts.Count > 0)
                createLoadoutLabel += " " + "CEQL_AlreadyExists".Translate();

            options.Add(new FloatMenuOption(
                createLoadoutLabel,
                () => CreateLoadout(thingDef, itemLabel),
                mouseoverGuiAction: existingLoadouts.Count > 0
                    ? rect => TooltipHandler.TipRegion(rect, "CEQL_ExistingContainers".Translate(string.Join(", ", existingLoadouts)))
                    : null));
        }

        Find.WindowStack.Add(new FloatMenu(options));
        return true;
    }

    // Right-clicking a selected storage building (shelf, pallet, etc.) lists the items
    // held inside it instead of the usual building menu, so picking a cramped item out
    // of a full shelf doesn't require pixel-perfect clicking on its tiny icon. Choosing
    // an entry re-enters TryShowMenu for that specific item, showing our normal menu.
    private static bool TryShowStorageContentsMenu(Thing storageThing)
    {
        var slotGroup = storageThing.Map.haulDestinationManager.SlotGroupAt(storageThing.Position);
        var heldThings = slotGroup?.HeldThings?.ToList();
        if (heldThings == null || heldThings.Count == 0) return false;

        var options = new List<FloatMenuOption>();
        foreach (var item in heldThings)
        {
            var it = item;
            options.Add(new FloatMenuOption(
                it.LabelCap + " x" + it.stackCount,
                () => TryShowMenu(it)));
        }
        Find.WindowStack.Add(new FloatMenu(options));
        return true;
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

    private static string GetAmmoInfo(ThingDef def)
    {
        var ammoProps = def.GetCompProperties<CompProperties_AmmoUser>();
        if (ammoProps?.ammoSet?.ammoTypes == null) return null;

        var map = Find.CurrentMap;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(def.LabelCap);
        sb.AppendLine("CEQL_MagazineSize".Translate() + ": " + ammoProps.magazineSize);
        sb.AppendLine("CEQL_AmmoTypes".Translate() + ":");
        foreach (var link in ammoProps.ammoSet.ammoTypes)
        {
            if (link.ammo == null) continue;
            int count = map?.resourceCounter.GetCount(link.ammo) ?? 0;
            if (count <= 0) continue;
            sb.AppendLine("  • " + link.ammo.LabelCap + " x" + count);
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildLoadoutSlotsTooltip(Loadout loadout)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var slot in loadout.Slots)
        {
            string name = slot.thingDef != null ? slot.thingDef.LabelCap : slot.genericDef?.LabelCap ?? "?";
            sb.AppendLine(name + " x" + slot.count);
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildLoadoutTooltip(Pawn pawn, Loadout loadout)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(pawn.LabelCap);

        // Loadout slots
        if (loadout != null && !loadout.defaultLoadout)
        {
            foreach (var slot in loadout.Slots)
            {
                string name = slot.thingDef != null ? slot.thingDef.LabelCap : slot.genericDef?.LabelCap ?? "?";
                sb.AppendLine(name + " x" + slot.count);
            }
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

    private static void ShowLoadoutSubmenu(ThingDef def, string itemLabel)
    {
        var subOptions = new List<FloatMenuOption>
        {
            new FloatMenuOption("CEQL_AddToColonistMenu".Translate(), () => ShowAddSubmenu(def, itemLabel)),
            new FloatMenuOption("CEQL_RemoveFromColonistMenu".Translate(), () => ShowRemoveColonistSubmenu()),
        };
        Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowAddSubmenu(ThingDef def, string itemLabel)
    {
        var subOptions = new List<FloatMenuOption>();

        // Option 1: By colonist (personal loadouts)
        subOptions.Add(new FloatMenuOption(
            "CEQL_AddByColonist".Translate(),
            () => ShowAddByColonistSubmenu(def, itemLabel)));

        // Option 2: By loadout (shared loadouts)
        subOptions.Add(new FloatMenuOption(
            "CEQL_AddByLoadout".Translate(),
            () => ShowAddByLoadoutSubmenu(def, itemLabel)));

        Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowAddByColonistSubmenu(ThingDef def, string itemLabel)
    {
        var subOptions = new List<FloatMenuOption>();
        var colonists = Find.ColonistBar?.GetColonistsInOrder() ?? PawnsFinder.AllMaps_FreeColonists;
        foreach (var pawn in colonists)
        {
            if (pawn.IsSlave) continue;
            var p = pawn;
            var multi = CombatExtended.ExtendedLoadout.LoadoutMulti_Manager.GetLoadout(p, allowNull: true)
                as CombatExtended.ExtendedLoadout.Loadout_Multi;
            Loadout loadout = multi?.PersonalLoadout ?? p.GetLoadout();
            string tooltip = BuildLoadoutTooltip(p, loadout);

            subOptions.Add(new FloatMenuOption(
                p.LabelShortCap,
                () => AddToColonistLoadout(p, def, itemLabel),
                mouseoverGuiAction: rect => TooltipHandler.TipRegion(rect, tooltip)));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowAddByLoadoutSubmenu(ThingDef def, string itemLabel)
    {
        var subOptions = new List<FloatMenuOption>();
        foreach (var loadout in LoadoutManager.Loadouts)
        {
            if (loadout.defaultLoadout) continue;
            var lo = loadout;
            string tooltip = BuildLoadoutSlotsTooltip(lo);
            subOptions.Add(new FloatMenuOption(
                lo.label,
                () =>
                {
                    lo.AddSlot(new LoadoutSlot(def, 1));
                    Messages.Message("CEQL_ItemAddedToLoadout".Translate(itemLabel, lo.label),
                        MessageTypeDefOf.PositiveEvent, false);
                },
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

    private static void ShowRemoveColonistSubmenu()
    {
        var subOptions = new List<FloatMenuOption>();

        // Option 1: By colonist (personal loadouts)
        subOptions.Add(new FloatMenuOption(
            "CEQL_RemoveByColonist".Translate(),
            () => ShowRemoveByColonistSubmenu()));

        // Option 2: By loadout (shared loadouts)
        subOptions.Add(new FloatMenuOption(
            "CEQL_RemoveByLoadout".Translate(),
            () => ShowRemoveByLoadoutSubmenu()));

        Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowRemoveByColonistSubmenu()
    {
        var subOptions = new List<FloatMenuOption>();
        var colonists = Find.ColonistBar?.GetColonistsInOrder() ?? PawnsFinder.AllMaps_FreeColonists;
        foreach (var pawn in colonists)
        {
            if (pawn.IsSlave) continue;
            var p = pawn;
            var multi = CombatExtended.ExtendedLoadout.LoadoutMulti_Manager.GetLoadout(p, allowNull: true)
                as CombatExtended.ExtendedLoadout.Loadout_Multi;
            Loadout loadout = multi?.PersonalLoadout ?? p.GetLoadout();
            if (loadout == null || loadout.defaultLoadout || loadout.Slots.Count == 0) continue;

            string tooltip = BuildLoadoutTooltip(p, loadout);
            subOptions.Add(new FloatMenuOption(
                p.LabelShortCap,
                () => ShowRemoveSlotSubmenu(p),
                mouseoverGuiAction: rect => TooltipHandler.TipRegion(rect, tooltip)));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowRemoveByLoadoutSubmenu()
    {
        var subOptions = new List<FloatMenuOption>();
        foreach (var loadout in LoadoutManager.Loadouts)
        {
            if (loadout.defaultLoadout || loadout.Slots.Count == 0) continue;
            var lo = loadout;
            string tooltip = BuildLoadoutSlotsTooltip(lo);
            subOptions.Add(new FloatMenuOption(
                lo.label,
                () => ShowRemoveSlotFromLoadoutSubmenu(lo),
                mouseoverGuiAction: rect => TooltipHandler.TipRegion(rect, tooltip)));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowRemoveSlotFromLoadoutSubmenu(Loadout loadout)
    {
        var slotOptions = new List<FloatMenuOption>();
        foreach (var slot in loadout.Slots)
        {
            var s = slot;
            string name = s.thingDef != null ? s.thingDef.LabelCap : s.genericDef?.LabelCap ?? "?";
            slotOptions.Add(new FloatMenuOption(
                name + " x" + s.count,
                () =>
                {
                    loadout.RemoveSlot(s);
                    Messages.Message("CEQL_ItemRemovedFromLoadout".Translate(name, loadout.label),
                        MessageTypeDefOf.NeutralEvent, false);
                }));
        }
        if (slotOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(slotOptions));
    }

    private static void ShowRemoveSlotSubmenu(Pawn pawn)
    {
        var multi = CombatExtended.ExtendedLoadout.LoadoutMulti_Manager.GetLoadout(pawn, allowNull: true)
            as CombatExtended.ExtendedLoadout.Loadout_Multi;

        Loadout loadout = multi?.PersonalLoadout ?? pawn.GetLoadout();
        if (loadout == null || loadout.defaultLoadout || loadout.Slots.Count == 0) return;

        var slotOptions = new List<FloatMenuOption>();
        foreach (var slot in loadout.Slots)
        {
            var s = slot;
            string name = s.thingDef != null ? s.thingDef.LabelCap : s.genericDef?.LabelCap ?? "?";
            slotOptions.Add(new FloatMenuOption(
                name + " x" + s.count,
                () =>
                {
                    loadout.RemoveSlot(s);
                    multi?.NotifyLoadoutChanged();
                    if (s.thingDef != null)
                        SidearmsHelper.ForgetWeaponDef(pawn, s.thingDef);
                    Messages.Message("CEQL_ItemRemovedFromPawn".Translate(name, pawn.LabelShortCap),
                        MessageTypeDefOf.NeutralEvent, false);
                }));
        }
        if (slotOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(slotOptions));
    }

    private static void ShowOutfitSubmenu(ThingDef def, string itemLabel)
    {
        var subOptions = new List<FloatMenuOption>();
        string outfitInfo = GetOutfitInfo(def);
        subOptions.Add(new FloatMenuOption(
            "CEQL_OutfitInfo".Translate(),
            () => {},
            mouseoverGuiAction: outfitInfo != null
                ? rect => TooltipHandler.TipRegion(rect, outfitInfo)
                : null));
        subOptions.Add(new FloatMenuOption("CEQL_AddToOutfit".Translate(), () => ShowAddToOutfitSubmenu(def, itemLabel)));
        subOptions.Add(new FloatMenuOption("CEQL_RemoveFromOutfit".Translate(), () => ShowRemoveFromOutfitSubmenu(def, itemLabel)));
        subOptions.Add(new FloatMenuOption("CEQL_AssignOutfit".Translate(), () => ShowAssignOutfitColonistSubmenu()));
        Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowAddToOutfitSubmenu(ThingDef def, string itemLabel)
    {
        var outfits = Current.Game?.outfitDatabase?.AllOutfits;
        if (outfits == null) return;

        var subOptions = new List<FloatMenuOption>();
        foreach (var outfit in outfits)
        {
            if (outfit.filter.Allows(def)) continue;
            var o = outfit;
            string tooltip = GetOutfitInfo(def);
            subOptions.Add(new FloatMenuOption(
                o.label,
                () =>
                {
                    o.filter.SetAllow(def, true);
                    Messages.Message("CEQL_ItemAddedToOutfit".Translate(itemLabel, o.label),
                        MessageTypeDefOf.PositiveEvent, false);
                },
                mouseoverGuiAction: tooltip != null ? rect => TooltipHandler.TipRegion(rect, tooltip) : null));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowRemoveFromOutfitSubmenu(ThingDef def, string itemLabel)
    {
        var outfits = Current.Game?.outfitDatabase?.AllOutfits;
        if (outfits == null) return;

        var subOptions = new List<FloatMenuOption>();
        foreach (var outfit in outfits)
        {
            if (!outfit.filter.Allows(def)) continue;
            var o = outfit;
            string tooltip = GetOutfitInfo(def);
            subOptions.Add(new FloatMenuOption(
                o.label,
                () =>
                {
                    o.filter.SetAllow(def, false);
                    Messages.Message("CEQL_ItemRemovedFromOutfit".Translate(itemLabel, o.label),
                        MessageTypeDefOf.NeutralEvent, false);
                },
                mouseoverGuiAction: tooltip != null ? rect => TooltipHandler.TipRegion(rect, tooltip) : null));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowAssignOutfitColonistSubmenu()
    {
        var subOptions = new List<FloatMenuOption>();
        var colonists = Find.ColonistBar?.GetColonistsInOrder() ?? PawnsFinder.AllMaps_FreeColonists;
        foreach (var pawn in colonists)
        {
            if (pawn.IsSlave) continue;
            var p = pawn;
            string currentOutfit = p.outfits?.CurrentApparelPolicy?.label ?? "?";
            subOptions.Add(new FloatMenuOption(
                p.LabelShortCap + " (" + currentOutfit + ")",
                () => ShowAssignOutfitSubmenu(p)));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowAssignOutfitSubmenu(Pawn pawn)
    {
        var outfits = Current.Game?.outfitDatabase?.AllOutfits;
        if (outfits == null) return;

        var subOptions = new List<FloatMenuOption>();
        foreach (var outfit in outfits)
        {
            var o = outfit;
            subOptions.Add(new FloatMenuOption(
                o.label,
                () =>
                {
                    pawn.outfits.CurrentApparelPolicy = o;
                    Messages.Message("CEQL_OutfitAssigned".Translate(o.label, pawn.LabelShortCap),
                        MessageTypeDefOf.PositiveEvent, false);
                }));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static string GetDietInfo(ThingDef def)
    {
        var diets = Current.Game?.foodRestrictionDatabase?.AllFoodRestrictions;
        if (diets == null) return null;

        var sb = new System.Text.StringBuilder();
        foreach (var diet in diets)
        {
            if (diet.filter.Allows(def))
                sb.AppendLine("✓ " + diet.label);
            else
                sb.AppendLine("   " + diet.label);
        }
        return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
    }

    private static void ShowDietSubmenu(ThingDef def, string itemLabel)
    {
        var subOptions = new List<FloatMenuOption>();
        string dietInfo = GetDietInfo(def);
        subOptions.Add(new FloatMenuOption(
            "CEQL_DietInfo".Translate(),
            () => {},
            mouseoverGuiAction: dietInfo != null
                ? rect => TooltipHandler.TipRegion(rect, dietInfo)
                : null));
        subOptions.Add(new FloatMenuOption("CEQL_AddToDiet".Translate(), () => ShowAddToDietSubmenu(def, itemLabel)));
        subOptions.Add(new FloatMenuOption("CEQL_RemoveFromDiet".Translate(), () => ShowRemoveFromDietSubmenu(def, itemLabel)));
        Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowAddToDietSubmenu(ThingDef def, string itemLabel)
    {
        var diets = Current.Game?.foodRestrictionDatabase?.AllFoodRestrictions;
        if (diets == null) return;

        var subOptions = new List<FloatMenuOption>();
        foreach (var diet in diets)
        {
            if (diet.filter.Allows(def)) continue;
            var d = diet;
            string tooltip = GetDietInfo(def);
            subOptions.Add(new FloatMenuOption(
                d.label,
                () =>
                {
                    d.filter.SetAllow(def, true);
                    Messages.Message("CEQL_ItemAddedToDiet".Translate(itemLabel, d.label),
                        MessageTypeDefOf.PositiveEvent, false);
                },
                mouseoverGuiAction: tooltip != null ? rect => TooltipHandler.TipRegion(rect, tooltip) : null));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowRemoveFromDietSubmenu(ThingDef def, string itemLabel)
    {
        var diets = Current.Game?.foodRestrictionDatabase?.AllFoodRestrictions;
        if (diets == null) return;

        var subOptions = new List<FloatMenuOption>();
        foreach (var diet in diets)
        {
            if (!diet.filter.Allows(def)) continue;
            var d = diet;
            string tooltip = GetDietInfo(def);
            subOptions.Add(new FloatMenuOption(
                d.label,
                () =>
                {
                    d.filter.SetAllow(def, false);
                    Messages.Message("CEQL_ItemRemovedFromDiet".Translate(itemLabel, d.label),
                        MessageTypeDefOf.NeutralEvent, false);
                },
                mouseoverGuiAction: tooltip != null ? rect => TooltipHandler.TipRegion(rect, tooltip) : null));
        }
        if (subOptions.Count > 0)
            Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    private static void ShowStorageSubmenu(ThingDef def, string itemLabel, Thing thing)
    {
        var subOptions = new List<FloatMenuOption>
        {
            new FloatMenuOption("CEQL_StorageAdd".Translate(), () => StartStorageTargeting(def, itemLabel, thing, move: false)),
            new FloatMenuOption("CEQL_StorageMove".Translate(), () => StartStorageTargeting(def, itemLabel, thing, move: true)),
        };
        Find.WindowStack.Add(new FloatMenu(subOptions));
    }

    // Lets the player click a stockpile zone or storage building on the map, then
    // allows the item's def in that storage's filter. "Move" additionally disallows
    // the def in whatever storage the clicked item is currently sitting in, so vanilla
    // hauling picks it up and relocates it to the newly allowed storage.
    private static void StartStorageTargeting(ThingDef def, string itemLabel, Thing thing, bool move)
    {
        var map = Find.CurrentMap;
        if (map == null) return;

        var targetParams = new TargetingParameters
        {
            canTargetLocations = true,
            canTargetBuildings = true,
            canTargetItems = false,
            canTargetPawns = false,
            canTargetSelf = false,
            validator = t => CanStoreHere(map, t.Cell, def)
        };

        Find.Targeter.BeginTargeting(targetParams, target => OnStorageTargeted(target, def, itemLabel, thing, move, map));
    }

    // A storage building's own fixed filter (GetParentStoreSettings) reflects what it's
    // physically capable of holding (e.g. a pallet built only for a specific category) —
    // separate from the player-editable filter we're about to change. Zones have no such
    // fixed filter, so anything goes there.
    private static bool CanStoreHere(Map map, IntVec3 cell, ThingDef def)
    {
        var parent = map.haulDestinationManager.SlotGroupAt(cell)?.parent;
        if (parent == null) return false;
        var parentSettings = parent.GetParentStoreSettings();
        return parentSettings == null || parentSettings.filter.Allows(def);
    }

    private static void OnStorageTargeted(LocalTargetInfo target, ThingDef def, string itemLabel, Thing thing, bool move, Map map)
    {
        var parent = map.haulDestinationManager.SlotGroupAt(target.Cell)?.parent;
        if (parent == null || !CanStoreHere(map, target.Cell, def))
        {
            Messages.Message("CEQL_StorageCantHold".Translate(itemLabel), MessageTypeDefOf.RejectInput, false);
            return;
        }

        parent.GetStoreSettings().filter.SetAllow(def, true);
        string newLabel = parent.SlotYielderLabel();

        if (move && thing?.Spawned == true)
        {
            var oldParent = thing.Map.haulDestinationManager.SlotGroupAt(thing.Position)?.parent;
            if (oldParent != null && oldParent != parent)
            {
                oldParent.GetStoreSettings().filter.SetAllow(def, false);
                Messages.Message("CEQL_ItemMovedToStorage".Translate(itemLabel, oldParent.SlotYielderLabel(), newLabel),
                    MessageTypeDefOf.PositiveEvent, false);
                return;
            }
        }

        Messages.Message("CEQL_ItemAddedToStorage".Translate(itemLabel, newLabel),
            MessageTypeDefOf.PositiveEvent, false);
    }

    private static List<string> FindLoadoutsContaining(ThingDef def)
    {
        var result = new List<string>();
        foreach (var loadout in LoadoutManager.Loadouts)
        {
            if (loadout.defaultLoadout) continue;
            foreach (var slot in loadout.Slots)
            {
                if (slot.thingDef == def)
                {
                    result.Add(loadout.label);
                    break;
                }
            }
        }
        return result;
    }

    private static List<string> FindOutfitsAllowing(ThingDef def)
    {
        var result = new List<string>();
        var outfits = Current.Game?.outfitDatabase?.AllOutfits;
        if (outfits == null) return result;
        foreach (var outfit in outfits)
        {
            if (outfit.filter.Allows(def))
                result.Add(outfit.label);
        }
        return result;
    }

    private static void CreateOutfit(ThingDef def)
    {
        var outfit = Current.Game.outfitDatabase.MakeNewOutfit();
        outfit.filter.SetDisallowAll();
        outfit.filter.SetAllow(def, true);
        Find.WindowStack.Add(new Dialog_RenamePolicy(outfit));
    }

    private static void CreateLoadout(ThingDef def, string label)
    {
        var newLoadout = new Loadout(label);
        newLoadout.AddSlot(new LoadoutSlot(def, 1));
        LoadoutManager.AddLoadout(newLoadout);

        var ext = CombatExtended.ExtendedLoadout.Loadout_Extended.Get(newLoadout);
        ext.HpRange = new FloatRange(0.5f, 1f);
        ext.RefillThreshold = 0.85f;

        Find.WindowStack.Add(new Dialog_ManageLoadouts(newLoadout));
    }
}
