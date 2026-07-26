# CE Quick Loadout

Right-click items on the map to manage Combat Extended loadouts and outfits without opening any manager.

## Features

### Weapons & Tools
- **Add to loadout** — by colonist (personal loadout) or by shared loadout
- **Remove from loadout** — by colonist (personal) or by shared loadout
- **Create loadout** — instantly create a new CE loadout with the selected item
- **Ammo info** — hover to see compatible ammo types and stock on the map
- **Auto weapon upgrade** — pawns automatically find better quality versions of their loadout weapons (enabled by default, configurable in mod settings)
- **Loadout warnings** — pawn overlay shows when ammo is low, weapon is broken, or assigned weapon/tool is missing entirely. Hover to see what's missing.

### Apparel
- **Outfit info** — hover to see which outfits include this apparel
- **Add to outfit** — add apparel to any existing outfit
- **Remove from outfit** — remove apparel from an existing outfit
- **Change colonist outfit** — assign a different outfit to a colonist
- **Create outfit** — create a new outfit with the selected apparel

### RPG Inventory Integration
- **Outfit info** — hover to see which outfits include the apparel
- **Add to outfit** — add to any outfit from the inventory context menu
- **Remove from current outfit** — remove from the colonist's current outfit

### General
- Supports Combat Extended ExtendedLoadout (PersonalLoadout)
- Tooltips show loadout/outfit contents on hover
- Colonists sorted by colonist bar order
- English and Russian localization

## How to use

1. Left-click an item on the map to select it
2. Right-click to open the context menu
3. Weapons/tools: manage loadouts. Apparel: manage outfits.

## Requirements

- RimWorld 1.5
- [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended)
- [Harmony](https://github.com/pardeike/HarmonyRimWorld)
- Hardcore SK modpack

## Building

```bash
dotnet build Source/CEQuickLoadout/CEQuickLoadout.csproj -c Release
```

---

# CE Quick Loadout (Русский)

Правый клик по предметам на карте для управления лоадаутами и гардеробами Combat Extended без открытия менеджера.

## Возможности

### Оружие и инструменты
- **Добавить в лоадаут** — колонисту (персональный) или в общий лоадаут
- **Удалить из лоадаута** — у колониста (персональный) или из общего лоадаута
- **Создать лоадаут** — мгновенно создаёт новый CE лоадаут с выбранным предметом
- **Инфо о патронах** — наведите, чтобы увидеть совместимые патроны и запас на карте
- **Автопоиск лучшего оружия** — пешки автоматически находят оружие лучшего качества из лоадаута (включено по умолчанию, настраивается)
- **Предупреждения лоадаута** — иконка на пешке показывает если мало патронов, оружие сломано или назначенного оружия/инструмента нет. Наведите чтобы узнать чего не хватает.

### Одежда
- **В гардеробе** — наведите, чтобы увидеть в каких гардеробах есть эта одежда
- **Добавить в гардероб** — добавить одежду в любой существующий гардероб
- **Удалить из гардероба** — удалить одежду из гардероба
- **Сменить гардероб колонисту** — назначить другой гардероб колонисту
- **Создать гардероб** — создать новый гардероб с выбранной одеждой

### Интеграция с RPG Inventory
- **В гардеробе** — наведите, чтобы увидеть в каких гардеробах одежда
- **Добавить в гардероб** — добавить в любой гардероб из контекстного меню инвентаря
- **Удалить из текущего гардероба** — удалить из текущего гардероба колониста

### Общее
- Поддержка Combat Extended ExtendedLoadout (PersonalLoadout)
- Тултипы показывают содержимое лоадаута/гардероба при наведении
- Колонисты отсортированы по порядку на панели
- Локализация: русский и английский

## Как использовать

1. Левый клик по предмету на карте — выделить
2. Правый клик — контекстное меню
3. Оружие/инструменты: управление лоадаутами. Одежда: управление гардеробами.

## Требования

- RimWorld 1.5
- [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended)
- [Harmony](https://github.com/pardeike/HarmonyRimWorld)
- Модпак Hardcore SK

## Сборка

```bash
dotnet build Source/CEQuickLoadout/CEQuickLoadout.csproj -c Release
```
