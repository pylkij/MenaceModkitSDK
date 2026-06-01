# Vehicle API Reference

`Vehicle` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's vehicle systems and exposes safe access to vehicle health, armor, modular equipment slots, and twin-fire detection. Call these methods any time after `GameState.SceneLoaded` has fired — field handles are resolved automatically on first scene load.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetVehicleInfo(entity)` | `VehicleInfo` | Queries |
| `GetSlotInfo(slotObj)` | `SlotInfo` | Queries |
| `IsVehicle(entity)` | `bool` | Checks |
| `SetHitpointsPct(entity, value)` | `void` | Write |
| `SetArmorDurabilityPct(entity, value)` | `void` | Write |
| `HealAndClearDamageEffects(entity)` | `void` | Write |
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
        // Vehicle resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately
        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull) return;

        if (!Vehicle.IsVehicle(actor)) return;

        var info = Vehicle.GetVehicleInfo(actor);
        if (info != null)
            SdkLogger.Msg($"Vehicle: {info.TemplateId}, HP: {info.BaseHp}/{info.MaxHp}, Twin-Fire: {info.HasTwinFire}");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `VehicleInfo`

A snapshot of a vehicle entity's full state, returned by `GetVehicleInfo()`.

```csharp
public class VehicleInfo
{
    public string TemplateId { get; set; }
    public float HitpointsPct { get; set; }
    public float ArmorDurabilityPct { get; set; }
    public int BaseHp { get; set; }
    public int MaxHp { get; set; }
    public int Armor { get; set; }
    public int EquippedSlots { get; set; }
    public bool HasTwinFire { get; set; }
    public List<SlotInfo> Slots { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateId` | `string` | Stable `m_ID` from the entity's `EntityTemplate`. Use this for all template lookups — never display names. |
| `HitpointsPct` | `float` | Current hitpoints as a fraction of maximum (0.0–1.0). |
| `ArmorDurabilityPct` | `float` | Current armor durability as a fraction of maximum (0.0–1.0). |
| `BaseHp` | `int` | Base hitpoint value before modifiers. |
| `MaxHp` | `int` | Maximum hitpoint value. |
| `Armor` | `int` | Current armor rating. |
| `EquippedSlots` | `int` | Number of modular slots that have a weapon mounted. |
| `HasTwinFire` | `bool` | Whether twin-fire is currently active for this vehicle. |
| `Slots` | `List<SlotInfo>` | All modular slots on this vehicle. Empty if the vehicle has no modular system. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying entity object. |

---

### `SlotInfo`

Describes a single modular equipment slot, returned as part of `VehicleInfo.Slots`.

```csharp
public class SlotInfo
{
    public Il2CppMenace.Strategy.ModularVehicleSlotType SlotType { get; set; }
    public string EquippedItemId { get; set; }
    public bool HasItem { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `SlotType` | `ModularVehicleSlotType` | The slot category: `Light`, `Medium`, or `Heavy`. |
| `EquippedItemId` | `string` | Stable `m_ID` of the mounted weapon's template. `null` if the slot is empty. |
| `HasItem` | `bool` | `true` if a weapon is currently mounted in this slot. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying slot object. |

---

## Method Reference

### Queries

#### `GetVehicleInfo(entity)`

Aggregates all vehicle state into a single `VehicleInfo` snapshot. Reads health and armor fields via pre-resolved field handles, invokes `GetBaseHp`, `GetBaseMaxHp`, and `GetArmor` via `GameMethod`, and populates modular slot data by traversing the entity's `ItemContainer`.

```csharp
VehicleInfo GetVehicleInfo(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The vehicle entity to query. |

Returns `null` if the handle is null or the entity cannot be wrapped as a `Vehicle`. Returns a `VehicleInfo` with an empty `Slots` list if the entity has no modular vehicle system.

---

#### `GetSlotInfo(slotObj)`

Reads the slot type and mounted weapon from a single modular slot object. Called internally by `GetVehicleInfo` for each slot in the vehicle's modular system. Exposed publicly for callers that already hold a typed slot handle.

```csharp
SlotInfo GetSlotInfo(GameObj<Il2CppMenace.Strategy.ItemsModularVehicle.Slot> slotObj)
```

| Parameter | Type | Description |
|---|---|---|
| `slotObj` | `GameObj<ItemsModularVehicle.Slot>` | The typed slot handle to query. |

Returns a `SlotInfo` with `HasItem = false` and `EquippedItemId = null` if no weapon is mounted.

---

### Checks

#### `IsVehicle(entity)`

Checks whether an entity is a vehicle by invoking `Entity.IsVehicle()` via `GameMethod`.

```csharp
bool IsVehicle(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to check. |

Returns `false` if the handle is null or the call fails.

> **Note:** Always call `IsVehicle` before `GetVehicleInfo` when iterating over heterogeneous entity collections. `GetVehicleInfo` will return `null` for non-vehicles, but the `IsVehicle` guard makes intent explicit and avoids unnecessary work.

---

### Write Operations

All write methods perform an `IsVehicle` guard before invoking the underlying game method. They are no-ops if the entity handle is null or the entity is not a vehicle.

#### `SetHitpointsPct(entity, value)`

Sets the vehicle's hitpoints as a percentage of maximum.

```csharp
void SetHitpointsPct(GameObj entity, float value)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The vehicle entity to modify. |
| `value` | `float` | Percentage value between 0.0 and 1.0. |

---

#### `SetArmorDurabilityPct(entity, value)`

Sets the vehicle's armor durability as a percentage of maximum.

```csharp
void SetArmorDurabilityPct(GameObj entity, float value)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The vehicle entity to modify. |
| `value` | `float` | Percentage value between 0.0 and 1.0. |

---

#### `HealAndClearDamageEffects(entity)`

Fully restores the vehicle's hitpoints and clears all active damage effects (e.g. fire, oil leak).

```csharp
void HealAndClearDamageEffects(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The vehicle entity to heal. |

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `vehicle` | *(none)* | Print full `VehicleInfo` for the currently selected actor. |
| `twinfire` | *(none)* | Print twin-fire status and equipped slot count for the selected actor. |

Example session:

```
> vehicle
Vehicle: vehicle_tank_t55
HP: 18/18 (100%)
Armor: 12 (Durability: 100%)
Equipped Slots: 2
Twin-Fire: False
Slots:
  [Medium] weapon_main_gun_100mm
  [Light] weapon_mg_coaxial

> twinfire
Twin-Fire Active: False
Equipped Slots: 2
```

---

## Error Handling

Write methods (SetHitpointsPct, SetArmorDurabilityPct, HealAndClearDamageEffects) may throw GameObjException if called with a null pointer or zero field offset, consistent with the SDK write contract. Failures inside SDK infrastructure are reported via SdkLogger.

| Method | Fallback on error |
|---|---|
| `GetVehicleInfo` | `null` |
| `GetSlotInfo` | `SlotInfo` with defaults |
| `IsVehicle` | `false` |