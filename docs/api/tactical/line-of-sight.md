# LineOfSight API Reference

`LineOfSight` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's visibility and detection systems and exposes them as safe, exception-handled C# methods. Call these methods any time after `GameState.SceneLoaded` has fired — field handles are resolved automatically on first scene load.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `HasLineOfSight(fromX, fromY, toX, toY)` | `bool` | LOS Checks |
| `HasLineOfSight(fromTile, toTile, flags)` | `bool` | LOS Checks |
| `CanActorSee(actor, target)` | `bool` | LOS Checks |
| `GetVisibilityState(entity)` | `Visibility` | Visibility |
| `IsVisibleToPlayer(entity)` | `bool` | Visibility |
| `IsRevealed(actor)` | `bool` | Visibility |
| `GetVisibilityInfo(entity)` | `VisibilityInfo` | Visibility |
| `GetVision(entity)` | `int` | Stats |
| `GetDetection(entity)` | `int` | Stats |
| `GetConcealment(entity)` | `int` | Stats |
| `GetVisibleTiles(centerX, centerZ, range)` | `List<(int x, int z)>` | Queries |
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
        // LineOfSight resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately
        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull) return;

        var info = LineOfSight.GetVisibilityInfo(actor);
        if (info != null)
            SdkLogger.Msg($"Vision: {info.Vision}, Detection: {info.Detection}, Concealment: {info.Concealment}");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `VisibilityInfo`

A snapshot of an entity's full visibility state, returned by `GetVisibilityInfo()`.

