using System;
using System.Collections;
using System.Reflection;
using Verse;

namespace CEQuickLoadout;

internal static class SidearmsHelper
{
    private static bool initialized;
    private static Type memoryType;
    private static MethodInfo getMemoryMethod;
    private static MethodInfo informDropMethod;
    private static MethodInfo informAddMethod;
    private static MethodInfo forgetMethod;
    private static PropertyInfo rememberedWeaponsProp;
    private static FieldInfo thingField;

    private static void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        memoryType = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.CompSidearmMemory");
        if (memoryType == null) return;

        const BindingFlags pub = BindingFlags.Public | BindingFlags.Instance;
        const BindingFlags pubStatic = BindingFlags.Public | BindingFlags.Static;
        getMemoryMethod = memoryType.GetMethod("GetMemoryCompForPawn", pubStatic);
        informDropMethod = memoryType.GetMethod("InformOfDroppedSidearm", pub);
        informAddMethod = memoryType.GetMethod("InformOfAddedSidearm", pub);
        forgetMethod = memoryType.GetMethod("ForgetSidearmMemory", pub);
        rememberedWeaponsProp = memoryType.GetProperty("RememberedWeapons", pub);

        var pairType = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.ThingDefStuffDefPair");
        if (pairType != null)
            thingField = pairType.GetField("thing", pub);
    }

    private static object GetMemory(Pawn pawn)
    {
        EnsureInit();
        if (getMemoryMethod == null) return null;
        try { return getMemoryMethod.Invoke(null, new object[] { pawn, true }); }
        catch { return null; }
    }

    public static void NotifySwap(Pawn pawn, Thing oldWeapon, Thing newWeapon)
    {
        var memory = GetMemory(pawn);
        if (memory == null) return;
        try
        {
            if (oldWeapon != null && informDropMethod != null)
                informDropMethod.Invoke(memory, new object[] { oldWeapon, true });
            if (newWeapon != null && informAddMethod != null)
                informAddMethod.Invoke(memory, new object[] { newWeapon });
        }
        catch (Exception ex)
        {
            Log.Warning($"[CEQL] SimpleSidearms swap error: {ex.Message}");
        }
    }

    public static void ForgetWeaponDef(Pawn pawn, ThingDef weaponDef)
    {
        EnsureInit();
        if (forgetMethod == null || rememberedWeaponsProp == null || thingField == null) return;

        var memory = GetMemory(pawn);
        if (memory == null) return;

        try
        {
            var remembered = rememberedWeaponsProp.GetValue(memory, null) as IList;
            if (remembered == null || remembered.Count == 0) return;

            var toForget = new System.Collections.Generic.List<object>();
            foreach (var pair in remembered)
            {
                if (thingField.GetValue(pair) == weaponDef)
                    toForget.Add(pair);
            }

            foreach (var pair in toForget)
                forgetMethod.Invoke(memory, new[] { pair });
        }
        catch (Exception ex)
        {
            Log.Warning($"[CEQL] SimpleSidearms forget error: {ex.Message}");
        }
    }
}
