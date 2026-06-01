# TacticalController API Reference

`TacticalController` is a static class in the `Menace.SDK` namespace. It controls tactical game state including rounds, turns, time scale, and mission flow, wrapping the game's `TacticalManager` and `TacticalState` singletons. Safe to call any time after `GameState.SceneLoaded` has fired for a tactical scene.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetCurrentFactionType()` | `FactionType` | Turn State |
| `IsPlayerTurn()` | `bool` | Turn State |
| `IsPaused()` | `bool` | Turn State |
| `SetPaused(bool)` | `bool` | Turn State |
| `GetTimeScale()` | `float` | Turn State |
| `SetTimeScale(float)` | `bool` | Turn State |
| `GetCurrentRound()` | `int` | Round State |
| `IsMissionRunning()` | `bool` | Round State |
| `IsAnyPlayerUnitAlive()` | `bool` | Round State |
| `IsAnyEnemyAlive()` | `bool` | Round State |
| `GetTotalEnemyCount()` | `int` | Round State |
| `GetDeadEnemyCount()` | `int` | Round State |
| `GetActiveActor()` | `GameObj` | Actors |
| `SetActiveActor(GameObj)` | `bool` | Actors |
| `EndTurn()` | `bool` | Flow Control |
| `NextRound()` | `bool` | Flow Control |
| `NextFaction()` | `bool` | Flow Control |
| `SkipAITurn()` | `bool` | Flow Control |
| `FinishMission(TacticalFinishReason)` | `bool` | Flow Control |
| `ClearAllEnemies()` | `int` | Spawning |
| `GetTacticalState()` | `TacticalStateInfo` | Snapshot |

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
        // No setup required
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (!GameState.IsTactical) return;

        var state = TacticalController.GetTacticalState();
        logger.Msg($"Round {state.RoundNumber} — {state.CurrentFactionType}'s turn");
        logger.Msg($"Enemies: {state.AliveEnemyCount} alive, {state.DeadEnemyCount} dead");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `FactionType`

Mirrors the game's `Menace.Tactical.FactionType` enum exactly.

```csharp
public enum FactionType
{
    Neutral          = 0,
    Player           = 1,
    PlayerAI         = 2,
    Civilian         = 3,
    AlliedLocalForces = 4,
    EnemyLocalForces = 5,
    Pirates          = 6,
    Wildlife         = 7,
    Constructs       = 8,
    RogueArmy        = 9
}
```

Cast freely to and from `int` or `Il2CppMenace.Tactical.FactionType`. Call `.ToString()` for a human-readable name.

---

### `TacticalFinishReason`

Mirrors the game's `Menace.Tactical.TacticalFinishReason` enum exactly.

```csharp
public enum TacticalFinishReason
{
    None             = 0,
    AllPlayerUnitsDead = 1,
    Leave            = 2,
    LoadingSavegame  = 3
}
```

Passed to `FinishMission()`. Use `Leave` for a clean programmatic exit.

---

### `TacticalStateInfo`

A snapshot of the full tactical state, returned by `GetTacticalState()`.

