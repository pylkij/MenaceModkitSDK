# OCI API Reference

`OCI` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's Orbital Command Interface (ship upgrade system) and exposes safe access to upgrade templates, upgrade slots, OCI component counts, and upgrade installation. Call these methods any time after `GameState.SceneLoaded` has fired — field handles are resolved automatically on first scene load.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetShipUpgrades()` | `GameObj` | Core Accessors |
| `GetOciComponents()` | `int` | Core Accessors |
| `GetAllUpgradeTemplates()` | `List<UpgradeInfo>` | Upgrade Queries |
| `GetInstalledUpgrades()` | `List<UpgradeInfo>` | Upgrade Queries |
| `GetAvailableUpgrades()` | `List<UpgradeInfo>` | Upgrade Queries |
| `GetUpgradeInfo(template)` | `UpgradeInfo` | Upgrade Queries |
| `GetSlots()` | `List<SlotInfo>` | Slot Queries |
| `GetSlotInfo(slot, slotIndex, equippedArr)` | `SlotInfo` | Slot Queries |
| `TryEquipUpgrade(upgrade, paidOciComponents, slotIdx, checkUnlocked)` | `bool` | Write |
| `TryUnequipUpgrade(upgrade)` | `bool` | Write |
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
        // OCI resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately.
        var components = OCI.GetOciComponents();
        SdkLogger.Msg($"OCI Components available: {components}");

        var installed = OCI.GetInstalledUpgrades();
        SdkLogger.Msg($"Installed upgrades ({installed.Count}):");
        foreach (var u in installed)
            SdkLogger.Msg($"  [{u.UpgradeType}] {u.TemplateId} ({u.OciPointsCost} pts)");

        var slots = OCI.GetSlots();
        SdkLogger.Msg($"Slots ({slots.Count}):");
        foreach (var s in slots)
        {
            var equipped = s.EquippedUpgrade != null ? s.EquippedUpgrade.TemplateId : "(empty)";
            SdkLogger.Msg($"  [{s.SlotType}] {s.TemplateId} → {equipped}");
        }
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Enums

### `ShipUpgradeType`

The category of an upgrade or slot, determining which slots an upgrade can occupy.

| Value | Integer | Description |
|---|---|---|
| `Armament` | `0` | Weapon and offensive systems upgrade. |
| `Electronics` | `1` | Sensor and electronic warfare upgrade. |
| `Hull` | `2` | Structural and defensive upgrade. |
| `Hidden` | `3` | Upgrade not displayed in standard UI. |

---

### `ShipUpgradeUnlockType`

Describes the condition under which an upgrade becomes available for installation.

| Value | Integer | Description |
|---|---|---|
| `Always` | `0` | The upgrade is always available regardless of campaign state. |
| `Faction` | `1` | The upgrade requires alignment with a specific faction. See `UnlockedByFaction`. |
| `EventOnly` | `2` | The upgrade is only made available through a specific in-game event. |

---

### `StoryFactionType`

Identifies a story faction. Used on upgrades with `UnlockType == Faction` to indicate which faction must be aligned with for the upgrade to become available.

| Value | Integer | Description |
|---|---|---|
| `Jingwei` | `0` | The Jingwei faction. |
| `Unbent` | `1` | The Unbent faction. |
| `Dice` | `2` | The Dice faction. |
| `Tolimen` | `3` | The Tolimen faction. |
| `Lurchen` | `4` | The Lurchen faction. |
| `Firan` | `5` | The Firan faction. |
| `CMC` | `6` | The CMC faction. |
| `ZBC` | `7` | The ZBC faction. |

> **Note:** `Last` is an alias for `ZBC` (`7`) used internally as a bounds marker. It is not a distinct faction and should not be used in plugin logic.

---

## Data Types

### `UpgradeInfo`

A snapshot of a ship upgrade template's state, returned by the upgrade query methods.

```csharp
public class UpgradeInfo
{
    public string TemplateId { get; set; }
    public ShipUpgradeType UpgradeType { get; set; }
    public ShipUpgradeUnlockType UnlockType { get; set; }
    public StoryFactionType UnlockedByFaction { get; set; }
    public int OciPointsCost { get; set; }
    public bool IsInstalled { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateId` | `string` | Stable `m_ID` from the upgrade's `ShipUpgradeTemplate`. Use this for all template lookups — never display names. |
