# TileMap

`TileMap` is a static partial class in the `Menace.SDK` namespace. It wraps IL2CPP interop calls against `Map`, `Tile`, and `TacticalManager` and exposes them as safe, exception-handled C# methods. Field handles are resolved once on scene load — call sites never touch raw memory directly.

---

## Coordinate System

The game uses **X/Z** for horizontal tile position. **Y** is elevation (height), not a grid axis.

```
TileInfo.X  →  game's X coordinate (horizontal)
TileInfo.Z  →  game's Z coordinate (horizontal depth)
Elevation   →  game's Y axis
```

> All coordinate parameters in this document follow the `(int x, int z)` convention.

---

## Constants

| Constant | Value | Description |
|---|---|---|
| `TileMap.MAX_MAP_SIZE` | `42` | Maximum tiles per axis (map is at most 42×42) |
| `TileMap.TILE_SIZE` | `8.0f` | World-space size of one tile in Unity units |

---

## Quick Reference

### Map Methods

| Method | Returns | Description |
|---|---|---|
| `GetMap()` | `GameObj` | The current tactical map object |
| `GetMapInfo()` | `MapInfo` | Dimensions, fog of war state, and center position |
| `IsValidTile(x, z)` | `bool` | Native validity check (instance) |
| `IsInBounds(x, z)` | `bool` | Static bounds check |
| `ForEachTile(action)` | `void` | Iterate every tile on the map |
| `QueryTilesInside(area)` | `List<GameObj>` | All tiles inside a rect (default filters) |
| `QueryTilesInside(area, emptyOnly, nonBlockedOnly, nonIsolatedOnly)` | `List<GameObj>` | All tiles inside a rect with explicit filters |

### Tile Retrieval

| Method | Returns | Description |
|---|---|---|
| `GetTile(x, z)` | `GameObj` | Tile at grid coordinates |
| `GetTileAt(x, z)` | `GameObj` | Alias for `GetTile` |
| `GetTileAtWorldPos(worldPos)` | `GameObj` | Tile at a Unity world position |
| `GetTileInfo(x, z)` | `TileInfo` | Full tile snapshot by coordinates |
| `GetTileInfo(tile)` | `TileInfo` | Full tile snapshot from a `GameObj` |

### Tile State

| Method | Returns | Description |
|---|---|---|
| `IsBlocked(x, z)` | `bool` | Tile is impassable |
| `IsBlocked(tile)` | `bool` | |
| `IsEmpty(x, z)` | `bool` | No actor and no structure |
| `IsEmpty(tile)` | `bool` | |
| `HasActor(x, z)` | `bool` | A living actor occupies the tile |
| `HasActor(tile)` | `bool` | |
| `GetActorOnTile(x, z)` | `GameObj` | The actor on the tile, or `GameObj.Null` |
| `GetActorOnTile(tile)` | `GameObj` | |
| `IsValidMovementDestination(x, z)` | `bool` | Valid destination for movement |
| `IsValidMovementDestination(tile)` | `bool` | |
| `CanBeEntered(x, z)` | `bool` | Tile can be entered by any actor |
| `CanBeEntered(tile)` | `bool` | |

### Visibility

| Method | Returns | Description |
|---|---|---|
| `IsVisibleToPlayer(x, z)` | `bool` | Tile is visible to the player faction |
| `IsVisibleToPlayer(tile)` | `bool` | |
| `IsVisibleToFaction(x, z, factionId)` | `bool` | Tile is visible to a specific faction |
| `IsVisibleToFaction(tile, factionId)` | `bool` | |

### Cover

| Method | Returns | Description |
|---|---|---|
| `GetCover(x, z, direction)` | `int` | Cover value in one direction (`Cover.*` constant) |
| `GetCover(tile, direction)` | `int` | |
| `GetAllCover(x, z)` | `int[8]` | Cover values in all 8 directions |
| `GetAllCover(tile)` | `int[8]` | |

### Neighbors & Geometry

