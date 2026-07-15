using RimWorld;
using UnityEngine;
using Verse;

namespace CEQuickLoadout;

public class CEQuickLoadoutSettings : ModSettings
{
    public bool enableWeaponUpgradeSearch = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref enableWeaponUpgradeSearch, "enableWeaponUpgradeSearch", false);
        base.ExposeData();
    }
}

public class CEQuickLoadoutMod : Mod
{
    public static CEQuickLoadoutSettings Settings { get; private set; }

    public CEQuickLoadoutMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<CEQuickLoadoutSettings>();
    }

    public override string SettingsCategory() => "CE Quick Loadout";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.CheckboxLabeled(
            "CEQL_SettingUpgradeSearch".Translate(),
            ref Settings.enableWeaponUpgradeSearch,
            "CEQL_SettingUpgradeSearchDesc".Translate());
        listing.GapLine();
        if (listing.ButtonText("CEQL_SettingDumpDebug".Translate()))
            DumpDebugInfo();
        listing.End();
    }

    private static void DumpDebugInfo()
    {
        var map = Find.CurrentMap;
        if (map == null) { Log.Warning("[CEQL Debug] No map loaded"); return; }

        foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
        {
            if (pawn.IsSlave) continue;
            var sb = new System.Text.StringBuilder();
            var curJob = pawn.CurJob;
            string jobMod = curJob?.def?.modContentPack?.Name ?? "?";
            string driverType = pawn.jobs?.curDriver?.GetType().FullName ?? "?";

            sb.Append($"[CEQL Debug] {pawn.LabelShortCap}:");
            sb.Append($" Primary={pawn.equipment?.Primary?.LabelCap}(id={pawn.equipment?.Primary?.thingIDNumber})");
            sb.Append($" Job={curJob?.def?.defName}[{jobMod}] driver={driverType}");
            sb.Append($" target={curJob?.targetA.Thing?.LabelCap}");
            sb.Append($" Inv=[");

            var inv = pawn.inventory?.innerContainer;
            if (inv != null)
            {
                bool first = true;
                foreach (var t in inv)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    t.TryGetQuality(out var q);
                    sb.Append($"{t.LabelCap}(id={t.thingIDNumber},q={q})");
                }
            }
            sb.Append("]");
            Log.Message(sb.ToString());
        }
        Log.Message("[CEQL Debug] --- dump complete ---");
    }
}
