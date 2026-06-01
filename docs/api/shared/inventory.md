# Inventory API Reference

`Inventory` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's item and inventory systems, exposing safe access to item containers, item metadata, equipment queries, tag filtering, item mutation (add, remove, transfer), and spawning. Call these methods any time after `GameState.SceneLoaded` has fired — field handles are resolved automatically on first scene load.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetOwnedItems()` | `GameObj<OwnedItems>` | Global State |
| `GetContainer(entity)` | `GameObj<ItemContainer>` | Queries |
| `GetContainerInfo(container)` | `ContainerInfo` | Queries |
| `GetAllItems(container)` | `List<ItemInfo>` | Queries |
| `GetItemsInSlot(container, slotType)` | `List<ItemInfo>` | Queries |
| `GetItemAt(container, slotType, index)` | `GameObj<Item>` | Queries |
| `GetItemInfo(item)` | `ItemInfo` | Queries |
| `GetEquippedWeapons(entity)` | `List<ItemInfo>` | Queries |
| `GetEquippedArmor(entity)` | `ItemInfo` | Queries |
| `GetTotalTradeValue(container)` | `int` | Queries |
| `HasItemWithTag(container, tagType)` | `bool` | Checks |
| `GetItemsWithTag(container, tagType)` | `List<ItemInfo>` | Checks |
| `RemoveItem(container, item)` | `bool` | Write |
| `RemoveItemAt(container, slotType, index)` | `bool` | Write |
| `TransferItem(from, to, item)` | `bool` | Write |
| `ClearInventory(container, slotType?)` | `int` | Write |
| `GiveItemToActor(templateId)` | `string` | Write |
| `SpawnItem(templateId)` | `string` | Write |
| `RegisterConsoleCommands()` | `void` | Dev Tools |

---

## Quick Start

