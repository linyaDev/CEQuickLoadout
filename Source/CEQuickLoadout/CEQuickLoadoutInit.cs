using HarmonyLib;
using RimWorld;
using Verse;

namespace CEQuickLoadout;

[StaticConstructorOnStartup]
public static class CEQuickLoadoutInit
{
    static CEQuickLoadoutInit()
    {
        var harmony = new Harmony("linya.cequickloadout");
        harmony.PatchAll();
        Patch_RPGInventory.TryPatch(harmony);

#if RIMWORLD_16
        var handleMapClicks = AccessTools.Method(typeof(Selector), "HandleMapClicks");
        if (handleMapClicks != null)
            harmony.Patch(handleMapClicks, postfix: new HarmonyMethod(typeof(Patch_RightClickItem), nameof(Patch_RightClickItem.HandleMapClicks_Postfix)));

        var getOptions = AccessTools.Method(typeof(FloatMenuMakerMap), "GetOptions");
        if (getOptions != null)
            harmony.Patch(getOptions, postfix: new HarmonyMethod(typeof(Patch_RightClickPawn), nameof(Patch_RightClickPawn.GetOptions_Postfix)));
#endif
    }
}
