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
        listing.End();
    }
}
