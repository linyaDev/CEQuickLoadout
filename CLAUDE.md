# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

CE Quick Loadout — a RimWorld 1.5 mod for managing Combat Extended loadouts via right-click context menu on map items. Integrates with CE's ExtendedLoadout (PersonalLoadout). Part of the HSK modpack.

## Build

```bash
dotnet build Source/CEQuickLoadout/CEQuickLoadout.csproj -c Release
```

Output goes to `1.5/Assemblies/`. Close the game before building (DLL lock).

## Architecture

Single Harmony patch mod — two source files:
- `Source/CEQuickLoadout/CEQuickLoadoutInit.cs` — StaticConstructorOnStartup, applies Harmony patches
- `Source/CEQuickLoadout/Patch_RightClickItem.cs` — Prefix patch on `FloatMenuMakerMap.TryMakeFloatMenu_NonPawn`; builds the right-click menu with colonist submenu, loadout creation, and ammo info tooltip

## Key Dependencies

- **Assembly-CSharp** (RimWorld) — publicized via BepInEx.AssemblyPublicizer.MSBuild
- **CombatExtended** — publicized; uses `LoadoutManager`, `Loadout`, `LoadoutSlot`, `CompProperties_AmmoUser`
- **CombatExtended.ExtendedLoadout** — publicized; uses `LoadoutMulti_Manager`, `Loadout_Multi.PersonalLoadout`
- **Harmony** — runtime patching

Reference paths use `D:\RimWorld_HSK_1.5` for game DLLs and `D:\Mods\Hardcore-SK` for HSK mod DLLs.

## Localization

Translation keys in `Languages/{English,Russian}/Keyed/CEQuickLoadout.xml`. All user-facing strings use `"KEY".Translate()`.
