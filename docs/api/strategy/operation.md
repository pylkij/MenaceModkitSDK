# Operation API Reference

`Operation` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's campaign operation systems and exposes safe access to the active operation, its missions, faction assignments, planet, and time state. The game may run multiple operations simultaneously — the class distinguishes between the *current* operation (the one selected by `OperationsManager`) and the full list of *active* operations. Call these methods any time after `GameState.SceneLoaded` has fired — field handles are resolved automatically on first scene load.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetCurrentOperation()` | `GameObj` | Queries |
| `GetOperationInfo()` | `OperationInfo` | Queries |
| `GetOperationInfo(operation)` | `OperationInfo` | Queries |
| `GetCurrentMission()` | `GameObj` | Queries |
| `GetMissions()` | `List<GameObj>` | Queries |
| `GetAllOperations()` | `List<GameObj>` | Queries |
| `GetAllOperationInfo()` | `List<OperationInfo>` | Queries |
| `FindByFaction(factionId)` | `GameObj` | Queries |
| `FindByPlanet(planetId)` | `GameObj` | Queries |
| `GetCompletedOperationTypes()` | `List<string>` | Queries |
| `HasActiveOperation()` | `bool` | Checks |
| `HasCompletedOperationType(operationTemplateId)` | `bool` | Checks |
| `CanTimeOut()` | `bool` | Checks |
| `GetRemainingTime()` | `int` | Time |
| `GetOperationsManager()` | `GameObj` | Low-Level |
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
        // Operation resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately.
        if (!Operation.HasActiveOperation()) return;

        var op = Operation.GetCurrentOperation();
        var info = Operation.GetOperationInfo(
            GameObj<Il2CppMenace.Strategy.Operation>.Wrap(op.Pointer)
        );

        if (info != null)
        {
            SdkLogger.Msg($"Operation: {info.TemplateId}");
            SdkLogger.Msg($"Planet: {info.PlanetId}, Enemy: {info.EnemyFactionId}");
            SdkLogger.Msg($"Mission {info.CurrentMissionIndex + 1}/{info.MissionCount}");

            if (info.TimeLimit > 0)
                SdkLogger.Msg($"Time: {info.TimeSpent}/{info.TimeLimit} turns ({info.TimeRemaining} remaining)");
        }
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `OperationInfo`

A snapshot of a campaign operation's full state, returned by `GetOperationInfo()`.

```csharp
public class OperationInfo
{
    public string TemplateId { get; set; }
    public string EnemyFactionId { get; set; }
    public string FriendlyFactionId { get; set; }
    public string PlanetId { get; set; }
    public int CurrentMissionIndex { get; set; }
    public int MissionCount { get; set; }
    public int TimeSpent { get; set; }
    public int TimeLimit { get; set; }
    public int TimeRemaining { get; set; }
    public bool HasCompletedOnce { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateId` | `string` | Stable `m_ID` from the operation's `OperationTemplate`. Use this for all template lookups — never display names. |
| `EnemyFactionId` | `string` | Stable `m_ID` of the enemy `FactionTemplate`. `null` if unresolved. |
| `FriendlyFactionId` | `string` | Stable `m_ID` of the friendly (client) `StoryFactionTemplate`. `null` if unresolved. |
| `PlanetId` | `string` | Stable `m_ID` of the `PlanetTemplate` this operation is set on. `null` if unresolved. |
| `CurrentMissionIndex` | `int` | Zero-based index of the active mission within this operation. |
| `MissionCount` | `int` | Total number of missions in this operation. |
| `TimeSpent` | `int` | Strategic turns elapsed since the operation began. The precise meaning of this field is not fully verified — treat it as informational. |
| `TimeLimit` | `int` | Maximum turns allowed before the operation times out. `0` means no time limit. |
| `TimeRemaining` | `int` | Turns remaining before timeout, as reported by the game's `GetRemainingTime()` method. Meaningful only when `TimeLimit > 0`. |
| `HasCompletedOnce` | `bool` | **Always `false` in the current implementation.** Tracking this requires a cross-reference against `OperationsManager` that has not yet been wired up. Use `HasCompletedOperationType(templateId)` instead. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying operation object. |