| Method | Returns | Description |
|---|---|---|
| `GetNeighbor(x, z, direction)` | `GameObj` | Adjacent tile in a direction |
| `GetNeighbor(tile, direction)` | `GameObj` | |
| `GetAllNeighbors(x, z)` | `GameObj[8]` | All 8 adjacent tiles |
| `GetAllNeighbors(tile)` | `GameObj[8]` | |
| `GetValidNeighbors(tile)` | `List<GameObj>` | Adjacent tiles that are not null |
| `IsDirectNeighbor(x1, z1, x2, z2)` | `bool` | Two tiles share an edge or corner |
| `IsDirectNeighbor(tile, other)` | `bool` | |
| `GetDirectionTo(fromX, fromZ, toX, toZ)` | `int` | Direction index from one tile to another |
| `GetDirectionTo(fromTile, toTile)` | `int` | |
| `GetDistance(x1, z1, x2, z2)` | `int` | Game's native tile distance |
| `GetDistance(tile1, tile2)` | `int` | |
| `GetManhattanDistance(x1, z1, x2, z2)` | `int` | Manhattan distance |
| `GetManhattanDistance(tile1, tile2)` | `int` | |
| `TileToWorld(x, z, elevation)` | `Vector3` | Grid → world-space center position |
| `WorldToTile(worldPos)` | `(int x, int z)` | World-space → grid coordinates |

### Helpers

| Method | Returns | Description |
|---|---|---|
| `GetDirectionName(direction)` | `string` | Human-readable name for a direction index |
| `GetCoverName(coverType)` | `string` | Human-readable name for a cover value |

---

## Constants Classes

### `TileMap.Dir`

Direction constants for cover and neighbor queries. Values map directly to `Il2CppMenace.Tactical.Direction`.

| Constant | Value |
|---|---|
| `Dir.North` | 0 |
| `Dir.Northeast` | 1 |
| `Dir.East` | 2 |
| `Dir.Southeast` | 3 |
| `Dir.South` | 4 |
| `Dir.Southwest` | 5 |
| `Dir.West` | 6 |
| `Dir.Northwest` | 7 |
| `Dir.Count` | 8 |

### `TileMap.Cover`

Cover tier constants for interpreting `GetCover` return values.

| Constant | Value |
|---|---|
| `Cover.None` | 0 |
| `Cover.Light` | 1 |
| `Cover.Medium` | 2 |
| `Cover.Heavy` | 3 |

---

## Data Structures

### `TileMap.TileInfo`

A snapshot of a single tile's state at query time. Returned by `GetTileInfo`.

