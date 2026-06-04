# EntitySpawner API Reference

`EntitySpawner` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's tactical spawning and entity management systems and exposes them as safe, exception-handled C# methods. Call these methods any time after `GameState.SceneLoaded` has fired — the `TacticalManager` singleton and tile map are resolved at call time.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `SpawnUnit(templateId, tileX, tileZ, faction)` | `SpawnResult` | Spawning |
| `SpawnGroup(templateId, positions, faction)` | `List<SpawnResult>` | Spawning |
| `DestroyEntity(entity, immediate)` | `bool` | Destruction |
| `ClearEnemies(immediate, faction)` | `int` | Destruction |
| `ListEntities(factionFilter)` | `GameObj[]` | Queries |
| `GetEntityInfo(entity)` | `EntityInfo` | Queries |

---

## Quick Start

```csharp
using MelonLoader;
using Menace.SDK;
using Il2CppMenace.Tactical;

namespace MyPlugin;

public class MyPlugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony) { }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Spawn a hostile unit at tile (5, 3)
        var result = EntitySpawner.SpawnUnit("unit_rifleman_01", 5, 3, FactionType.EnemyLocalForces);

        if (result.Success)
        {
            var info = EntitySpawner.GetEntityInfo(result.Entity);
            SdkLogger.Msg($"Spawned '{info.Name}' (ID {info.EntityId}) at (5, 3)");
        }
        else
        {
            SdkLogger.Warn($"Spawn failed: {result.Error}");
        }

        // Spawn a patrol group along a line of tiles
        var positions = new List<(int x, int z)> { (2, 4), (3, 4), (4, 4) };
        var groupResults = EntitySpawner.SpawnGroup("unit_scout_01", positions, FactionType.EnemyLocalForces);

        int spawned = groupResults.Count(r => r.Success);
        SdkLogger.Msg($"Patrol spawned: {spawned}/{positions.Count} units placed");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `SpawnResult`

Returned by `SpawnUnit` and `SpawnGroup` to indicate whether a spawn succeeded and, if so, provide a handle to the spawned entity.

```csharp
public class SpawnResult
{
    public bool Success { get; set; }
    public GameObj Entity { get; set; }
    public string Error { get; set; }