| `UpgradeType` | `ShipUpgradeType` | The upgrade category, which determines which slot types it can occupy. |
| `UnlockType` | `ShipUpgradeUnlockType` | The condition under which this upgrade becomes available. |
| `UnlockedByFaction` | `StoryFactionType` | The faction required to unlock this upgrade. Only meaningful when `UnlockType` is `Faction`. |
| `OciPointsCost` | `int` | The number of OCI components required to install this upgrade. |
| `IsInstalled` | `bool` | `true` if this upgrade is currently equipped in a slot. Set by `GetInstalledUpgrades()` and `GetSlotInfo()`; `false` on results from `GetAllUpgradeTemplates()` and `GetAvailableUpgrades()`. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying upgrade template object. |

---

### `SlotInfo`

Describes a single upgrade slot on the ship, returned as part of the list from `GetSlots()`.

```csharp
public class SlotInfo
{
    public string TemplateId { get; set; }
    public ShipUpgradeType SlotType { get; set; }
    public UpgradeInfo EquippedUpgrade { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateId` | `string` | Stable `m_ID` from the slot's `ShipUpgradeSlotTemplate`. |
| `SlotType` | `ShipUpgradeType` | The upgrade category this slot accepts. |
| `EquippedUpgrade` | `UpgradeInfo` | The upgrade currently installed in this slot, with `IsInstalled` set to `true`. `null` if the slot is empty. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying slot template object. |

---

## Method Reference

### Core Accessors

#### `GetShipUpgrades()`

Retrieves the `ShipUpgrades` instance from `StrategyState`.

```csharp
GameObj GetShipUpgrades()
```

Returns `GameObj.Null` if `StrategyState` is unavailable or the `ShipUpgrades` pointer cannot be read.

> **Note:** This method returns a `GameObj` — a raw SDK handle — rather than a typed `Il2CppMenace.Strategy.ShipUpgrades`. Most plugin workflows should use the higher-level query methods instead. If you do work with this handle directly, be aware that the underlying access pattern may change in future SDK versions.

---

#### `GetOciComponents()`

Returns the number of OCI components currently available to spend.

```csharp
int GetOciComponents()
```

Returns `0` if `StrategyState` is unavailable or the value cannot be read.

---

### Upgrade Queries

#### `GetAllUpgradeTemplates()`

Returns `UpgradeInfo` snapshots for every `ShipUpgradeTemplate` registered in the game's template database, regardless of unlock state or installation status. `IsInstalled` is `false` on all returned entries.

```csharp
List<UpgradeInfo> GetAllUpgradeTemplates()
```

Returns an empty list if no templates are found or an error occurs.

---

#### `GetInstalledUpgrades()`

Returns `UpgradeInfo` snapshots for all upgrades currently equipped on the ship. `IsInstalled` is `true` on all returned entries.

```csharp
List<UpgradeInfo> GetInstalledUpgrades()
```

Returns an empty list if `ShipUpgrades` is unavailable or no upgrades are equipped.

---

#### `GetAvailableUpgrades()`

Returns `UpgradeInfo` snapshots for all upgrade templates that are currently unlocked (pass `IsUnlocked()`), regardless of whether they are already installed. `IsInstalled` is `false` on all returned entries.

```csharp
List<UpgradeInfo> GetAvailableUpgrades()
```

Returns an empty list if no unlocked templates are found or an error occurs.

---

#### `GetUpgradeInfo(template)`

Builds an `UpgradeInfo` snapshot from a raw upgrade template handle. Reads `TemplateId`, `UpgradeType`, `OciPointsCost`, `UnlockType`, and `UnlockedByFaction` via pre-resolved field handles. `IsInstalled` is not set by this method — it is the caller's responsibility to assign it if needed.

```csharp
UpgradeInfo GetUpgradeInfo(GameObj template)
```

| Parameter | Type | Description |
|---|---|---|
| `template` | `GameObj` | A handle to the `ShipUpgradeTemplate` to query. Must be alive. |

Returns `null` if `template` is not alive, cannot be wrapped as a `ShipUpgradeTemplate`, or an error occurs.

---

### Slot Queries

#### `GetSlots()`

Returns a `SlotInfo` snapshot for every upgrade slot on the ship, in slot-index order. Each entry includes the equipped upgrade if one is installed. Null or unreadable individual slots are skipped with a warning logged; all others are included.

```csharp
List<SlotInfo> GetSlots()
```

Returns an empty list if `ShipUpgrades` is unavailable, the slot array cannot be read, or an error occurs.

---

#### `GetSlotInfo(slot, slotIndex, equippedArr)`

Builds a `SlotInfo` snapshot for a single slot. Reads `TemplateId` and `SlotType` from the slot template handle, then cross-references `equippedArr` at `slotIndex` to populate `EquippedUpgrade`.

```csharp
SlotInfo GetSlotInfo(GameObj slot, int slotIndex, Il2CppReferenceArray<ShipUpgradeTemplate> equippedArr)
```