---

## Method Reference

### Queries

#### `GetCurrentOperation()`

Returns the operation currently selected by `OperationsManager`. This is the operation the game considers active for the player.

```csharp
GameObj GetCurrentOperation()
```

Returns `GameObj.Null` if no operation is active or if the `OperationsManager` cannot be reached.

> **Note:** When you need full operation state, prefer calling `GetOperationInfo(operation)` with the typed overload over using this method directly. `GetCurrentOperation()` is most useful as a handle for pointer comparisons or for passing to other SDK methods.

---

#### `GetOperationInfo()`

Convenience overload. Retrieves the current operation and returns its state as an `OperationInfo` snapshot. Use this when you only need to read state and do not need the `GameObj` handle.

```csharp
OperationInfo GetOperationInfo()
```

Returns `null` if no operation is active.

---

#### `GetOperationInfo(operation)`

Preferred overload. Aggregates full operation state into an `OperationInfo` snapshot from a typed operation handle. Reads faction, planet, mission, and time fields via pre-resolved field handles, and invokes `GetRemainingTime()` via `GameMethod`.

```csharp
OperationInfo GetOperationInfo(GameObj<Il2CppMenace.Strategy.Operation> operation)
```

| Parameter | Type | Description |
|---|---|---|
| `operation` | `GameObj<Il2CppMenace.Strategy.Operation>` | The typed operation handle to query. |

Returns `null` if the handle is not alive. Individual fields (`EnemyFactionId`, `FriendlyFactionId`, `PlanetId`) are `null` if their underlying template references cannot be resolved.

---

#### `GetCurrentMission()`

Returns the active mission within the current operation, via the game's `GetCurrentMission()` method.

```csharp
GameObj GetCurrentMission()
```

Returns `GameObj.Null` if no operation is active or the method call fails. Pass the result to `Mission` methods for full mission state. See the `Mission` API reference for details.

---

#### `GetMissions()`

Returns all missions in the current operation as an ordered list of `GameObj` handles, in the order they appear in the operation's mission list.

```csharp
List<GameObj> GetMissions()
```

Returns an empty list if no operation is active or the mission list cannot be read. Pass individual entries to `Mission` methods for per-mission state.

---

#### `GetAllOperations()`

Returns all currently active operations, not just the current one. Reads the full `m_AvailableOperations` list from `OperationsManager`.

```csharp
List<GameObj> GetAllOperations()
```

Returns an empty list if no operations are active or `OperationsManager` cannot be reached.

---

#### `GetAllOperationInfo()`

Returns `OperationInfo` snapshots for every active operation.

```csharp
List<OperationInfo> GetAllOperationInfo()
```

Returns an empty list if no operations are active. Operations whose state cannot be read are silently skipped.

---

#### `FindByFaction(factionId)`

Searches all active operations for one whose enemy or friendly faction matches the given template ID.

```csharp
GameObj FindByFaction(string factionId)
```

| Parameter | Type | Description |
|---|---|---|
| `factionId` | `string` | Stable `m_ID` of the faction template to search for. |

Returns the first matching operation, or `GameObj.Null` if none is found. Returns `GameObj.Null` immediately if `factionId` is null or empty.

---

#### `FindByPlanet(planetId)`

Searches all active operations for one set on the given planet.

```csharp
GameObj FindByPlanet(string planetId)
```

| Parameter | Type | Description |
|---|---|---|
| `planetId` | `string` | Stable `m_ID` of the planet template to search for. |

Returns the first matching operation, or `GameObj.Null` if none is found. Returns `GameObj.Null` immediately if `planetId` is null or empty.

---

#### `GetCompletedOperationTypes()`

Returns the template IDs of all operation types that have been completed at least once in the current campaign, as recorded by `OperationsManager`.

```csharp
List<string> GetCompletedOperationTypes()
```

Returns an empty list if no operations have been completed or `OperationsManager` cannot be reached.

---

### Checks

#### `HasActiveOperation()`

