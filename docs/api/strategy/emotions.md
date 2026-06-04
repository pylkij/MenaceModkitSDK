# Emotions API Reference

`Emotions` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's emotional state system and exposes safe access to the morale and psychological effects applied to unit leaders. Emotions are triggered by in-game events — kills, injuries, ally deaths, friendly fire, near-death experiences, and more — and apply skill modifiers that affect combat performance. Call these methods any time after `GameState.SceneLoaded` has fired — field handles are resolved automatically on first scene load.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetEmotionalStates(leader)` | `GameObj` | Queries |
| `GetEmotionalStatesInfo(leader)` | `EmotionalStatesInfo` | Queries |
| `GetEmotionInfo(leader, type)` | `EmotionalStateInfo` | Queries |
| `GetStateSet(leader)` | `EmotionalStateType` | Queries |
| `HasEmotion(leader, type)` | `bool` | Status Checks |
| `HasAnyEmotion(leader, types)` | `bool` | Status Checks |
| `GetRemainingDuration(leader, type)` | `int` | Status Checks |
| `TriggerEmotion(leader, trigger, target?)` | `EmotionResult` | Write |
| `ApplyEmotion(leader, templateId, trigger?, target?)` | `EmotionResult` | Write |
| `RemoveEmotion(leader, type)` | `EmotionResult` | Write |
| `ExtendDuration(leader, type, missions?)` | `EmotionResult` | Write |
| `ClearEmotions(leader)` | `int` | Write |
| `ClearNegativeEmotions(leader)` | `int` | Write |
| `ClearPositiveEmotions(leader)` | `int` | Write |
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
        // Emotions resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately.
        var leader = Roster.FindByNicknameTyped("Pike");
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return;

        // Apply a specific emotion template by stable template ID.
        var applyResult = Emotions.ApplyEmotion(leader, "emotion_determined");
        if (applyResult.Success)
            SdkLogger.Msg($"Applied: {applyResult.StateType} ({applyResult.Action})");

        // Clear any negative emotions the leader is carrying.
        int cleared = Emotions.ClearNegativeEmotions(leader);
        SdkLogger.Msg($"Cleared {cleared} negative emotion(s) from {leader.Untyped.GetName()}");

        // Inspect what's left.
        var info = Emotions.GetEmotionalStatesInfo(leader);
        if (info == null) return;

        SdkLogger.Msg($"{info.OwnerName}: {info.PositiveCount} positive, {info.NegativeCount} negative");
        foreach (var state in info.ActiveStates)
        {
            var polarity = state.IsPositive ? "+" : "-";
            var target = !string.IsNullOrEmpty(state.TargetLeaderName)
                ? $" -> {state.TargetLeaderName}"
                : "";
            SdkLogger.Msg($"  [{polarity}] {state.Type} ({state.RemainingDuration} missions){target}");
        }
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Enums

### `EmotionalStateType`

Mirrors `Il2CppMenace.Strategy.EmotionalStateType`. This is a `[Flags]` bitmask — values may be combined. `GetStateSet()` returns a combined value; test individual flags with bitwise AND (e.g. `(set & EmotionalStateType.Determined) != 0`).

| Value | Integer | Positive | Description |
|---|---|---|---|
| `None` | `0` | — | No emotional state. |
| `AnimosityTowards` | `1` | No | Animosity towards a specific target leader. |
| `Determined` | `2` | Yes | Focused and resolute. |
| `Weary` | `4` | No | Tired from extended duty. |
| `Disheartened` | `8` | No | Morale reduced. |
| `Eager` | `16` | Yes | Enthusiastic and ready for action. |
| `Frustrated` | `32` | No | Annoyed and less effective. |
| `Exhausted` | `64` | No | Severely fatigued. |
| `GoodwillTowards` | `128` | Yes | Goodwill towards a specific target leader. |
| `Hesitant` | `256` | No | Uncertain and cautious. |
| `Overconfident` | `512` | No | Too bold; may make mistakes. |
| `Injured` | `1024` | No | Physically wounded. |
| `Bruised` | `2048` | No | Minor physical damage. |
| `Euphoric` | `4096` | Yes | Extremely positive mood. |
| `Miserable` | `8192` | No | Extremely negative mood. |