```csharp
public class VisibilityInfo
{
    public Visibility State { get; set; }
    public bool IsVisible { get; set; }
    public bool IsRevealed { get; set; }
    public int Vision { get; set; }
    public int Detection { get; set; }
    public int Concealment { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `State` | `Visibility` | Raw visibility enum value (`Unset`, `Hidden`, `Visible`, etc.) |
| `IsVisible` | `bool` | `true` if `State == Visibility.Visible` |
| `IsRevealed` | `bool` | `true` if the actor has the Revealed flag set (always visible when in range) |
| `Vision` | `int` | Vision range stat from current entity properties |
| `Detection` | `int` | Detection stat from current entity properties |
| `Concealment` | `int` | Concealment stat from current entity properties |

### `LineOfSightFlags`

Passed to tile-based LOS checks to control which blockers are considered. The default value `LineOfSightFlags.Default` is appropriate for most use cases. Refer to the game's `Il2CppTactical` namespace for the full enum definition.

---

## Method Reference

### LOS Checks

#### `HasLineOfSight(fromX, fromY, toX, toY)`

Checks for clear line of sight between two tiles by coordinate. Internally resolves the tiles via `TileMap.GetTile` then delegates to the tile overload.

```csharp
bool HasLineOfSight(int fromX, int fromY, int toX, int toY)
```

| Parameter | Type | Description |
|---|---|---|
| `fromX` | `int` | X coordinate of the source tile |
| `fromY` | `int` | Y coordinate of the source tile |
| `toX` | `int` | X coordinate of the destination tile |
| `toY` | `int` | Y coordinate of the destination tile |

Returns `false` if either tile cannot be resolved. Returns `true` immediately if source and destination are the same tile.

---

#### `HasLineOfSight(fromTile, toTile, flags)`

Checks for clear line of sight between two `GameObj` tile references. Use this overload when you already hold tile handles to avoid the `TileMap.GetTile` lookup.

```csharp
bool HasLineOfSight(GameObj fromTile, GameObj toTile, LineOfSightFlags flags = LineOfSightFlags.Default)
```

| Parameter | Type | Description |
|---|---|---|
| `fromTile` | `GameObj` | Source tile handle |
| `toTile` | `GameObj` | Destination tile handle |
| `flags` | `LineOfSightFlags` | Blocker flags; defaults to `LineOfSightFlags.Default` |

Returns `false` if either handle is null, or if the underlying `Tile.HasLineOfSightTo` call throws.

---

#### `CanActorSee(actor, target)`

Checks whether an actor has line of sight to a target entity, factoring in detection versus concealment. Calls `Actor.HasLineOfSightTo` with `wasDetected = false` and no tile overrides.

```csharp
bool CanActorSee(GameObj actor, GameObj target)
```

| Parameter | Type | Description |
|---|---|---|
| `actor` | `GameObj` | The observing actor |
| `target` | `GameObj` | The entity being checked |

Returns `false` if either handle is null or the call fails.

> **Note:** This is a full detection check, not a raw geometry check. An actor with zero Detection may fail even if the tiles have clear LOS. Use `HasLineOfSight` if you only need geometry.

---

### Visibility

#### `GetVisibilityState(entity)`

Reads the `VisibilityToPlayer` field from the entity via a pre-resolved field handle.

```csharp
Visibility GetVisibilityState(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to query |

Returns `Visibility.Unset` if the handle is null.

---

#### `IsVisibleToPlayer(entity)`

Convenience wrapper around `GetVisibilityState`. Returns `true` only when the state is exactly `Visibility.Visible`.

```csharp
bool IsVisibleToPlayer(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to query |

---

#### `IsRevealed(actor)`

Reads the `Revealed` boolean field from an actor. Revealed actors are always visible to the player when within range, regardless of concealment.

```csharp
bool IsRevealed(GameObj actor)
```

| Parameter | Type | Description |
|---|---|---|
| `actor` | `GameObj` | The actor to query |

Returns `false` if the handle is null.

---

#### `GetVisibilityInfo(entity)`

Aggregates all visibility-related state into a single `VisibilityInfo` snapshot. Internally calls `GetVisibilityState`, `IsRevealed`, `GetVision`, `GetDetection`, and `GetConcealment`.

```csharp
VisibilityInfo GetVisibilityInfo(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to query |

Returns `null` if the handle is null or an exception is thrown during aggregation.

---

### Stats

The three stat methods below share the same internal pattern: call `Entity.GetCurrentProperties()`, wrap the returned object as a `GameObj`, then invoke the corresponding method on `EntityProperties`. All return `0` on failure.

#### `GetVision(entity)`

Returns the entity's current vision range.

```csharp
int GetVision(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to query |

---

#### `GetDetection(entity)`

Returns the entity's current detection stat. Detection is compared against a target's Concealment to determine whether `CanActorSee` succeeds.

```csharp
int GetDetection(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to query |

---

#### `GetConcealment(entity)`

Returns the entity's current concealment stat.

```csharp
int GetConcealment(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to query |

---

### Queries

#### `GetVisibleTiles(centerX, centerZ, range)`

Returns a list of all tile coordinates visible from a given position within a radius. Performs a circular bounds check (`dx² + dz² ≤ range²`) before calling `HasLineOfSight`, so tiles at the corners of the bounding square are excluded.

```csharp
List<(int x, int z)> GetVisibleTiles(int centerX, int centerZ, int range)
```

| Parameter | Type | Description |
|---|---|---|
| `centerX` | `int` | X coordinate of the origin tile |
| `centerZ` | `int` | Z coordinate of the origin tile |
| `range` | `int` | Radius in tiles |

Returns an empty list if the center tile cannot be resolved or if `TileMap.GetMapInfo()` is unavailable. The list is clamped to map bounds automatically.

> **Performance:** This method calls `HasLineOfSight` once per candidate tile. For large ranges on big maps the call count grows as O(range²). Cache the result if you need it across multiple frames.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `los` | `<x1> <y1> <x2> <y2>` | Check LOS between two tiles; prints Clear/Blocked and distance |
| `visibility` | *(none)* | Print full `VisibilityInfo` for the currently selected actor |
| `vision` | *(none)* | Print Vision, Detection, and Concealment for the selected actor |
| `cansee` | `<target_name>` | Check whether the selected actor can see a named target |
| `visibletiles` | `<range>` | Count tiles visible from the selected actor within the given range (default: 10) |

Example session:

```
> los 3 4 7 9
LOS from (3,4) to (7,9): Clear
Distance: 6.4

> visibility
Visibility State: Visible
Is Visible: True, Revealed: False
Vision: 8, Detection: 4
Concealment: 0

> cansee Enemy_Rifleman
Can see 'Enemy_Rifleman': True
```

---

## Error Handling

All public methods catch exceptions internally and report them via `ModError.ReportInternal`. Callers receive a safe fallback value (`false`, `0`, `null`, or an empty list) rather than a propagated exception. If a method consistently returns its fallback value, check the mod error log for the corresponding `LineOfSight.*` entry.

| Method | Fallback on error |
|---|---|
| `HasLineOfSight` (both overloads) | `false` |
| `CanActorSee` | `false` |
| `GetVisibilityState` | `Visibility.Unset` |
| `IsVisibleToPlayer` | `false` |
| `IsRevealed` | `false` |
| `GetVisibilityInfo` | `null` |
| `GetVision` | `0` |
| `GetDetection` | `0` |
| `GetConcealment` | `0` |
| `GetVisibleTiles` | `[]` (empty list) |