Returns `true` if there is a currently active operation.

```csharp
bool HasActiveOperation()
```

Equivalent to checking whether `GetCurrentOperation()` is alive. Prefer this for simple guard checks before calling other methods.

---

#### `HasCompletedOperationType(operationTemplateId)`

Returns `true` if the given operation type has been completed at least once in the current campaign.

```csharp
bool HasCompletedOperationType(string operationTemplateId)
```

| Parameter | Type | Description |
|---|---|---|
| `operationTemplateId` | `string` | Stable `m_ID` of the `OperationTemplate` to check. |

> **Note:** This is the correct way to check completion history. `OperationInfo.HasCompletedOnce` is not currently populated — see the data type notes above.

---

#### `CanTimeOut()`

Returns `true` if the current operation is subject to a time limit.

```csharp
bool CanTimeOut()
```

Returns `false` if no operation is active or the call fails.

---

### Time

#### `GetRemainingTime()`

Returns the number of turns remaining before the current operation times out, as reported by the game's `GetRemainingTime()` method.

```csharp
int GetRemainingTime()
```

Returns `0` if no operation is active, the operation has no time limit, or the method call fails. Check `CanTimeOut()` before relying on this value.

---

### Low-Level Access

#### `GetOperationsManager()`

Returns the `OperationsManager` instance from the active `StrategyState`.

```csharp
GameObj GetOperationsManager()
```

Returns `GameObj.Null` if `StrategyState` cannot be found or the manager pointer is zero.

> **Note:** This method is public as an escape hatch for cases where the SDK does not yet expose something you need from `OperationsManager` directly. In most cases you should use the higher-level methods above rather than working with the manager handle yourself.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `operation` | *(none)* | Print full `OperationInfo` for the current operation. |
| `opmissions` | *(none)* | List all missions in the current operation with their status and current index. |
| `optime` | *(none)* | Print time spent and remaining for the current operation. |
| `alloperations` | *(none)* | List all active operations with faction, planet, mission progress, and time. |
| `completedops` | *(none)* | List all operation types completed in this campaign. |
| `findop` | `<id>` | Find an operation by faction or planet template ID. |

Example session:

```
> operation
Operation: operation_liberation_alpha
Planet: planet_kerath
Enemy: faction_empire_north
Allied: faction_resistance_cell_7
Missions: 2/4
Time: 3/10 (7 remaining)
Completed Before: False

> opmissions
Operation Missions (4):
  0. mission_recon_kerath [Completed]
  1. mission_assault_outpost [Active] <-- CURRENT
  2. mission_sabotage_depot [Locked]
  3. mission_extraction [Locked]

> alloperations
Active Operations (2):
  operation_liberation_alpha: faction_empire_north vs faction_resistance_cell_7 (Time: 7 left) <-- CURRENT
    Planet: planet_kerath, Mission 2/4
  operation_siege_bravo: faction_empire_south vs faction_coalition_main
    Planet: planet_vorrath, Mission 1/3

> completedops
Completed Operation Types (2):
  operation_tutorial_contact
  operation_border_skirmish
```

---

## Error Handling

All methods follow safe-read patterns — null pointer guards and alive checks are performed before any field access or method invocation. Failures inside SDK infrastructure are reported via `SdkLogger`.

| Method | Fallback on error |
|---|---|
| `GetCurrentOperation` | `GameObj.Null` |
| `GetOperationInfo` (both overloads) | `null` |
| `GetCurrentMission` | `GameObj.Null` |
| `GetMissions` | Empty `List<GameObj>` |
| `GetAllOperations` | Empty `List<GameObj>` |
| `GetAllOperationInfo` | Empty `List<OperationInfo>` |
| `FindByFaction` | `GameObj.Null` |
| `FindByPlanet` | `GameObj.Null` |
| `GetCompletedOperationTypes` | Empty `List<string>` |
| `HasActiveOperation` | `false` |
| `HasCompletedOperationType` | `false` |
| `CanTimeOut` | `false` |
| `GetRemainingTime` | `0` |
| `GetOperationsManager` | `GameObj.Null` |