> **Note:** `AnimosityTowards` and `GoodwillTowards` are targeted emotions directed at a specific leader. When reading these via `EmotionalStateInfo`, check `TargetLeaderName` for the relationship target.

---

### `EmotionalStateCategory`

Mirrors `Il2CppMenace.Strategy.EmotionalStateCategory`. Classifies an emotion by its general nature.

| Value | Integer | Description |
|---|---|---|
| `Normal` | `0` | Standard morale and psychological effects. |
| `Injuries` | `1` | Physical damage states (Bruised, Injured). |
| `Exhaustion` | `2` | Fatigue-related states (Weary, Exhausted). |
| `Relationship` | `3` | Targeted states towards another leader (AnimosityTowards, GoodwillTowards). |

---

### `EmotionalTrigger`

Mirrors `Il2CppMenace.Strategy.EmotionalTrigger`. Identifies the in-game event that caused an emotion to be applied.

| Value | Integer | Description |
|---|---|---|
| `StabilizedBy` | `0` | Was stabilised by another unit. |
| `StabilizedOthers` | `1` | Stabilised other units. |
| `ReceivedFriendlyFireFrom` | `2` | Received friendly fire from another unit. |
| `DeployedXTimesWithOther` | `3` | Deployed a number of times alongside another unit. |
| `KilledXEnemyEntities` | `4` | Killed a threshold number of enemy entities. |
| `KilledXEnemyMiniBosses` | `5` | Killed a threshold number of enemy mini-bosses. |
| `DeployedInTheXMissionsBeforeCurrent` | `6` | Deployed in a number of recent prior missions. |
| `NotDeployedInTheXMissionsBeforeCurrent` | `7` | Not deployed in a number of recent prior missions. |
| `KilledXCivElements` | `8` | Killed a threshold number of civilian elements. |
| `SuccessOnFavPlanet` | `9` | Mission success on the leader's favoured planet. |
| `FailedOnFavPlanet` | `10` | Mission failure on the leader's favoured planet. |
| `LostOverXPercentHitpoints` | `11` | Lost more than a threshold percentage of hitpoints. |
| `GameEffect` | `12` | Triggered by a game effect. |
| `Event` | `14` | Triggered by a scripted event. |
| `Cheat` | `16` | Manually applied via dev tooling. Default trigger for `ApplyEmotion()`. |
| `OtherLeaderKilledCivElementOnFavPlanet` | `18` | Another leader killed a civilian element on this leader's favoured planet. |
| `Fled` | `19` | The unit fled from combat. |
| `NearDeathExperience` | `20` | Survived a near-fatal encounter. |
| `LostAllSquaddies` | `21` | All squad members were lost. |

---

## Data Types

### `EmotionalStateInfo`

A snapshot of a single active emotional state, returned as part of the list from `GetEmotionalStatesInfo()` or directly from `GetEmotionInfo()`.

