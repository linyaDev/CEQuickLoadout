using System.Collections.Generic;
using CombatExtended;
using Verse;

namespace CEQuickLoadout;

public enum LoadoutWarning
{
    None = 0,
    AmmoLow = 1,      // orange: carried ammo below AmmoLowFraction of the loadout target
    WeaponMissing = 2 // red: a required weapon is not equipped or in inventory (takes priority)
}

// A single shortfall against the loadout: a missing weapon or low ammo stack.
public struct LoadoutShortfall
{
    public string Label; // user-facing item name
    public int Have;     // amount currently carried
    public int Need;     // amount the loadout asks for
}

// Detailed result of checking one pawn against their CE loadout. The icon level
// is derived: a missing weapon (red) outranks low ammo (orange).
public class LoadoutReport
{
    public readonly List<LoadoutShortfall> MissingWeapons = new List<LoadoutShortfall>();
    public readonly List<LoadoutShortfall> LowAmmo = new List<LoadoutShortfall>();

    public LoadoutWarning Level =>
        MissingWeapons.Count > 0 ? LoadoutWarning.WeaponMissing :
        LowAmmo.Count > 0 ? LoadoutWarning.AmmoLow :
        LoadoutWarning.None;
}

// Once per in-game hour, checks every spawned colonist's CE loadout and records
// what they are short on, shown above the pawn by Patch_PawnOverlay.
// MapComponents with a (Map) constructor are auto-instantiated by Map.FillComponents().
public class LoadoutWatcher : MapComponent
{
    private const int CheckIntervalTicks = 2500; // 1 in-game hour
    private const float AmmoLowFraction = 0.3f;  // below 30% of the loadout target = low

    private readonly Dictionary<Pawn, LoadoutReport> reports = new Dictionary<Pawn, LoadoutReport>();

    public LoadoutWatcher(Map map) : base(map) { }

    public LoadoutWarning GetWarning(Pawn pawn) =>
        reports.TryGetValue(pawn, out var r) ? r.Level : LoadoutWarning.None;

    public LoadoutReport GetReport(Pawn pawn) =>
        reports.TryGetValue(pawn, out var r) ? r : null;

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % CheckIntervalTicks != 0) return;
        Recheck();
    }

    private void Recheck()
    {
        reports.Clear();
        foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
        {
            if (pawn.IsSlave) continue;
            var report = Evaluate(pawn);
            if (report.Level != LoadoutWarning.None)
                reports[pawn] = report;
        }
    }

    private static LoadoutReport Evaluate(Pawn pawn)
    {
        var report = new LoadoutReport();

        var loadout = pawn.GetLoadout();
        if (loadout == null || loadout.defaultLoadout) return report;

        foreach (var slot in loadout.Slots)
        {
            var def = slot.thingDef;
            if (def == null || slot.count <= 0) continue;

            if (def.IsWeapon)
            {
                int have = CountCarried(pawn, def);
                if (have < slot.count)
                    report.MissingWeapons.Add(new LoadoutShortfall
                    {
                        Label = def.LabelCap,
                        Have = have,
                        Need = slot.count
                    });
            }
            else if (def is AmmoDef)
            {
                int have = CountInInventory(pawn, def);
                if (have < slot.count * AmmoLowFraction)
                    report.LowAmmo.Add(new LoadoutShortfall
                    {
                        Label = def.LabelCap,
                        Have = have,
                        Need = slot.count
                    });
            }
        }

        return report;
    }

    // Weapons: equipped primary + inventory (sidearms).
    private static int CountCarried(Pawn pawn, ThingDef def)
    {
        int count = 0;

        var primary = pawn.equipment?.Primary;
        if (primary != null && primary.def == def)
            count += primary.stackCount;

        count += CountInInventory(pawn, def);
        return count;
    }

    private static int CountInInventory(Pawn pawn, ThingDef def)
    {
        int count = 0;
        var inv = pawn.inventory?.innerContainer;
        if (inv != null)
        {
            foreach (var thing in inv)
                if (thing.def == def)
                    count += thing.stackCount;
        }
        return count;
    }
}