    public static SpawnResult Failed(string error);
    public static SpawnResult Ok(GameObj entity);
}
```

| Property | Type | Description |
|---|---|---|
| `Success` | `bool` | `true` if the unit was spawned successfully |
| `Entity` | `GameObj` | Handle to the spawned actor; only valid when `Success` is `true` |
| `Error` | `string` | Human-readable failure reason; `null` when `Success` is `true` |

Always check `Success` before using `Entity`. The entity handle is not guaranteed to remain valid if the actor is destroyed later in the same frame.

---

### `EntityInfo`

A snapshot of an entity's identity and status fields, returned by `GetEntityInfo`. Values are read directly from IL2CPP field offsets and reflect state at the moment of the call.

```csharp
public class EntityInfo
{
    public int EntityId { get; set; }
    public string Name { get; set; }
    public string TypeName { get; set; }
    public int FactionId { get; set; }
    public bool IsAlive { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `EntityId` | `int` | Unique entity ID (`<ID>k__BackingField`, offset `0x10`) |
| `Name` | `string` | Display name from `GetName()`, falling back to `DebugName` (offset `0x88`) |
| `TypeName` | `string` | IL2CPP type name of the entity object |
| `FactionId` | `int` | Raw faction ID from `m_FactionID` (offset `0x4C`) |
| `IsAlive` | `bool` | `m_IsAlive` flag (offset `0x48`); `false` for dead or dying actors |
| `Pointer` | `IntPtr` | Raw IL2CPP pointer; use for low-level interop only |

---

## Method Reference

### Spawning

#### `SpawnUnit(templateId, tileX, tileZ, faction)`

Spawns a single unit from an `EntityTemplate` onto the given tile. The tile must exist on the current map and must not already be occupied by another actor.

```csharp
SpawnResult SpawnUnit(string templateId, int tileX, int tileZ, FactionType faction = FactionType.Neutral)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | The `m_ID` of the `EntityTemplate` to spawn |
| `tileX` | `int` | X coordinate of the target tile |
| `tileZ` | `int` | Z coordinate of the target tile |
| `faction` | `FactionType` | Faction to assign to the spawned unit; defaults to `Neutral` |

Returns a `SpawnResult` with `Success = false` if the template is not found, the tile does not exist, the tile is already occupied, `TacticalManager` is unavailable, or `TrySpawnUnit` returns false. The `Error` property will contain a descriptive reason in each case.

> **Note:** Template IDs are the `m_ID` field on `EntityTemplate` assets, not display names or prefab names. Verify IDs against the game's template registry before calling.

---

#### `SpawnGroup(templateId, positions, faction)`

Spawns multiple units of the same template across a list of tile coordinates. Each position is attempted independently — a failure at one tile does not prevent spawning at the others.

```csharp
List<SpawnResult> SpawnGroup(string templateId, List<(int x, int z)> positions, FactionType faction = FactionType.Neutral)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | The `m_ID` of the `EntityTemplate` to spawn |
| `positions` | `List<(int x, int z)>` | Ordered list of tile coordinates to spawn at |
| `faction` | `FactionType` | Faction to assign to all spawned units; defaults to `Neutral` |

Returns a list of `SpawnResult` values in the same order as `positions`. Check each result's `Success` property individually. Returns an empty list if `positions` is empty.

> **Performance:** Each position resolves its tile and calls `TrySpawnUnit` separately. For large groups, prefer pre-filtering positions to verified unoccupied tiles using `ListEntities` before calling.

---

### Destruction

#### `DestroyEntity(entity, immediate)`

Kills an entity by calling `Actor.Die`. Has no effect if the entity is already dead.

```csharp
bool DestroyEntity(GameObj entity, bool immediate = false)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to destroy |
| `immediate` | `bool` | If `true`, skips the death animation; defaults to `false` |

Returns `true` if the `Die` call succeeded. Returns `false` if the entity handle is null, the entity is not alive, or an exception is thrown.

---

#### `ClearEnemies(immediate, faction)`

Destroys all living actors belonging to the specified faction. Useful for resetting map state during testing or scripted scenarios.

```csharp
int ClearEnemies(bool immediate = true, FactionType faction = FactionType.EnemyLocalForces)
```

| Parameter | Type | Description |
|---|---|---|
| `immediate` | `bool` | If `true`, skips death animations for all cleared actors; defaults to `true` |
| `faction` | `FactionType` | The faction to clear; defaults to `EnemyLocalForces` |

Returns the number of actors successfully destroyed. Actors that are already dead or whose `DestroyEntity` call fails are not counted.

---

### Queries

#### `ListEntities(factionFilter)`

Returns all actors currently registered on the tactical map, optionally filtered by faction.

```csharp
GameObj[] ListEntities(FactionType? factionFilter = null)
```

| Parameter | Type | Description |
|---|---|---|
| `factionFilter` | `FactionType?` | If set, only actors belonging to this faction are returned; pass `null` for all factions |

Returns an empty array if `TacticalManager` is unavailable, if no factions are registered, or if an exception is thrown. Dead actors may be included if they are still registered with their faction; check `EntityInfo.IsAlive` if alive-only results are required.

---

#### `GetEntityInfo(entity)`

Reads identity and status fields from an entity into an `EntityInfo` snapshot. All values are read from IL2CPP field offsets and are accurate at the time of the call.

```csharp
EntityInfo GetEntityInfo(GameObj entity)
```

| Parameter | Type | Description |
|---|---|---|
| `entity` | `GameObj` | The entity to query |

Returns `null` if the handle is null or if an exception is thrown during field reads. Check for `null` before accessing properties.

---

## Error Handling

All public methods catch exceptions internally and report them via `SdkLogger.Error`. Callers receive a safe fallback value rather than a propagated exception. If a method consistently returns its fallback, check the mod error log for the corresponding `EntitySpawner.*` entry.

| Method | Fallback on error |
|---|---|
| `SpawnUnit` | `SpawnResult` with `Success = false` and `Error` set |
| `SpawnGroup` | Per-entry `SpawnResult` with `Success = false`; never throws |
| `DestroyEntity` | `false` |
| `ClearEnemies` | `0` (partial count of any actors destroyed before the error) |
| `ListEntities` | `[]` (empty array) |
| `GetEntityInfo` | `null` |