| Property | Type | Description |
|---|---|---|
| `X` | `int` | Game's X coordinate |
| `Z` | `int` | Game's Z coordinate (horizontal depth) |
| `Elevation` | `float` | Height (game's Y axis) |
| `IsBlocked` | `bool` | Tile is impassable |
| `IsIsolated` | `bool` | Unreachable from the main connected area |
| `IsTemporarilyOccupied` | `bool` | Reserved by a moving actor |
| `IsLOSBlockedByHalfcover` | `bool` | Line of sight broken by a half-cover object |
| `IsInFogOfWar` | `bool` | Inside fog of war |
| `HasActor` | `bool` | A living actor is present |
| `HasStructure` | `bool` | A structure is present |
| `ActorName` | `string` | Name of the actor on the tile, or `null` |
| `CoverValues` | `int[8]` | Cover per direction; index with `Dir.*` constants |
| `InherentCover` | `int` | Cover inherent to the tile regardless of direction |
| `HasCover` | `bool` | Any cover in any direction |
| `HasHalfCover` | `bool` | Any half-cover present |
| `IsVisibleToPlayer` | `bool` | Visible to the player faction |
| `VisibleMask` | `ulong` | Raw bitmask — one bit per faction ID |
| `HasEffects` | `bool` | One or more active tile effects |
| `Concealment` | `int` | Concealment value of the tile |
| `WorldPos` | `Vector3` | World-space center of the tile |
| `Pointer` | `IntPtr` | Raw native pointer to the `Tile` object |

---

### `TileMap.MapInfo`

A snapshot of the current map's state at query time. Returned by `GetMapInfo`.

| Property | Type | Description |
|---|---|---|
| `SizeX` | `int` | Map width in tiles (X axis) |
| `SizeZ` | `int` | Map depth in tiles (Z axis) |
| `IsUsingFogOfWar` | `bool` | Fog of war is active |
| `IsReady` | `bool` | Map has finished generating and is ready for queries |
| `CenterWorldPos` | `Vector3` | World-space position of the map center |
| `Pointer` | `IntPtr` | Raw native pointer to the `Map` object |

---

## Quick Start

```csharp
using Menace.SDK;
using MelonLoader;
using HarmonyLib;

namespace MyPlugin;

public class MyPlugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        // TileMap resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        var info = TileMap.GetMapInfo();
        if (info == null || !info.IsReady) return;

        SdkLogger.Msg($"Map loaded: {info.SizeX}x{info.SizeZ}, FoW={info.IsUsingFogOfWar}");

        // Inspect a tile
        var tileInfo = TileMap.GetTileInfo(3, 7);
        if (tileInfo != null)
        {
            SdkLogger.Msg($"Tile (3,7): blocked={tileInfo.IsBlocked}, " +
                          $"cover N={TileMap.GetCoverName(tileInfo.CoverValues[TileMap.Dir.North])}");
        }
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Method Reference

### Map

#### `GetMap`
Returns the current tactical map as an untyped `GameObj`. Returns `GameObj.Null` if no map is loaded.

```csharp
GameObj map = TileMap.GetMap();
```

---

#### `GetMapInfo`
Returns a `MapInfo` snapshot with dimensions, fog of war state, ready flag, and world-space center. Returns `null` if no map is loaded.

```csharp
MapInfo info = TileMap.GetMapInfo();
if (info != null && info.IsReady)
    SdkLogger.Msg($"{info.SizeX}x{info.SizeZ}");
```

---

#### `IsValidTile`
Instance method — calls the native `Map.IsValidTile` on the current map.

```csharp
bool valid = TileMap.IsValidTile(x, z);
```

---

#### `IsInBounds`
Static method — calls `Map.IsInBounds` without fetching the map instance. Slightly cheaper than `IsValidTile` when you only need a bounds check.

```csharp
bool inBounds = TileMap.IsInBounds(x, z);
```

---

#### `ForEachTile`
Executes an `Action<GameObj>` on every tile in the current map. Skips null tiles silently.

```csharp
TileMap.ForEachTile(tile =>
{
    if (TileMap.HasActor(tile))
        SdkLogger.Msg($"Actor at {tile.GetName()}");
});
```

---

#### `QueryTilesInside`
Returns all tiles within a `RectInt` area. The default overload applies `nonBlockedOnly: true` and `nonIsolatedOnly: true`.

```csharp
// Default filters: non-blocked, non-isolated, any occupancy
List<GameObj> tiles = TileMap.QueryTilesInside(new RectInt(0, 0, 10, 10));

// Custom filters: only empty tiles, no blocked/isolated filter
List<GameObj> empty = TileMap.QueryTilesInside(new RectInt(0, 0, 10, 10),
    emptyOnly: true, nonBlockedOnly: false, nonIsolatedOnly: false);
```

---

### Tile Retrieval

#### `GetTile` / `GetTileAt`
Fetches a tile at grid coordinates. `GetTileAt` is a direct alias.

```csharp
GameObj tile = TileMap.GetTile(5, 12);
```

---

#### `GetTileAtWorldPos`
Resolves a world-space `Vector3` to the tile beneath it using `Map.GetTileAtPos`.

```csharp
GameObj tile = TileMap.GetTileAtWorldPos(actor.transform.position);
```

---

#### `GetTileInfo`
Returns a full `TileInfo` snapshot. Accepts coordinates or a `GameObj`. Returns `null` on failure.

```csharp
TileInfo info = TileMap.GetTileInfo(5, 12);
if (info != null)
    SdkLogger.Msg($"Elevation: {info.Elevation:F1}, Concealment: {info.Concealment}");
```

---

### Tile State

#### `IsBlocked`
Returns `true` if the tile is impassable. Returns `true` on null tile (safe default).

```csharp
if (!TileMap.IsBlocked(x, z))
    // tile is passable
```

---

#### `IsEmpty`
Returns `true` if the tile has no actor and no structure.

```csharp
if (TileMap.IsEmpty(tile)) { }
```

---

#### `HasActor`
Returns `true` if a living actor occupies the tile.

```csharp
if (TileMap.HasActor(5, 12))
    SdkLogger.Msg("Tile occupied");
```

---

#### `GetActorOnTile`
Returns the `GameObj` of the actor on the tile, or `GameObj.Null` if none.

```csharp
GameObj actor = TileMap.GetActorOnTile(5, 12);
if (!actor.IsNull)
    SdkLogger.Msg(actor.GetName());
```

---

#### `IsValidMovementDestination`
Returns `true` if the tile is a legal movement target (not blocked, not isolated, not temporarily occupied).

```csharp
if (TileMap.IsValidMovementDestination(x, z)) { }
```

---

#### `CanBeEntered`
Returns `true` if the tile can be entered by any actor.

```csharp
if (TileMap.CanBeEntered(tile)) { }
```

---

### Visibility

#### `IsVisibleToPlayer`
Returns `true` if the tile is currently visible to the player faction.

```csharp
bool seen = TileMap.IsVisibleToPlayer(5, 12);
```

---

#### `IsVisibleToFaction`
Returns `true` if the tile is visible to the given faction ID.

```csharp
bool enemySees = TileMap.IsVisibleToFaction(5, 12, factionId: 1);
```

---

### Cover

#### `GetCover`
Returns the cover value (`Cover.None` through `Cover.Heavy`) on a tile in one direction.

```csharp
int cover = TileMap.GetCover(5, 12, TileMap.Dir.North);
SdkLogger.Msg(TileMap.GetCoverName(cover)); // "Heavy", "Light", etc.
```

---

#### `GetAllCover`
Returns an 8-element array of cover values, indexed by `Dir.*` constants.

```csharp
int[] cover = TileMap.GetAllCover(5, 12);
for (int dir = 0; dir < TileMap.Dir.Count; dir++)
    SdkLogger.Msg($"{TileMap.GetDirectionName(dir)}: {TileMap.GetCoverName(cover[dir])}");
```

---

### Neighbors & Geometry

#### `GetNeighbor`
Returns the adjacent tile in a cardinal or diagonal direction. Returns `GameObj.Null` at map edges.

```csharp
GameObj north = TileMap.GetNeighbor(5, 12, TileMap.Dir.North);
```

---

#### `GetAllNeighbors`
Returns all 8 adjacent tiles as an array. Slots at map edges will be `GameObj.Null`.

```csharp
GameObj[] neighbors = TileMap.GetAllNeighbors(5, 12);
```

---

#### `GetValidNeighbors`
Returns only the non-null neighbors as a `List<GameObj>`. Prefer this over `GetAllNeighbors` when iterating.

```csharp
List<GameObj> reachable = TileMap.GetValidNeighbors(tile);
```

---

#### `IsDirectNeighbor`
Returns `true` if two tiles share an edge or corner.

```csharp
bool adjacent = TileMap.IsDirectNeighbor(5, 12, 5, 13);
```

---

#### `GetDirectionTo`
Returns the direction index (a `Dir.*` value) from one tile toward another, or `-1` on failure.

```csharp
int dir = TileMap.GetDirectionTo(5, 12, 8, 15);
SdkLogger.Msg(TileMap.GetDirectionName(dir)); // e.g. "Southeast"
```

---

#### `GetDistance`
Returns the native game distance between two tiles in tile units. Returns `-1` on failure.

```csharp
int dist = TileMap.GetDistance(0, 0, 5, 5);
```

---

#### `GetManhattanDistance`
Returns the Manhattan (rectilinear) distance between two tiles. Returns `-1` on failure.

```csharp
int manhattan = TileMap.GetManhattanDistance(0, 0, 5, 5);
```

---

#### `TileToWorld`
Converts tile grid coordinates to the world-space center of that tile.

```csharp
Vector3 center = TileMap.TileToWorld(5, 12);
Vector3 elevated = TileMap.TileToWorld(5, 12, elevation: 2.5f);
```

Returns `(x * 8 + 4, elevation, z * 8 + 4)`.

---

#### `WorldToTile`
Converts a world-space position to tile grid coordinates. Truncates — does not round.

```csharp
var (tx, tz) = TileMap.WorldToTile(actor.transform.position);
```

---

## Dev Console Commands

`TileMap` registers the following commands via `TileMap.RegisterConsoleCommands()`, which is called automatically during `DevConsole` initialization.

| Command | Usage | Description |
|---|---|---|
| `tile` | `tile <x> <z>` | Print full tile info for the given coordinates |
| `cover` | `cover <x> <z>` | Print cover values in all 8 directions |
| `mapinfo` | `mapinfo` | Print current map dimensions and state |
| `blocked` | `blocked <x> <z>` | Check if a tile is blocked |
| `visible` | `visible <x> <z>` | Check if a tile is visible to the player |
| `dist` | `dist <x1> <z1> <x2> <z2>` | Print distance, Manhattan distance, and direction between two tiles |
| `whostile` | `whostile <x> <z>` | Print the name of the actor occupying a tile |

---

## Notes

**Field handles are resolved lazily on first scene load.** Calling any `TileMap` method before `GameState.SceneLoaded` has fired will return safe defaults (`null`, `GameObj.Null`, `false`, `-1`) rather than throwing.

**All methods are exception-safe.** Errors are reported through `ModError.ReportInternal` and return safe defaults — no call site needs a try/catch.

**Overload pattern.** Most methods come in two forms: `(int x, int z)` which fetches the tile internally, and `(GameObj tile)` which works on an already-resolved object. Use the `GameObj` overloads when you are processing tiles in a loop (e.g. inside `ForEachTile` or after `QueryTilesInside`) to avoid redundant map lookups.

**`TileInfo` and `MapInfo` are snapshots.** They reflect the game state at the moment of the call. Store or cache them only for the duration of a single frame or event handler.