| Parameter | Type | Description |
|---|---|---|
| `slot` | `GameObj` | A handle to the `ShipUpgradeSlotTemplate` to query. Must be alive. |
| `slotIndex` | `int` | Zero-based index of this slot in the slot array. Used to look up the equipped upgrade in `equippedArr`. |
| `equippedArr` | `Il2CppReferenceArray<ShipUpgradeTemplate>` | The raw equipped upgrades array from `ShipUpgrades`. Pass `null` to skip equipped upgrade resolution; `EquippedUpgrade` will be `null` on the returned snapshot. |

Returns `null` if `slot` is not alive, cannot be wrapped as a `ShipUpgradeSlotTemplate`, or an error occurs.

> **Note:** This method is called internally by `GetSlots()` for each slot in the array. You can call it directly if you already hold a slot handle and the equipped array, but for most workflows `GetSlots()` is the appropriate entry point.

---

### Write Operations

Write methods are no-ops if `ShipUpgrades` is unavailable. Each logs an error at the point of failure.

#### `TryEquipUpgrade(upgrade, paidOciComponents, slotIdx, checkUnlocked)`

Attempts to install an upgrade into the specified slot by invoking the native `TryEquipUpgrade` on `ShipUpgrades`.

```csharp
bool TryEquipUpgrade(GameObj upgrade, int paidOciComponents, int slotIdx, bool checkUnlocked = true)
```

| Parameter | Type | Description |
|---|---|---|
| `upgrade` | `GameObj` | A handle to the `ShipUpgradeTemplate` to install. Must be alive. |
| `paidOciComponents` | `int` | The number of OCI components being paid for this installation. |
| `slotIdx` | `int` | Zero-based index of the target slot. Use `GetSlots()` to confirm the correct index. |
| `checkUnlocked` | `bool` | When `true` (default), the native method enforces unlock requirements before equipping. Pass `false` to bypass unlock checks. |

Returns `true` on success. Returns `false` without throwing if `upgrade` is not alive, either managed cast fails, or the native call fails.

---

#### `TryUnequipUpgrade(upgrade)`

Attempts to remove an upgrade from whichever slot it currently occupies by invoking the native `TryUnequipUpgrade` on `ShipUpgrades`.

```csharp
bool TryUnequipUpgrade(GameObj upgrade)
```

| Parameter | Type | Description |
|---|---|---|
| `upgrade` | `GameObj` | A handle to the `ShipUpgradeTemplate` to remove. Must be alive. |

Returns `true` on success. Returns `false` without throwing if `upgrade` is not alive, either managed cast fails, or the native call fails.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `oci` | *(none)* | Print available OCI components, all installed upgrades with type and cost, and all available upgrades with type and cost. |
| `ocislots` | *(none)* | List all upgrade slots with their type and currently equipped upgrade, if any. |
| `ociupgrades` | `[type]` | List all upgrade templates with type, cost, and unlock requirements. Optionally filter by a partial type name. |
| `equipoci` | `<id> <slot> <cost>` | Equip an upgrade by template ID into the given slot index, passing the specified OCI component cost. |

Example session:

```
> oci
OCI Components: 800
Installed Upgrades (1):
  [Armament] upgrade_railgun_mk2 (400 pts)
Available Upgrades (3):
  [Electronics] upgrade_ecm_suite (300 pts)
  [Hull] upgrade_reactive_armour (350 pts)
  [Armament] upgrade_missile_battery (450 pts)

> ocislots
OCI Slots (3):
  [Armament] slot_armament_primary → upgrade_railgun_mk2
  [Electronics] slot_electronics_primary (empty)
  [Hull] slot_hull_primary (empty)

> ociupgrades electronics
OCI Upgrades (1):
  [Electronics] upgrade_ecm_suite (300 pts)

> equipoci upgrade_ecm_suite 1 300
Equipped: upgrade_ecm_suite in slot 1
```

---

## Error Handling

All methods handle failures internally and report them via `SdkLogger`. No method propagates exceptions to the caller.

| Method | Fallback on error |
|---|---|
| `GetShipUpgrades` | `GameObj.Null` |
| `GetOciComponents` | `0` |
| `GetAllUpgradeTemplates` | Empty `List<UpgradeInfo>` |
| `GetInstalledUpgrades` | Empty `List<UpgradeInfo>` |
| `GetAvailableUpgrades` | Empty `List<UpgradeInfo>` |
| `GetUpgradeInfo` | `null` |
| `GetSlots` | Empty `List<SlotInfo>` (unreadable entries skipped) |
| `GetSlotInfo` | `null` |
| `TryEquipUpgrade` | `false` |
| `TryUnequipUpgrade` | `false` |