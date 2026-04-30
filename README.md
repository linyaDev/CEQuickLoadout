# CE Quick Loadout

Right-click items on the map to manage Combat Extended loadouts without opening the CE manager.

## Features

- **Add to colonist** — select item, right-click, pick a colonist from the submenu. Hover to preview their current loadout.
- **Create loadout** — instantly create a new CE loadout with the selected item and open the manager.
- **ExtendedLoadout support** — adds items directly to colonist's PersonalLoadout when using CE Extended Loadout mod.

## How to use

1. Left-click an item on the map to select it
2. Right-click to open the context menu
3. Choose a colonist or create a new loadout

## Requirements

- RimWorld 1.5
- [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended)
- [Harmony](https://github.com/pardeike/HarmonyRimWorld)
- Hardcore SK modpack

## Building

```bash
dotnet build Source/CEQuickLoadout/CEQuickLoadout.csproj -c Release
```
