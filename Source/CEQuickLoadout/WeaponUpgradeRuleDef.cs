using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CEQuickLoadout;

// XML config: per-weapon stat used to compare upgrade candidates.
// Weapons not listed in any rule are compared by quality (default behavior).
// Def names are stored as strings so missing weapon/stat defs only warn instead of breaking load.
public class WeaponUpgradeRuleDef : Def
{
    public List<WeaponUpgradeRule> rules = new List<WeaponUpgradeRule>();
}

public class WeaponUpgradeRule
{
    public string weapon;
    public string stat;
}

public static class WeaponUpgradeRules
{
    private static Dictionary<ThingDef, StatDef> lookup;

    // Returns the stat to compare by for this weapon def, or null to compare by quality.
    public static StatDef StatFor(ThingDef weaponDef)
    {
        lookup ??= Build();
        return lookup.TryGetValue(weaponDef, out var stat) ? stat : null;
    }

    private static Dictionary<ThingDef, StatDef> Build()
    {
        var result = new Dictionary<ThingDef, StatDef>();
        foreach (var def in DefDatabase<WeaponUpgradeRuleDef>.AllDefs)
        {
            foreach (var rule in def.rules)
            {
                if (rule.weapon.NullOrEmpty() || rule.stat.NullOrEmpty())
                {
                    Log.Warning($"[CEQL] {def.defName}: rule with empty weapon or stat, skipping.");
                    continue;
                }

                var weapon = DefDatabase<ThingDef>.GetNamedSilentFail(rule.weapon);
                if (weapon == null)
                {
                    Log.Warning($"[CEQL] {def.defName}: unknown weapon def \"{rule.weapon}\", skipping.");
                    continue;
                }

                var stat = DefDatabase<StatDef>.GetNamedSilentFail(rule.stat);
                if (stat == null)
                {
                    Log.Warning($"[CEQL] {def.defName}: unknown stat \"{rule.stat}\" for {rule.weapon}, skipping.");
                    continue;
                }

                result[weapon] = stat;
            }
        }
        return result;
    }
}