```csharp
public class EmotionalStateInfo
{
    public EmotionalStateType Type { get; set; }
    public EmotionalTrigger Trigger { get; set; }
    public EmotionalStateCategory Category { get; set; }
    public string TargetLeaderName { get; set; }
    public int RemainingDuration { get; set; }
    public bool IsNew { get; set; }
    public bool IsPositive { get; set; }
    public bool IsSuperState { get; set; }
    public string SkillName { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `Type` | `EmotionalStateType` | The emotion type, as a single flag value. |
| `Trigger` | `EmotionalTrigger` | The event that caused this emotion to be applied. |
| `Category` | `EmotionalStateCategory` | The general classification of this emotion. |
| `TargetLeaderName` | `string` | Name of the target leader for relationship emotions (`AnimosityTowards`, `GoodwillTowards`). `null` for non-targeted emotions. |
| `RemainingDuration` | `int` | Missions remaining until this emotion expires. |
| `IsNew` | `bool` | `true` if this emotion was applied during the current mission. |
| `IsPositive` | `bool` | `true` if this emotion confers a positive effect. |
| `IsSuperState` | `bool` | `true` if this emotion is a super state — a heightened form that may supersede a base emotion. |
| `SkillName` | `string` | Name of the skill modifier applied by this emotion, read from the `Effect` field of its template. `null` if no skill modifier could be read. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying `EmotionalState` object. |

---

### `EmotionalStatesInfo`

A snapshot of a unit leader's full emotional state collection, returned by `GetEmotionalStatesInfo()`.

```csharp
public class EmotionalStatesInfo
{
    public string OwnerName { get; set; }
    public IntPtr OwnerPointer { get; set; }
    public IntPtr Pointer { get; set; }
    public List<EmotionalStateInfo> ActiveStates { get; set; }
    public int StateCount { get; }
    public int PositiveCount { get; }
    public int NegativeCount { get; }
}
```

| Property | Type | Description |
|---|---|---|
| `OwnerName` | `string` | Name of the owning unit leader. |
| `OwnerPointer` | `IntPtr` | Raw native pointer to the owning `BaseUnitLeader` object. |
| `Pointer` | `IntPtr` | Raw native pointer to the `EmotionalStates` collection object. |
| `ActiveStates` | `List<EmotionalStateInfo>` | All currently active emotional states. Never `null`; empty if the leader has no active emotions. |
| `StateCount` | `int` | Total number of active emotions. Equivalent to `ActiveStates.Count`. |
| `PositiveCount` | `int` | Number of active emotions where `IsPositive` is `true`. |
| `NegativeCount` | `int` | Number of active emotions where `IsPositive` is `false`. |

---

### `EmotionResult`

Returned by all write operations. Indicates whether the operation succeeded and, if so, what action was taken.

```csharp
public class EmotionResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
    public EmotionalStateType StateType { get; set; }
    public string Action { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `Success` | `bool` | `true` if the operation completed successfully. |
| `Error` | `string` | Human-readable error message if `Success` is `false`. `null` on success. |
| `StateType` | `EmotionalStateType` | The emotion type involved in the operation. `None` for trigger-based operations where the resulting type is determined by the game. |
| `Action` | `string` | Description of the action taken. One of: `Applied`, `Triggered`, `Extended`, `Removed`. |

---

## Method Reference

### Queries

#### `GetEmotionalStates(leader)`

Retrieves the raw `EmotionalStates` collection object for a unit leader.

```csharp
GameObj GetEmotionalStates(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |

Returns `GameObj.Null` if the leader is not alive or the collection could not be read.

> **Note:** This method returns an untyped `GameObj` for use in low-level scenarios. In most cases, prefer `GetEmotionalStatesInfo()` for a fully-populated snapshot or `GetEmotionInfo()` for a specific emotion.

---

#### `GetEmotionalStatesInfo(leader)`

Aggregates all emotional state data for a unit leader into a single `EmotionalStatesInfo` snapshot. Reads each state's type, trigger, category, duration, polarity, super-state flag, target leader name, and applied skill name via pre-resolved field handles. Individual states that fail to read are skipped with a warning logged; all others are included regardless of polarity or duration.

```csharp
EmotionalStatesInfo GetEmotionalStatesInfo(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |

Returns `null` if the leader is not alive or the collection could not be read.

---

#### `GetEmotionInfo(leader, type)`

Returns the `EmotionalStateInfo` for a specific active emotion type on a leader.

```csharp
EmotionalStateInfo GetEmotionInfo(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |
| `type` | `EmotionalStateType` | The emotion type to retrieve. |

Returns `null` if the leader does not have an active emotion of that type.

---

#### `GetStateSet(leader)`

Returns a bitmask of all active emotion types for a unit leader as a single combined `EmotionalStateType` value. Test individual flags with bitwise AND:

```csharp
var set = Emotions.GetStateSet(leader);
if ((set & EmotionalStateType.Determined) != 0)
    SdkLogger.Msg("Leader is determined");
```

```csharp
EmotionalStateType GetStateSet(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |

Returns `EmotionalStateType.None` if the leader has no active emotions or the collection could not be read.

---

### Status Checks

#### `HasEmotion(leader, type)`

```csharp
bool HasEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to check. |
| `type` | `EmotionalStateType` | The emotion type to test for. |

Returns `true` if the leader has an active emotion of the specified type. Returns `false` if the leader is not alive or the collection could not be read.

---

#### `HasAnyEmotion(leader, types)`

```csharp
bool HasAnyEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, params EmotionalStateType[] types)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to check. |
| `types` | `EmotionalStateType[]` | One or more emotion types to test for. |

Returns `true` if the leader has an active emotion matching any of the specified types. Short-circuits on the first match.

---

#### `GetRemainingDuration(leader, type)`

```csharp
int GetRemainingDuration(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |
| `type` | `EmotionalStateType` | The emotion type to check. |

Returns the number of missions remaining until the emotion expires. Returns `-1` if the leader does not have an active emotion of that type.

---

### Write Operations

Write methods are no-ops if any required object in the call chain (`BaseUnitLeader`, `EmotionalStates`) is unavailable. Each logs a warning or error at the point of failure and returns an appropriate fallback value.

#### `TriggerEmotion(leader, trigger, target?)`

Fires a trigger event on a unit leader, allowing the game's emotional state system to evaluate and apply the appropriate emotion based on the leader's templates and current state.

```csharp
EmotionResult TriggerEmotion(
    GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader,
    EmotionalTrigger trigger,
    GameObj<Il2CppMenace.Strategy.BaseUnitLeader> target = default)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader on whom to fire the trigger. |
| `trigger` | `EmotionalTrigger` | The trigger event to fire. |
| `target` | `GameObj<BaseUnitLeader>` | Optional. The target leader for relationship triggers such as `ReceivedFriendlyFireFrom` or `DeployedXTimesWithOther`. Ignored if not alive. |

Returns an `EmotionResult` with `Action = "Triggered"` on success. `StateType` will be `None` — the resulting emotion type is determined by the game's evaluation logic, not this call. Returns a failed result if the leader's `EmotionalStates` collection is unavailable.

> **Note:** Whether an emotion is actually applied depends on the game's internal evaluation of the leader's templates against the trigger. A successful result indicates the trigger was fired, not that an emotion was necessarily added. Use `GetEmotionalStatesInfo()` after the call to inspect the outcome.

---

#### `ApplyEmotion(leader, templateId, trigger?, target?)`

Applies a specific emotional state template to a unit leader by stable template ID, bypassing the game's trigger evaluation logic.

```csharp
EmotionResult ApplyEmotion(
    GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader,
    string templateId,
    EmotionalTrigger trigger = EmotionalTrigger.Cheat,
    GameObj<Il2CppMenace.Strategy.BaseUnitLeader> target = default)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to apply the emotion to. |
| `templateId` | `string` | Stable `m_ID` of the `EmotionalStateTemplate` to apply. Use the `emotemplates` console command to list available IDs. |
| `trigger` | `EmotionalTrigger` | The trigger to record as the cause. Defaults to `EmotionalTrigger.Cheat`. |
| `target` | `GameObj<BaseUnitLeader>` | Optional. The target leader for relationship emotions. Ignored if not alive. |

Returns an `EmotionResult` with `Action = "Applied"` on success. Returns a failed result if the template ID is not found, the leader's `EmotionalStates` collection is unavailable, or the game's `TryApplyEmotionalState` call returns `false`.

> **Note:** Use `templateId` values from `emotemplates` — never display names. The game's `TryApplyEmotionalState` may still reject the application if preconditions are not met (e.g. the state conflicts with an existing super state). Check `EmotionResult.Success` before assuming the emotion was added.

---

#### `RemoveEmotion(leader, type)`

Removes the active emotional state of the specified type from a unit leader.

```csharp
EmotionResult RemoveEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to modify. |
| `type` | `EmotionalStateType` | The emotion type to remove. |

Returns an `EmotionResult` with `Action = "Removed"` on success. Returns a failed result if the leader does not have an active emotion of the specified type or the collection is unavailable.

---

#### `ExtendDuration(leader, type, missions?)`

Extends the remaining duration of an active emotion by a number of missions.

```csharp
EmotionResult ExtendDuration(
    GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader,
    EmotionalStateType type,
    int missions = 1)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to modify. |
| `type` | `EmotionalStateType` | The emotion type to extend. |
| `missions` | `int` | Number of missions to add to the remaining duration. Defaults to `1`. |

Returns an `EmotionResult` with `Action = "Extended"` on success. Returns a failed result if the leader does not have an active emotion of the specified type or the collection is unavailable.

---

#### `ClearEmotions(leader)`

Removes all active emotional states from a unit leader, regardless of polarity.

```csharp
int ClearEmotions(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to clear. |

Returns the number of emotions successfully removed. Returns `0` if the leader has no active emotions or the collection is unavailable. Individual removal failures are logged as errors and do not prevent remaining emotions from being processed.

---

#### `ClearNegativeEmotions(leader)`

Removes all active emotional states where `IsPositive` is `false`.

```csharp
int ClearNegativeEmotions(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to clear. |

Returns the number of negative emotions successfully removed.

---

#### `ClearPositiveEmotions(leader)`

Removes all active emotional states where `IsPositive` is `true`.

```csharp
int ClearPositiveEmotions(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to clear. |

Returns the number of positive emotions successfully removed.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `emotions` | `<nickname>` | List all active emotions for the named unit, including polarity, type, duration, and target leader where applicable. |
| `hasemotion` | `<nickname> <type>` | Check whether the named unit has a specific emotion type active, and print the remaining duration if so. |
| `triggeremotion` | `<nickname> <trigger>` | Fire a trigger event on the named unit (e.g. `KilledXEnemyEntities`, `GameEffect`, `Cheat`). |
| `applyemotion` | `<nickname> <templateId>` | Apply an emotion template to the named unit by stable template ID. |
| `removeemotion` | `<nickname> <type>` | Remove an active emotion by type from the named unit (e.g. `Determined`, `Weary`, `Euphoric`). |
| `clearemotions` | `<nickname> [negative\|positive]` | Clear all, negative, or positive emotions from the named unit. Omit the filter to clear all. |
| `extendemotion` | `<nickname> <type> [missions]` | Extend the duration of an active emotion by the specified number of missions (default: 1). |
| `emotemplates` | *(none)* | List all available `EmotionalStateTemplate` IDs. |

Example session:

```
> emotions Pike
Emotional States for Pike (2 active):
  Positive: 1, Negative: 1
  [+] Determined: 3 missions
  [-] Weary: 1 missions

> hasemotion Pike Weary
Pike HAS Weary (1 missions remaining)

> extendemotion Pike Weary 2
Extended Weary by 2 mission(s)

> applyemotion Pike emotion_euphoric
Applied 'emotion_euphoric' to Pike: Applied

> removeemotion Pike Weary
Removed Weary from Pike

> clearemotions Pike negative
Removed 0 negative emotion(s) from Pike

> emotions Pike
Emotional States for Pike (2 active):
  Positive: 2, Negative: 0
  [+] Determined: 3 missions
  [+] Euphoric: 2 missions
```

---

## Error Handling

All methods handle failures internally and report them via `SdkLogger`. No method propagates exceptions to the caller.

| Method | Fallback on error |
|---|---|
| `GetEmotionalStates` | `GameObj.Null` |
| `GetEmotionalStatesInfo` | `null` (unreadable individual states skipped) |
| `GetEmotionInfo` | `null` |
| `GetStateSet` | `EmotionalStateType.None` |
| `HasEmotion` / `HasAnyEmotion` | `false` |
| `GetRemainingDuration` | `-1` |
| `TriggerEmotion` / `ApplyEmotion` / `RemoveEmotion` / `ExtendDuration` | `EmotionResult` with `Success = false` and `Error` set |
| `ClearEmotions` / `ClearNegativeEmotions` / `ClearPositiveEmotions` | `0` |
