# Mission API Reference

`Mission` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's mission system and exposes safe access to mission state, objectives, and mission flow control. Call these methods any time after `GameState.SceneLoaded` has fired — field handles are resolved automatically on first scene load.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetMission()` | `Il2CppMenace.Strategy.Mission` | Queries |
| `GetMissionInfo(mission)` | `MissionInfo` | Queries |
| `GetObjectives(mission)` | `List<ObjectiveInfo>` | Queries |
| `GetStatus()` | `MissionStatus?` | Status Checks |
| `IsPlayable()` | `bool` | Status Checks |
| `IsLocked()` | `bool` | Status Checks |
| `IsPlayed()` | `bool` | Status Checks |
| `IsUnplayable()` | `bool` | Status Checks |
| `CompletePendingObjectives()` | `void` | Write |
| `CompleteObjective(index)` | `bool` | Write |
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
        // Mission resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately.
        var mission = Mission.GetMission();
        if (mission == null) return;

        var info = Mission.GetMissionInfo(mission);
        if (info != null)
            SdkLogger.Msg($"Mission: {info.TemplateId}, Status: {info.Status}, Layer: {info.Layer}, Light: {info.LightCondition}");

        var objectives = Mission.GetObjectives(mission);
        foreach (var obj in objectives)
            SdkLogger.Msg($"  [{(obj.IsComplete ? "DONE" : obj.IsFailed ? "FAIL" : "    ")}] {obj.Name} ({obj.Progress}/{obj.TargetProgress})");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Enums

### `MissionStatus`

Represents the current availability state of a mission.

| Value | Integer | Description |
|---|---|---|
| `Playable` | `0` | The mission is available and can be entered. |
| `Locked` | `1` | The mission exists but is not yet accessible. |
| `Played` | `2` | The mission has already been completed. |
| `Unplayable` | `3` | The mission cannot be played under current conditions. |

---

### `MissionLayer`

Indicates where a mission falls in a campaign or operation sequence.

| Value | Integer | Description |
|---|---|---|
| `Invalid` | `0` | Layer could not be determined. |
| `First` | `1` | An opening mission in the sequence. |
| `Middle` | `2` | An intermediate mission. |
| `Final` | `3` | The concluding mission in the sequence. |

---

### `LightConditionType`

The ambient lighting environment for the mission's tactical layer.

| Value | Integer | Description |
|---|---|---|
| `Dawn` | `0` | Early morning lighting. |
| `Day` | `1` | Full daylight. |
| `Dusk` | `2` | Late evening lighting. |
| `Night` | `3` | Night-time conditions. |
| `Random` | `4` | Light condition is randomised at mission start. |

---

## Data Types

### `MissionInfo`

A snapshot of the active mission's full state, returned by `GetMissionInfo()`.

```csharp
public class MissionInfo
{
    public string TemplateId { get; set; }
    public MissionStatus Status { get; set; }
    public MissionLayer Layer { get; set; }
    public int Seed { get; set; }
    public string BiomeId { get; set; }
    public string WeatherId { get; set; }
    public LightConditionType LightCondition { get; set; }
    public string DifficultyId { get; set; }
    public float EnemyArmyPoints { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateId` | `string` | Stable `m_ID` from the mission's `MissionTemplate`. Use this for all template lookups — never display names. |
| `Status` | `MissionStatus` | Current availability state of the mission. |
| `Layer` | `MissionLayer` | Position of this mission in the campaign sequence. |
| `Seed` | `int` | Random seed used for procedural generation of this mission instance. |
| `BiomeId` | `string` | Stable `m_ID` of the mission's `BiomeTemplate`. `null` if the field could not be read. |
| `WeatherId` | `string` | Stable `m_ID` of the mission's `WeatherTemplate`. `null` if the field could not be read. |
| `LightCondition` | `LightConditionType` | Ambient lighting environment for the tactical layer. |
| `DifficultyId` | `string` | Stable `m_ID` of the mission's `MissionDifficultyTemplate`. `null` if the field could not be read. |
| `EnemyArmyPoints` | `float` | Total army point budget allocated to the enemy force. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying mission object. |

---

### `ObjectiveInfo`

Describes a single mission objective, returned as part of the list from `GetObjectives()`.

