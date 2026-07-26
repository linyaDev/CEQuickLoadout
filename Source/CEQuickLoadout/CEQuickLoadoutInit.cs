using HarmonyLib;
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
    }
}
