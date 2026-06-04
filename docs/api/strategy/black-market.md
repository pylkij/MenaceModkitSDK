# BlackMarket API Reference

`BlackMarket` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's black market shop system and exposes safe access to item stacks, item instances, shop configuration, and stock management. Field handles are resolved automatically on each scene load — no setup is required beyond calling `Initialize()` once at mod startup.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetBlackMarket()` | `GameObj<BlackMarket>` | Queries |
| `GetBlackMarketInfo()` | `BlackMarketInfo` | Queries |
| `GetAvailableStacks()` | `List<ItemStackInfo>` | Queries |
| `GetStackInfo(index)` | `ItemStackInfo` | Queries |
| `GetStackAt(index)` | `GameObj<BlackMarketItemStack>` | Queries |
| `GetItemsInStack(stack)` | `List<ItemInfo>` | Queries |
| `FindStackByTemplateId(templateId)` | `ItemStackInfo` | Queries |
| `HasTemplate(templateId)` | `bool` | Status Checks |
| `GetStackCount()` | `int` | Status Checks |
| `GetExpiringStacks()` | `List<ItemStackInfo>` | Status Checks |
| `GetPermanentStacks()` | `List<ItemStackInfo>` | Status Checks |
| `GetStacksByType(type)` | `List<ItemStackInfo>` | Status Checks |
| `GetTotalTradeValue()` | `int` | Status Checks |
| `StockItemInBlackMarket(templateId)` | `string` | Write |
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
        BlackMarket.Initialize();
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately.
        var bm = BlackMarket.GetBlackMarket();
        if (bm.Untyped.IsNull) return;

        var info = BlackMarket.GetBlackMarketInfo();
        if (info != null)
            SdkLogger.Msg($"BlackMarket: {info.StackCount} stacks, {info.TotalItemCount} items, pool size {info.ItemPoolSize}");

        var stacks = BlackMarket.GetAvailableStacks();
        foreach (var stack in stacks)
        {
            var expiry = stack.CanTimeout ? $" ({stack.RemainingTimeout} ops)" : " [PERM]";
            SdkLogger.Msg($"  [{stack.Type}] {stack.TemplateID} x{stack.ItemCount} - ${stack.TradeValue}{expiry}");
        }
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Enums

### `StackType`

Represents the category of an item stack in the shop.

| Value | Integer | Description |
|---|---|---|
| `None` | `0` | No stack type assigned. |
| `Base` | `1` | Permanent base item that never expires. |
| `Regular` | `2` | Regular generated shop item. |
| `Tagged` | `3` | Item generated for a specific tag requirement. |
| `SpecialOffer` | `4` | Special offer item (`IsSpecialOffer` returns `true` when type is `SpecialOffer`). |

---

## Data Types

### `BlackMarketInfo`

A snapshot of the shop's current state and configuration, returned by `GetBlackMarketInfo()`.