```csharp
using MelonLoader;
using Menace.SDK;

namespace MyPlugin;

public class MyPlugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        // Inventory resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Title") return;

        // Field handles are already resolved — safe to call immediately.
        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull) return;

        if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actor, out var entity)) return;

        var container = Inventory.GetContainer(entity);
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return;

        var items = Inventory.GetAllItems(container);
        SdkLogger.Msg($"Actor has {items.Count} items. Total value: ${Inventory.GetTotalTradeValue(container)}");

        var weapons = Inventory.GetEquippedWeapons(entity);
        foreach (var w in weapons)
            SdkLogger.Msg($"  Weapon: {w.TemplateName} (Rarity: {w.RarityTier}, Skills: {w.SkillCount})");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `ItemInfo`

A snapshot of a single item's state, returned by query methods.

```csharp
public class ItemInfo
{
    public string GUID { get; set; }
    public string TemplateName { get; set; }
    public ItemSlot SlotType { get; set; }
    public int TradeValue { get; set; }
    public int RarityTier { get; set; }
    public int SkillCount { get; set; }
    public bool IsTemporary { get; set; }
    public GameObj<Il2CppMenace.Items.Item> Item { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `GUID` | `string` | Unique runtime identifier for this item instance. Not stable across sessions. |
| `TemplateName` | `string` | The Unity asset name from the item's `ItemTemplate`. Use `TemplateName` for display and `ItemSlot` for logic — do not use as a stable template ID. |
| `SlotType` | `ItemSlot` | The equipment slot this item occupies. See [`ItemSlot`](#itemslot) below. |
| `TradeValue` | `int` | Base trade value from the item's template. |
| `RarityTier` | `int` | Rarity level from the item's template. Higher values indicate rarer items. |
| `SkillCount` | `int` | Number of skills attached to this item instance. |
| `IsTemporary` | `bool` | Whether this item is flagged as temporary (e.g. a scripted or event-granted item). |
| `Item` | `GameObj<Item>` | Typed handle to the underlying item object. Use this to pass the item to write methods. |

---

### `ContainerInfo`

A snapshot of a container's composition, returned by `GetContainerInfo()`.

```csharp
public class ContainerInfo
{
    public int TotalItems { get; set; }
    public Dictionary<ItemSlot, int> SlotCounts { get; set; }
    public bool HasModularVehicle { get; set; }
    public GameObj<Il2CppMenace.Items.ItemContainer> Container { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TotalItems` | `int` | Total number of items across all slot types. |
| `SlotCounts` | `Dictionary<ItemSlot, int>` | Per-slot item counts. All valid `ItemSlot` values (excluding `None`, `All`, `COUNT`) are present as keys. |
| `HasModularVehicle` | `bool` | Whether this container is attached to an entity with a modular vehicle system. |
| `Container` | `GameObj<ItemContainer>` | Typed handle to the underlying container object. |

---

### `ItemSlot`

Defines which equipment slot an item occupies.

| Value | Int | Description |
|---|---|---|
| `None` | -1 | Sentinel / unfiltered. Do not pass to slot-specific methods. |
| `InfantryWeapon` | 0 | Primary infantry weapon slot. |
| `InfantrySpecial` | 1 | Secondary / special infantry weapon slot. |
| `InfantryArmor` | 2 | Infantry armor slot. |
| `InfantryAccessory` | 3 | Infantry accessory slot. |
| `Vehicle` | 4 | Vehicle equipment slot. |
| `VehicleAccessory` | 5 | Vehicle accessory slot. |
| `VehicleLightTurret` | 6 | Vehicle light turret slot. |
| `VehicleHeavyTurret` | 7 | Vehicle heavy turret slot. |
| `ModularVehicleLight` | 8 | Modular vehicle light weapon slot. |
| `ModularVehicleMedium` | 9 | Modular vehicle medium weapon slot. |
| `ModularVehicleHeavy` | 10 | Modular vehicle heavy weapon slot. |
| `COUNT` | 11 | Sentinel / enum boundary. Do not use. |
| `All` | 255 | Sentinel / wildcard. Do not pass to slot-specific methods. |

> **Note:** Methods that accept an `ItemSlot` parameter will return empty results or `false` when passed `None`, `All`, or `COUNT`.

---

### `TagType`

Describes a gameplay characteristic attached to an item's template. Used with `HasItemWithTag()` and `GetItemsWithTag()`.

| Value | Description |
|---|---|
| `SPECIAL_WEAPON` | Special-class weapon. |
| `HEAVY_ARMOR` | Heavy armor item. |
| `LIGHT_ARMOR` | Light armor item. |
| `WEAPON` | General weapon tag. |
| `VEHICLE_WEAPON` | Weapon intended for vehicle use. |
| `SQUAD_WEAPON` | Crew-served or squad-level weapon. |
| `INFANTRY` | Infantry-use item. |
| `VEHICLE` | Vehicle-use item. |
| `ACCESSORY` | Accessory item. |
| `UNIQUE` | Unique/one-of-a-kind item. |
| `COMMODITY` | Trade commodity. |
| `CRAFTING_MATERIAL` | Crafting ingredient. |
| `DRUG` | Drug/consumable item. |
| `DEPLOYABLE` | Can be deployed. |
| `UTILITY` | Utility item. |
| `GRENADE` | Grenade-type item. |
| `MINE` | Mine/trap item. |
| `ROCKET` | Rocket weapon. |
| `SMG` | Submachine gun. |
| `ASSAULT_RIFLE` | Assault rifle. |
| `BATTLE_RIFLE` | Battle rifle. |
| `SHOTGUN` | Shotgun. |
| `SNIPER` | Sniper weapon. |
| `PROJECTILE` | Fires physical projectiles. |
| `ENERGY` | Energy-based weapon. |
| `MACHINE` | Machine-type weapon. |
| `ANTI_INFANTRY` | Effective against infantry. |
| `ANTI_VEHICLE` | Effective against vehicles. |
| `ANTI_STRUCTURE` | Effective against structures. |
| `ARMOR_PIERCING` | Armor-piercing capability. |
| `ARMOR_DAMAGING` | Damages armor. |
| `AREA_OF_EFFECT` | Has area-of-effect damage. |
| `INCENDIARY` | Causes fire effects. |
| `EMP` | Electromagnetic pulse effect. |
| `INDIRECT_FIRE` | Fires in an arc (mortars, artillery). |
| `SUPPRESSIVE` | Suppression capability. |
| `DEMORALIZING` | Lowers morale. |
| `DISABLING` | Can disable targets. |
| `SCATTER` | Scatter-shot pattern. |
| `ACCURATE` | Higher-than-normal accuracy. |
| `INACCURATE` | Lower-than-normal accuracy. |
| `SHORT_RANGE` | Optimized for short range. |
| `LONG_RANGE` | Optimized for long range. |
| `MINIMUM_RANGE` | Has a minimum engagement range. |
| `HIGH_RATE_OF_FIRE` | High rate of fire. |
| `LOW_PENETRATION` | Low armor penetration. |
| `LIMITED_AMMO` | Limited ammunition supply. |
| `HORDE` | Effective against large groups. |
| `SMOKE` | Produces smoke. |
| `IGNORES_COVER` | Ignores cover bonuses. |
| `MASSIVE_EXPLOSION` | Very large explosion radius. |
| `HAZARDOUS` | Hazardous to the user or allies. |
| `LOW_QUALITY` | Below-standard quality item. |
| `FLEXIBLE` | Flexible/multi-role item. |
| `MOBILE` | Mobile or portable. |
| `LARGE` | Large item. |
| `MANY_ACCESSORIES` | Has many attachment points. |
| `CAMOUFLAGE` | Provides camouflage. |
| `STEALTH` | Stealth capability. |
| `SCANNER` | Scanning/detection capability. |
| `DEPLOY` | Requires deployment to use. |
| `STANCE` | Stance-related item. |
| `JAM` | Prone to jamming. |
| `STRUCTURE` | Structure-type entity tag. |
| `DESTRUCTIBLE` | Can be destroyed. |
| `TANK` | Tank-type vehicle. |
| `WALKER` | Walker-type vehicle. |
| `HOVERING` | Hovering vehicle. |
| `MOTORIZED_INFANTRY` | Motorized infantry unit. |
| `JETPACK` | Jetpack-equipped unit. |
| `XENO` | Xenomorph/alien entity. |
| `VIP` | VIP unit or item. |
| `MINI_BOSS` | Mini-boss entity. |
| `WEAPONS_TEAM` | Weapons team unit. |
| `PROJECTILE` | Projectile entity. |

---

## Method Reference

### Global State

#### `GetOwnedItems()`

Returns the global `OwnedItems` manager from `StrategyState`. This is the top-level inventory pool used on the strategy map for spawning and unassigned item storage.

```csharp
GameObj<Il2CppMenace.Strategy.OwnedItems> GetOwnedItems()
```

Returns `GameObj.Null` (default) when not on the strategy map or when `StrategyState` is unavailable. Always check `CheckAlive()` before using the returned handle.

> **Note:** `GetOwnedItems()` is only valid on the strategy map. It will return null during tactical combat or in menus.

---

### Queries

#### `GetContainer(entity)`

Returns the `ItemContainer` for a given entity.

```csharp
GameObj<Il2CppMenace.Items.ItemContainer> GetContainer(GameObj<Il2CppMenace.Tactical.Entity> entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj<Entity>` | The entity whose container to retrieve. |

Returns `default` if the entity handle is not alive or has no item container.

---

#### `GetContainerInfo(container)`

Aggregates slot-by-slot item counts and modular vehicle state into a single `ContainerInfo` snapshot.

```csharp
ContainerInfo GetContainerInfo(GameObj<Il2CppMenace.Items.ItemContainer> container)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to inspect. |

Returns `null` if the container handle is not alive.

---

#### `GetAllItems(container)`

Returns all items in a container across all slot types.

```csharp
List<ItemInfo> GetAllItems(GameObj<Il2CppMenace.Items.ItemContainer> container)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to query. |

Returns an empty list if the container is not alive or contains no items.

---

#### `GetItemsInSlot(container, slotType)`

Returns all items in a container that occupy a specific slot type.

```csharp
List<ItemInfo> GetItemsInSlot(GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to query. |
| `slotType` | `ItemSlot` | The slot type to filter by. Must not be `None`, `All`, or `COUNT`. |

Returns an empty list if the container is not alive, the slot type is invalid, or no items occupy that slot.

---

#### `GetItemAt(container, slotType, index)`

Returns the item at a specific slot and index within a container.

```csharp
GameObj<Il2CppMenace.Items.Item> GetItemAt(GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType, int index)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to query. |
| `slotType` | `ItemSlot` | The slot type to look in. Must not be `None`. |
| `index` | `int` | Zero-based index within the slot. |

Returns `default` if the container is not alive, the slot type is `None`, or the index is out of range.

---

#### `GetItemInfo(item)`

Reads metadata from a single item handle into an `ItemInfo` snapshot.

```csharp
ItemInfo GetItemInfo(GameObj<Il2CppMenace.Items.Item> item)
```

| Parameter | Type | Description |
|---|---|---|
| `item` | `GameObj<Item>` | The item handle to read. |

Returns `null` if the item handle is not alive or the underlying template cannot be read.

---

#### `GetEquippedWeapons(entity)`

Returns all items in an entity's `InfantryWeapon` and `InfantrySpecial` slots.

```csharp
List<ItemInfo> GetEquippedWeapons(GameObj<Il2CppMenace.Tactical.Entity> entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj<Entity>` | The entity to query. |

Returns an empty list if the entity has no container or no weapons equipped. This is a convenience wrapper over `GetItemsInSlot` — it does not filter by vehicle weapon slots. For vehicle weapons, query `ModularVehicleLight`, `ModularVehicleMedium`, and `ModularVehicleHeavy` directly.

---

#### `GetEquippedArmor(entity)`

Returns the first item in an entity's `InfantryArmor` slot.

```csharp
ItemInfo GetEquippedArmor(GameObj<Il2CppMenace.Tactical.Entity> entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj<Entity>` | The entity to query. |

Returns `null` if the entity has no container or no armor equipped.

---

#### `GetTotalTradeValue(container)`

Sums the trade value of all items in a container.

```csharp
int GetTotalTradeValue(GameObj<Il2CppMenace.Items.ItemContainer> container)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to total. |

Returns `0` if the container is empty or not alive.

---

### Checks

#### `HasItemWithTag(container, tagType)`

Returns whether a container holds at least one item whose template carries the given tag.

```csharp
bool HasItemWithTag(GameObj<Il2CppMenace.Items.ItemContainer> container, TagType tagType)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to check. |
| `tagType` | `TagType` | The tag to search for. See [`TagType`](#tagtype) above. |

Returns `false` if the container is not alive or no matching items are found.

---

#### `GetItemsWithTag(container, tagType)`

Returns all items in a container whose templates carry a given tag. Iterates all items and checks each template individually via `BaseItemTemplate.HasTag`.

```csharp
List<ItemInfo> GetItemsWithTag(GameObj<Il2CppMenace.Items.ItemContainer> container, TagType tagType)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to search. |
| `tagType` | `TagType` | The tag to filter by. See [`TagType`](#tagtype) above. |

Returns an empty list if the container is not alive or no items carry the tag.

---

### Write Operations

All write methods are no-ops if any required handle is null or not alive.

#### `RemoveItem(container, item)`

Removes a specific item from a container.

```csharp
bool RemoveItem(GameObj<Il2CppMenace.Items.ItemContainer> container, GameObj<Il2CppMenace.Items.Item> item)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to remove from. |
| `item` | `GameObj<Item>` | The item to remove. |

Returns `true` if the item was successfully removed.

---

#### `RemoveItemAt(container, slotType, index)`

Removes the item at a specific slot and index.

```csharp
bool RemoveItemAt(GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType, int index)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to remove from. |
| `slotType` | `ItemSlot` | The slot to remove from. Must not be `None`. |
| `index` | `int` | Zero-based index within the slot. |

Returns `true` if the item was successfully removed.

---

#### `TransferItem(from, to, item)`

Moves an item from one container to another. Removes from `from`, then places into `to`.

```csharp
bool TransferItem(
    GameObj<Il2CppMenace.Items.ItemContainer> from,
    GameObj<Il2CppMenace.Items.ItemContainer> to,
    GameObj<Il2CppMenace.Items.Item> item)
```

| Parameter | Type | Description |
|---|---|---|
| `from` | `GameObj<ItemContainer>` | The source container. |
| `to` | `GameObj<ItemContainer>` | The destination container. |
| `item` | `GameObj<Item>` | The item to transfer. |

Returns `true` only if both the removal from `from` and the placement into `to` succeed. Returns `false` without modifying either container if the removal fails.

> **Caveat:** Placement into the destination always passes index `0`. The game resolves the actual destination slot from the item's template type — the index is a hint, not a guarantee of position. Behaviour may be unpredictable for containers with complex slot configurations.

---

#### `ClearInventory(container, slotType?)`

Removes all items from a container, optionally limited to a specific slot type.

```csharp
int ClearInventory(GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType = ItemSlot.None)
```

| Parameter | Type | Description |
|---|---|---|
| `container` | `GameObj<ItemContainer>` | The container to clear. |
| `slotType` | `ItemSlot` | *(Optional)* If provided (and not `None`), only items in this slot are removed. Defaults to `None` (clears all slots). |

Returns the number of items successfully removed. Returns `0` if the container is not alive.

---

#### `GiveItemToActor(templateId)`

Creates an item from a template and places it directly into the currently selected actor's container. Intended for use during tactical mode.

```csharp
string GiveItemToActor(string templateId)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | Stable template ID string (e.g. `"weapon.laser_smg"`). |

Returns a human-readable status string describing the result. Check the returned string to confirm success — it will include the actor name on success, or a specific failure reason (no actor selected, template not found, no container, etc.) on failure.

> **Note:** Requires an active actor to be selected via `TacticalController`. Use `SpawnItem` instead when on the strategy map.

---

#### `SpawnItem(templateId)`

Creates an item from a template and adds it to the global `OwnedItems` pool. Strategy map only.

```csharp
string SpawnItem(string templateId)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | Stable template ID string (e.g. `"weapon.laser_smg"`). |

Returns a human-readable status string describing the result. Check the returned string to confirm success — it will include the spawned item's ID on success, or a specific failure reason (template not found, `OwnedItems` unavailable, etc.) on failure.

> **Note:** Only valid on the strategy map. Returns an error string if called during tactical combat or while `OwnedItems` is unavailable.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `inventory` | *(none)* | List all items in the selected actor's inventory with slot type and trade value. |
| `weapons` | *(none)* | List equipped weapons for the selected actor with rarity and skill count. |
| `armor` | *(none)* | Show equipped armor details for the selected actor. |
| `slot` | `<type>` | List items in a specific slot type by name (e.g. `slot InfantryWeapon`). |
| `itemvalue` | *(none)* | Print the total trade value of the selected actor's inventory. |
| `spawn` | `<template>` | Spawn an item by template ID into `OwnedItems`. Strategy map only. |
| `give` | `<template>` | Give an item by template ID directly to the selected actor. Tactical mode. |
| `spawnlist` | `[filter]` | List all item templates, optionally filtered by a substring. Returns up to 50 results. |
| `spawninfo` | *(none)* | Print spawn system diagnostics: handle resolution state, `OwnedItems` availability, and total template count. |
| `hastag` | `<tag>` | Check whether the selected actor's inventory contains any item with the given `TagType`. |

Example session:

```
> inventory
Inventory (3 items):
  [InfantryWeapon] weapon.laser_smg ($420)
  [InfantryArmor] armor.flak_vest ($180)
  [InfantrySpecial] grenade.frag ($60) [TEMP]

> weapons
Equipped Weapons:
  weapon.laser_smg (Rarity: 2) - 3 skills

> slot InfantryArmor
InfantryArmor (1 items):
  armor.flak_vest ($180)

> itemvalue
Total Trade Value: $660 (3 items)

> spawnlist weapon
Item Templates (12):
  weapon.laser_smg
  weapon.laser_rifle
  weapon.plasma_cannon
  ...

> spawn weapon.laser_smg
Spawned: weapon.laser_smg (ID: weapon.laser_smg_instance_7f3a)

> give weapon.laser_smg
Gave weapon.laser_smg to Sgt. Torres

> hastag ARMOR_PIERCING
Has tag 'ARMOR_PIERCING': Yes (1 items)

> spawninfo
Spawn System Info:
  Handles resolved: True
  OwnedItems: Available
  ItemTemplate count: 147
```

---

## Error Handling

Query methods return `null` or empty collections on failure. Write methods return `false` or `0`. Spawn and give methods return a descriptive string. All internal failures are reported via `SdkLogger`.

| Method | Fallback on error |
|---|---|
| `GetOwnedItems` | `default` (null handle) |
| `GetContainer` | `default` (null handle) |
| `GetContainerInfo` | `null` |
| `GetAllItems` | Empty `List<ItemInfo>` |
| `GetItemsInSlot` | Empty `List<ItemInfo>` |
| `GetItemAt` | `default` (null handle) |
| `GetItemInfo` | `null` |
| `GetEquippedWeapons` | Empty `List<ItemInfo>` |
| `GetEquippedArmor` | `null` |
| `GetTotalTradeValue` | `0` |
| `HasItemWithTag` | `false` |
| `GetItemsWithTag` | Empty `List<ItemInfo>` |
| `RemoveItem` | `false` |
| `RemoveItemAt` | `false` |
| `TransferItem` | `false` |
| `ClearInventory` | `0` |
| `GiveItemToActor` | Descriptive error string |
| `SpawnItem` | Descriptive error string |