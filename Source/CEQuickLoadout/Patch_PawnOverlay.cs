using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CEQuickLoadout;

// Draws a warning icon above colonists flagged by LoadoutWatcher:
//   red    "!" — a required weapon is missing
//   orange "!" — carried ammo is below the loadout target
// DrawPawnGUIOverlay runs in the map OnGUI pass, so we draw directly with
// GUI.DrawTexture using a screen position projected from the pawn.
[HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
public static class Patch_PawnOverlay
{
    private const float IconSize = 20f;

    private static Texture2D weaponIcon;
    private static Texture2D ammoIcon;
    private static bool iconsResolved;

    private static void ResolveIcons()
    {
        if (iconsResolved) return;
        iconsResolved = true;
        weaponIcon = ContentFinder<Texture2D>.Get("UI/Overlays/CEQL_WeaponWarning", reportFailure: false);
        ammoIcon = ContentFinder<Texture2D>.Get("UI/Overlays/CEQL_AmmoWarning", reportFailure: false);
    }

    public static void Postfix(PawnUIOverlay __instance)
    {
        var pawn = __instance.pawn; // private field, accessible via publicized Assembly-CSharp
        if (pawn == null || !pawn.Spawned || pawn.Map != Find.CurrentMap) return;
        if (!pawn.RaceProps.Humanlike) return;

        var watcher = pawn.Map.GetComponent<LoadoutWatcher>();
        if (watcher == null) return;

        var report = watcher.GetReport(pawn);
        if (report == null || report.Level == LoadoutWarning.None) return;

        ResolveIcons();
        var tex = report.Level == LoadoutWarning.WeaponMissing ? weaponIcon : ammoIcon;
        if (tex == null) return;

        // Anchor the icon at the pawn's top-left corner (scales with zoom).
        Vector3 world = pawn.DrawPos;
        world.x -= 0.5f; // half a cell left
        world.z += 0.5f; // half a cell up (north)
        Vector2 pos = Find.Camera.WorldToScreenPoint(world) / Prefs.UIScale;
        pos.y = UI.screenHeight - pos.y;
        var rect = new Rect(pos.x - IconSize / 2f, pos.y - IconSize / 2f, IconSize, IconSize);

        GUI.DrawTexture(rect, tex);
        if (Mouse.IsOver(rect))
            TooltipHandler.TipRegion(rect, BuildTooltip(report));
    }

    // Lists every shortfall by name: missing weapons first, then low ammo with
    // a have/need count so the player sees exactly what to top up.
    private static string BuildTooltip(LoadoutReport report)
    {
        var sb = new StringBuilder();
        sb.Append("CEQL_LoadoutWarningHeader".Translate());

        foreach (var w in report.MissingWeapons)
        {
            sb.AppendLine();
            sb.Append("CEQL_TipWeaponLine".Translate(w.Label, w.Have, w.Need));
        }
        foreach (var a in report.LowAmmo)
        {
            sb.AppendLine();
            sb.Append("CEQL_TipAmmoLine".Translate(a.Label, a.Have, a.Need));
        }
        return sb.ToString();
    }
}
