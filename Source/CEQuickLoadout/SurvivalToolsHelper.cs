using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace CEQuickLoadout;

// Optional SurvivalToolsLite integration. Its tools don't use equippedStatOffsets:
// the per-instance work factor is SurvivalTool.WorkStatFactors =
// baseWorkStatFactors (def) * ToolEffectivenessFactor (quality/material) * stuff toolStatFactors.
// That's the value shown on the tool's info card ("Mining speed 162%" etc.).
internal static class SurvivalToolsHelper
{
    private static bool initialized;
    private static Type toolType;
    private static PropertyInfo workStatFactorsProp;

    private static void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        toolType = GenTypes.GetTypeInAnyAssembly("SurvivalToolsLite.SurvivalTool");
        if (toolType == null) return;
        workStatFactorsProp = toolType.GetProperty("WorkStatFactors", BindingFlags.Public | BindingFlags.Instance);
    }

    public static bool TryGetWorkStatFactor(Thing t, StatDef stat, out float value)
    {
        value = 0f;
        EnsureInit();
        if (toolType == null || workStatFactorsProp == null || !toolType.IsInstanceOfType(t)) return false;

        try
        {
            if (workStatFactorsProp.GetValue(t, null) is not IEnumerable<StatModifier> factors) return false;
            foreach (var mod in factors)
            {
                if (mod.stat == stat)
                {
                    value = mod.value;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[CEQL] SurvivalToolsLite WorkStatFactors error: {ex.Message}");
        }
        return false;
    }
}
