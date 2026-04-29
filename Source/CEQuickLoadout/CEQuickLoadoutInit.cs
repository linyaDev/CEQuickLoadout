using HarmonyLib;
using Verse;

namespace CEQuickLoadout;

[StaticConstructorOnStartup]
public static class CEQuickLoadoutInit
{
    static CEQuickLoadoutInit()
    {
        new Harmony("linya.cequickloadout").PatchAll();
    }
}