```csharp
public class BlackMarketInfo
{
    public int StackCount { get; set; }
    public int TotalItemCount { get; set; }
    public Vector2Int RegularItemCount { get; set; }
    public Vector2Int ItemTimeout { get; set; }
    public int ItemPoolSize { get; set; }
    public float CampaignProgress { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `StackCount` | `int` | Number of item stacks currently available in the shop. |
| `TotalItemCount` | `int` | Total number of individual items across all stacks. |
| `RegularItemCount` | `Vector2Int` | Minimum and maximum regular items generated per FillUp (`x` = min, `y` = max). |
| `ItemTimeout` | `Vector2Int` | Minimum and maximum operations before item removal (`x` = min, `y` = max). |
| `ItemPoolSize` | `int` | Number of templates available in the base item pool. |
| `CampaignProgress` | `float` | Current campaign progress (`0.0`–`1.0`) affecting item generation. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying `BlackMarket` object. |

---

### `ItemStackInfo`

Describes a single purchasable item entry in the shop, returned as part of the list from `GetAvailableStacks()` or individually from `GetStackInfo()` and `FindStackByTemplateId()`.

```csharp
public class ItemStackInfo
{
    public string TemplateID { get; set; }
    public int RemainingTimeout { get; set; }
    public int ItemCount { get; set; }
    public StackType Type { get; set; }
    public int Rarity { get; set; }
    public int TradeValue { get; set; }
    public bool CanTimeout { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateID` | `string` | Stable `m_ID` from the item's `DataTemplate`. Use this for all template lookups — never display names. `null` if the field could not be read. |
| `RemainingTimeout` | `int` | Operations remaining before this stack is removed from the shop. |
| `ItemCount` | `int` | Number of item instances available in this stack. |
| `Type` | `StackType` | Category of this stack. |
| `Rarity` | `int` | Rarity value (`0`–`100`) of the item template. |
| `TradeValue` | `int` | Base trade value of the item template. |
| `CanTimeout` | `bool` | Whether this stack will eventually expire, as determined by `CanTimeout()` on the native stack object. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying `BlackMarketItemStack` object. |

---

### `ItemInfo`

Describes a single item instance within a stack, returned as part of the list from `GetItemsInStack()`.

```csharp
public class ItemInfo
{
    public string TemplateID { get; set; }
    public int Rarity { get; set; }
    public int TradeValue { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateID` | `string` | Stable `m_ID` from the item's `DataTemplate`. `null` if the field could not be read. |
| `Rarity` | `int` | Rarity value (`0`–`100`) of the item template. |
| `TradeValue` | `int` | Base trade value of the item template. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying `BaseItem` object. |

---

## Method Reference

### Queries

#### `GetBlackMarket()`

Retrieves the active `BlackMarket` instance from `StrategyState`.

```csharp
GameObj<Il2CppMenace.Strategy.BlackMarket> GetBlackMarket()
```

Returns a default (empty) `GameObj<BlackMarket>` with `Untyped.IsNull == true` if the strategy layer is not active or `StrategyState` is unavailable.

> **Note:** The typed wrapper is returned directly. Callers who need to check availability should test `bm.Untyped.IsNull`. Pass the result directly into other `BlackMarket` methods rather than inspecting the pointer yourself.

---

#### `GetBlackMarketInfo()`

Aggregates current shop state and configuration into a single `BlackMarketInfo` snapshot. Reads stack counts, item totals, config ranges, item pool size, and campaign progress.

```csharp
BlackMarketInfo GetBlackMarketInfo()
```

Returns `null` if the `BlackMarket` instance is unavailable.

---

#### `GetAvailableStacks()`

Returns a list of `ItemStackInfo` snapshots for all stacks currently in the shop.

```csharp
List<ItemStackInfo> GetAvailableStacks()
```

Returns an empty list if the `BlackMarket` instance is unavailable. Unreadable individual stacks are skipped silently.

---

#### `GetStackInfo(index)`

Returns a full `ItemStackInfo` snapshot for the stack at the specified index.

```csharp
ItemStackInfo GetStackInfo(int index)
```

| Parameter | Type | Description |
|---|---|---|
| `index` | `int` | Zero-based index into the shop's stack list. |

Returns `null` if the index is out of range or the `BlackMarket` is unavailable.

---

#### `GetStackAt(index)`

Returns the raw typed wrapper for the stack at the specified index, without building a full `ItemStackInfo`. Use this when you need to pass the stack handle to another SDK method rather than inspect its data.

```csharp
GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack> GetStackAt(int index)
```

| Parameter | Type | Description |
|---|---|---|
| `index` | `int` | Zero-based index into the shop's stack list. |

Returns a default (empty) `GameObj<BlackMarketItemStack>` if the index is out of range or the `BlackMarket` is unavailable.

---

#### `GetItemsInStack(stack)`

Returns a list of `ItemInfo` snapshots for all item instances within the given stack.

```csharp
List<ItemInfo> GetItemsInStack(GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack> stack)
```

| Parameter | Type | Description |
|---|---|---|
| `stack` | `GameObj<BlackMarketItemStack>` | The stack to inspect. Obtain from `GetStackAt()`. |

Returns an empty list if the stack is null or has no instances. Null individual items are skipped silently.

---

#### `FindStackByTemplateId(templateId)`

Searches all available stacks for one whose item template matches the given `m_ID`. Comparison is case-insensitive.

```csharp
ItemStackInfo FindStackByTemplateId(string templateId)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | The stable `m_ID` of the item template to search for. |

Returns the first matching `ItemStackInfo`, or `null` if no match is found or `templateId` is null or empty.

---

### Status Checks

#### `HasTemplate(templateId)`

Returns `true` if the shop currently contains at least one stack whose item template matches the given `m_ID`.

```csharp
bool HasTemplate(string templateId)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | The stable `m_ID` of the item template to check for. |

---

#### `GetStackCount()`

Returns the total number of stacks currently in the shop.

```csharp
int GetStackCount()
```

Returns `0` if the `BlackMarket` is unavailable.

---

#### `GetExpiringStacks()`

Returns all stacks with exactly one operation remaining before removal.

```csharp
List<ItemStackInfo> GetExpiringStacks()
```

Only stacks for which `CanTimeout()` returns `true` are included. Returns an empty list if the `BlackMarket` is unavailable or no stacks are about to expire.

---

#### `GetPermanentStacks()`

Returns all stacks that will never expire.

```csharp
List<ItemStackInfo> GetPermanentStacks()
```

Only stacks for which `CanTimeout()` returns `false` are included. Returns an empty list if the `BlackMarket` is unavailable or no permanent stacks exist.

---

#### `GetStacksByType(type)`

Returns all stacks matching the given `StackType`.

```csharp
List<ItemStackInfo> GetStacksByType(StackType type)
```

| Parameter | Type | Description |
|---|---|---|
| `type` | `StackType` | The stack category to filter by. |

Returns an empty list if the `BlackMarket` is unavailable or no stacks of the given type exist.

---

#### `GetTotalTradeValue()`

Returns the sum of trade values across all stacks, weighted by the number of instances in each stack.

```csharp
int GetTotalTradeValue()
```

Returns `0` if the `BlackMarket` is unavailable.

---

### Write Operations

#### `StockItemInBlackMarket(templateId)`

Creates an item from the given template and adds it to the `BlackMarket` via `AddItem`. Intended for testing and dev tooling — not for use in production mod logic.

```csharp
string StockItemInBlackMarket(string templateId)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | The stable `m_ID` of the `ItemTemplate` to stock. |

Returns a human-readable result string describing success or the reason for failure. Will fail if the strategy layer is not active, the `BlackMarket` object is no longer alive, or the template is not found.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `blackmarket` | *(none)* | Print shop overview: stack count, item totals, config ranges, item pool size, and campaign progress. |
| `bmdebug` | *(none)* | Print raw diagnostic state: `StrategyState`, `BlackMarket` pointer, stack count, `StrategyConfig`, `BlackMarketConfig` pointer, and handle resolution status. |
| `bmitems` | *(none)* | List all stacks with index, template ID, item count, trade value, expiry, and type tag. |
| `bmstack` | `<index>` | Print full details for the stack at the given zero-based index. |
| `bmexpiring` | *(none)* | List all stacks with exactly one operation remaining. |
| `bmpermanent` | *(none)* | List all stacks that never expire. |
| `bmfind` | `<id>` | Search for stacks whose template ID contains the given substring (case-insensitive). |
| `bmstock` | `<template_id>` | Stock an item by template ID. For testing only. |
| `bmvalue` | *(none)* | Print total trade value across all stacks and the current stack count. |
| `bmbytype` | `<type>` | Filter stacks by type. Accepts type name (`None`, `Base`, `Regular`, `Tagged`, `SpecialOffer`) or integer (`0`–`4`). |

Example session:

```
> blackmarket
BlackMarket Status:
  Stacks: 6 (14 total items)
  Config: 3-5 items, 2-4 ops timeout
  Item Pool: 42 templates
  Campaign Progress: 35%

> bmitems
BlackMarket Items (6 stacks):
  0. weapon.laser_smg x2 - $320 (3 ops) [Regular]
  1. armour.light_vest x1 - $180 (2 ops) [Regular]
  2. grenade.frag x3 - $90 [PERM] [Base]
  3. medkit.standard x2 - $60 (1 ops) [Regular]
  4. weapon.sniper_rifle x1 - $540 (4 ops) [SpecialOffer]
  5. ammo.heavy x4 - $45 [PERM] [Base]

> bmexpiring
Expiring Items (1):
  medkit.standard x2 - $60 (1 op left)
These items will be removed after the next operation!

> bmfind laser
Found 1 matching items:
  weapon.laser_smg x2 - $320 (3 ops)

> bmstock weapon.laser_smg
Stocked 'weapon.laser_smg' in BlackMarket
```

---

## Error Handling

All methods handle failures internally and report them via `SdkLogger`. No method propagates exceptions to the caller.

| Method | Fallback on error |
|---|---|
| `GetBlackMarket` | Default (empty) `GameObj<BlackMarket>` |
| `GetBlackMarketInfo` | `null` |
| `GetAvailableStacks` | Empty `List<ItemStackInfo>` |
| `GetStackInfo` | `null` |
| `GetStackAt` | Default (empty) `GameObj<BlackMarketItemStack>` |
| `GetItemsInStack` | Empty `List<ItemInfo>` |
| `FindStackByTemplateId` | `null` |
| `HasTemplate` | `false` |
| `GetStackCount` | `0` |
| `GetExpiringStacks` | Empty `List<ItemStackInfo>` |
| `GetPermanentStacks` | Empty `List<ItemStackInfo>` |
| `GetStacksByType` | Empty `List<ItemStackInfo>` |
| `GetTotalTradeValue` | `0` |
| `StockItemInBlackMarket` | Human-readable error string |