```csharp
public class ObjectiveInfo
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsComplete { get; set; }
    public bool IsFailed { get; set; }
    public int Progress { get; set; }
    public int TargetProgress { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Localised title of the objective, via `GetTitle()`. |
| `Description` | `string` | Localised body text of the objective, via `GetTranslatedObjectiveText()`. |
| `IsComplete` | `bool` | `true` if the objective has been successfully completed. |
| `IsFailed` | `bool` | `true` if the objective has failed and cannot be recovered. |
| `Progress` | `int` | Current progress value toward the objective's target. |
| `TargetProgress` | `int` | Required progress value for completion. `0` on objectives without a numeric counter. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying objective object. |

---

## Method Reference

### Queries

#### `GetMission()`

Retrieves the currently active mission via the `TacticalManager`.

```csharp
Il2CppMenace.Strategy.Mission GetMission()
```

Returns `null` if no mission is active or the `TacticalManager` is unavailable.

> **Note:** This method returns a raw `Il2CppMenace.Strategy.Mission` rather than an SDK-wrapped type. The raw handle is what the game's objective manager and status APIs expect — pass it directly into `GetMissionInfo()` and `GetObjectives()` rather than inspecting it yourself.

---

#### `GetMissionInfo(mission)`

Aggregates all mission state into a single `MissionInfo` snapshot. Reads template, status, layer, seed, biome, weather, light condition, and difficulty via pre-resolved field handles, and invokes `GetEnemyArmyPoints()` via the native mission object. Fields that fail to read are left at their default values; a warning or error is logged for each failure.

```csharp
MissionInfo GetMissionInfo(Il2CppMenace.Strategy.Mission mission)
```

| Parameter | Type | Description |
|---|---|---|
| `mission` | `Il2CppMenace.Strategy.Mission` | The mission object to query. Obtain from `GetMission()`. |

Returns `null` if `mission` is `null`.

---

#### `GetObjectives(mission)`

Returns a list of `ObjectiveInfo` snapshots for all objectives on the given mission. Null or unreadable individual objectives are skipped with a warning logged; all others are included regardless of completion state.

```csharp
List<ObjectiveInfo> GetObjectives(Il2CppMenace.Strategy.Mission mission)
```

| Parameter | Type | Description |
|---|---|---|
| `mission` | `Il2CppMenace.Strategy.Mission` | The mission object to query. Obtain from `GetMission()`. |

Returns an empty list if `mission` is `null` or has no objective manager.

---

### Status Checks

All status check methods resolve the active mission internally via `TacticalManager` and return `false` (or `null` for `GetStatus`) if no mission is active or the manager is unavailable. Each call to a convenience check invokes `GetStatus()` once.

#### `GetStatus()`

Returns the `MissionStatus` of the currently active mission.

```csharp
MissionStatus? GetStatus()
```

Returns `null` if no mission is active or the `TacticalManager` is unavailable.

---

#### `IsPlayable()`

```csharp
bool IsPlayable()
```

Returns `true` if the active mission's status is `MissionStatus.Playable`.

---

#### `IsLocked()`

```csharp
bool IsLocked()
```

Returns `true` if the active mission's status is `MissionStatus.Locked`.

---

#### `IsPlayed()`

```csharp
bool IsPlayed()
```

Returns `true` if the active mission's status is `MissionStatus.Played`.

---

#### `IsUnplayable()`

```csharp
bool IsUnplayable()
```

Returns `true` if the active mission's status is `MissionStatus.Unplayable`.

---

### Write Operations

Write methods that target objectives are no-ops if any required manager in the call chain (`TacticalManager`, mission, objective manager) is unavailable. Each logs a warning or error at the point of failure.

#### `CompletePendingObjectives()`

Force-completes all objectives on the current mission that are not already in a terminal state (`IsCompleted` or `IsFailed`). Objectives already in a terminal state are silently skipped. Individual `ForceComplete` failures are logged as errors and do not prevent remaining objectives from being processed.

```csharp
void CompletePendingObjectives()
```

---

#### `CompleteObjective(index)`

Force-completes the objective at the specified index in the current mission's objective list.

```csharp
bool CompleteObjective(int index)
```

| Parameter | Type | Description |
|---|---|---|
| `index` | `int` | Zero-based index into the objective list returned by `GetObjectives()`. |

Returns `true` on success. Returns `false` without throwing if the index is out of range, the objective is already in a terminal state, or any required manager is unavailable.

> **Note:** Use `GetObjectives()` first to inspect the list and confirm the target index, especially after `CompletePendingObjectives()` has already advanced some objectives.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `mission` | *(none)* | Print template name, status, layer, seed, biome, weather, light condition, difficulty, and enemy army points for the active mission. |
| `objectives` | *(none)* | List all objectives with their index, completion state, and progress counters. |
| `completeobjective` | `<index>` | Force-complete the objective at the given zero-based index. |
| `missionstatus` | *(none)* | Print the current mission status and a summary count of completed, failed, and remaining objectives. |

Example session:

```
> mission
Mission: mission_assault_bridgehead
Status: Playable, Layer: First
Seed: 482910
Biome: biome_temperate, Weather: weather_overcast
Light: Dusk, Difficulty: difficulty_normal
Enemy Army Points: 1450

> objectives
Objectives (3):
  0. [    ] Capture the bridge [0/1]
  1. [    ] Destroy enemy artillery [0/3]
  2. [    ] Evacuate wounded units [0/1]

> completeobjective 1
Completed objective 1

> missionstatus
Mission Status: Playable
Objectives: 1 complete, 0 failed, 2 remaining
```

---

## Error Handling

All methods handle failures internally and report them via `SdkLogger`. No method propagates exceptions to the caller.

| Method | Fallback on error |
|---|---|
| `GetMission` | `null` |
| `GetMissionInfo` | `null` |
| `GetObjectives` | Empty `List<ObjectiveInfo>` (unreadable entries skipped) |
| `GetStatus` | `null` |
| `IsPlayable` / `IsLocked` / `IsPlayed` / `IsUnplayable` | `false` |
| `CompletePendingObjectives` | No-op; per-objective failures logged individually |
| `CompleteObjective` | `false` |