```csharp
public class TacticalStateInfo
{
    public int RoundNumber { get; set; }
    public FactionType CurrentFactionType { get; set; }
    public bool IsPlayerTurn { get; set; }
    public bool IsPaused { get; set; }
    public float TimeScale { get; set; }
    public bool IsMissionRunning { get; set; }
    public string ActiveActorName { get; set; }
    public bool IsAnyPlayerAlive { get; set; }
    public bool IsAnyEnemyAlive { get; set; }
    public int TotalEnemyCount { get; set; }
    public int DeadEnemyCount { get; set; }
    public int AliveEnemyCount { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `RoundNumber` | `int` | Current round, 1-indexed |
| `CurrentFactionType` | `FactionType` | Faction whose turn it currently is |
| `IsPlayerTurn` | `bool` | `true` if `CurrentFactionType == Player` |
| `IsPaused` | `bool` | Whether the game is currently paused |
| `TimeScale` | `float` | Current `Time.timeScale` value |
| `IsMissionRunning` | `bool` | Whether the mission is still in progress |
| `ActiveActorName` | `string` | Unity name of the selected actor, or `null` if none |
| `IsAnyPlayerAlive` | `bool` | Whether at least one player unit is alive |
| `IsAnyEnemyAlive` | `bool` | Whether at least one AI unit is alive |
| `TotalEnemyCount` | `int` | Total enemy actors (alive and dead) |
| `DeadEnemyCount` | `int` | Dead enemy actors |
| `AliveEnemyCount` | `int` | `TotalEnemyCount - DeadEnemyCount` |

---

## Method Reference

### Turn State

#### `GetCurrentFactionType()`

Returns the faction whose turn it currently is.

```csharp
FactionType GetCurrentFactionType()
```

Returns `FactionType.Neutral` if the faction ID is out of range or the singleton is unavailable.

---

#### `IsPlayerTurn()`

Returns `true` if the current faction is `FactionType.Player`.

```csharp
bool IsPlayerTurn()
```

---

#### `IsPaused()`

Returns whether the game is currently paused.

```csharp
bool IsPaused()
```

Returns `false` if the singleton is unavailable.

---

#### `SetPaused(paused)`

Pauses or unpauses the game.

```csharp
bool SetPaused(bool paused)
```

| Parameter | Type | Description |
|---|---|---|
| `paused` | `bool` | `true` to pause, `false` to unpause |

Returns `false` if the singleton is unavailable.

---

#### `GetTimeScale()`

Returns the current time scale directly from `Time.timeScale`.

```csharp
float GetTimeScale()
```

---

#### `SetTimeScale(scale)`

Sets the time scale, clamped to the range `[0, 10]`.

```csharp
bool SetTimeScale(float scale)
```

| Parameter | Type | Description |
|---|---|---|
| `scale` | `float` | `1.0` = normal speed, `2.0` = 2× speed, `0.5` = half speed |

Always returns `true`.

---

### Round State

#### `GetCurrentRound()`

Returns the current round number, 1-indexed.

```csharp
int GetCurrentRound()
```

Returns `0` if the singleton is unavailable.

---

#### `IsMissionRunning()`

Returns whether the mission is currently in progress.

```csharp
bool IsMissionRunning()
```

Returns `false` if the call fails.

---

#### `IsAnyPlayerUnitAlive()`

Returns whether at least one player unit is still alive.

```csharp
bool IsAnyPlayerUnitAlive()
```

Returns `false` if the singleton is unavailable.

---

#### `IsAnyEnemyAlive()`

Returns whether at least one AI unit is still alive.

```csharp
bool IsAnyEnemyAlive()
```

Returns `false` if the singleton is unavailable.

---

#### `GetTotalEnemyCount()`

Returns the total number of enemy actors, including dead ones.

```csharp
int GetTotalEnemyCount()
```

Returns `0` if the singleton is unavailable.

---

#### `GetDeadEnemyCount()`

Returns the number of dead enemy actors.

```csharp
int GetDeadEnemyCount()
```

Returns `0` if the singleton is unavailable.

---

### Actors

#### `GetActiveActor()`

Returns a `GameObj` handle for the currently selected actor.

```csharp
GameObj GetActiveActor()
```

Returns `GameObj.Null` if no actor is active or the singleton is unavailable. Always check `IsNull` before use.

---

#### `SetActiveActor(actor)`

Sets the active actor, ending the current actor's turn.

```csharp
bool SetActiveActor(GameObj actor)
```

| Parameter | Type | Description |
|---|---|---|
| `actor` | `GameObj` | The actor to select. Pass `GameObj.Null` to deselect. |

Returns `false` if the singleton is unavailable.

---

### Flow Control

#### `EndTurn()`

Ends the current player turn via `TacticalState`.

```csharp
bool EndTurn()
```

Returns `false` if the `TacticalState` singleton is unavailable.

---

#### `NextRound()`

Advances the game to the next round by invoking `TacticalManager.NextRound`.

```csharp
bool NextRound()
```

Returns `false` if the singleton is unavailable or the method cannot be resolved.

> **Note:** `TacticalManager.NextRound` is a private method. This call uses raw reflection and may break across game updates.

---

#### `NextFaction()`

Advances to the next faction's turn by invoking `TacticalManager.NextFaction`.

```csharp
bool NextFaction()
```

Returns `false` if the singleton is unavailable or the method cannot be resolved.

> **Note:** `TacticalManager.NextFaction` is a private method. This call uses raw reflection and may break across game updates.

---

#### `SkipAITurn()`

Immediately ends the current AI faction's turn by calling `NextFaction()`. Does nothing and returns `false` if it is currently the player's turn.

```csharp
bool SkipAITurn()
```

---

#### `FinishMission(reason)`

Ends the mission with the specified reason.

```csharp
bool FinishMission(TacticalFinishReason reason = TacticalFinishReason.Leave)
```

| Parameter | Type | Description |
|---|---|---|
| `reason` | `TacticalFinishReason` | Reason for ending the mission. Defaults to `Leave`. |

Returns `false` if the singleton is unavailable.

---

### Spawning

#### `ClearAllEnemies()`

Removes all enemy units from the battlefield immediately. Delegates to `EntitySpawner.ClearEnemies`.

```csharp
int ClearAllEnemies()
```

Returns the number of enemies cleared.

---

### Snapshot

#### `GetTacticalState()`

Aggregates the full tactical state into a single `TacticalStateInfo` snapshot. Internally calls `GetCurrentRound`, `GetCurrentFactionType`, `IsPlayerTurn`, `IsPaused`, `GetTimeScale`, `IsMissionRunning`, `GetActiveActor`, `IsAnyPlayerUnitAlive`, `IsAnyEnemyAlive`, `GetTotalEnemyCount`, and `GetDeadEnemyCount`.

```csharp
TacticalStateInfo GetTacticalState()
```

Never returns `null`. Individual fields fall back to their safe defaults if the underlying call fails.

---

## Error Handling

Most methods delegate error handling to `GameMethod`, which catches exceptions internally and returns safe defaults. Methods using raw reflection (`NextRound`, `NextFaction`) wrap their calls in try/catch and report via `SdkLogger.Error`. Callers always receive a safe fallback rather than a propagated exception.

| Method | Fallback on failure |
|---|---|
| `GetCurrentRound` | `0` |
| `GetCurrentFactionType` | `FactionType.Neutral` |
| `IsPlayerTurn` | `false` |
| `IsPaused` | `false` |
| `SetPaused` | `false` |
| `SetTimeScale` | `true` (always succeeds) |
| `NextRound` | `false` |
| `NextFaction` | `false` |
| `EndTurn` | `false` |
| `GetActiveActor` | `GameObj.Null` |
| `SetActiveActor` | `false` |
| `GetTotalEnemyCount` | `0` |
| `GetDeadEnemyCount` | `0` |
| `IsMissionRunning` | `false` |
| `IsAnyPlayerUnitAlive` | `false` |
| `IsAnyEnemyAlive` | `false` |
| `FinishMission` | `false` |
| `ClearAllEnemies` | `0` |
| `GetTacticalState` | Fields at safe defaults |