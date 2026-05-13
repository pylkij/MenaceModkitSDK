# GameQuery API Reference

`GameQuery` is a static class in the `Menace.SDK` namespace. It discovers live game objects by IL2CPP proxy type using `Resources.FindObjectsOfTypeAll`, with optional per-scene caching. Type parameters are resolved at compile time, so typos in type names surface as compiler errors rather than silent runtime misses.

---

## How It Works

`GameQuery` uses IL2CPP interop types directly as type parameters. You pass the IL2CPP proxy class — generated from the dump — and the method returns instances of that type, ready to use without pointer casts or `GameObj` wrappers.

```csharp
// Returns a typed Actor directly — no casting needed
var actor = GameQuery.FindByName<Actor>("Sergeant_Kane");
```

On failure, all methods return `null` or an empty array rather than throwing.

---

## Finding All Objects

### `FindAll<T>`

```csharp
T[] FindAll<T>() where T : Il2CppObjectBase
```

Returns all live instances of the given type. Returns an empty array if none are found or if the type is unavailable.

```csharp
var allActors = GameQuery.FindAll<Actor>();
var allTemplates = GameQuery.FindAll<EntityTemplate>();
```

---

### `FindAllCached<T>`

```csharp
T[] FindAllCached<T>() where T : Il2CppObjectBase
```

Cached variant of `FindAll<T>`. Results are stored until `ClearCache` is called on scene load. Use this for types that are queried frequently within a scene and don't change, such as templates and static data objects.

```csharp
var templates = GameQuery.FindAllCached<PerkTemplate>();
```

> **Note:** Do not use `FindAllCached<T>` for live entities like `Actor` or `Tile` — their population changes during a mission. Use it for templates and other scene-stable objects.

---

## Finding by Name

### `FindByName<T>`

```csharp
T FindByName<T>(string name) where T : Il2CppObjectBase
```

Finds the first object of the given type whose Unity object name matches `name`. Returns `null` if not found. Name matching is exact and case-sensitive.

```csharp
var faction = GameQuery.FindByName<AIFaction>("PlayerFaction");
var perk = GameQuery.FindByName<PerkTemplate>("Sharpshooter");
```

Check for `null` before using the result:

```csharp
var template = GameQuery.FindByName<EntityTemplate>("Rifleman_T1");
if (template == null)
    return "Template not found";

var cost = GetEntityCost(new GameObj(template.Pointer));
```

---

## Passing Results to Legacy APIs

Some game methods still expect a `GameObj`. Wrap the typed result via its pointer:

```csharp
var template = GameQuery.FindByName<ArmyTemplate>("PlayerArmy");
if (template == null) return;

var entries = GetArmyTemplateEntries(new GameObj(template.Pointer));
```

Going the other direction — from `GameObj` to a typed instance — uses the same pattern:

```csharp
var actor = new Actor(TacticalController.GetActiveActor().Pointer);
```

> **Note:** Wrapping a null or sentinel `GameObj` via `new T(...Pointer)` produces a non-null wrapper with a zero pointer — it will not compare equal to `null`. If the source is a `GameObj`, check `.IsNull` before wrapping.

---

## Cache Lifecycle

The per-scene cache is cleared automatically on scene load via `ClearCache`. You do not need to call this manually. If you add a new scene load hook, call it from `ModpackLoaderMod.OnSceneWasLoaded`:

```csharp
GameQuery.ClearCache();
```

---

## Null and Error Handling

`GameQuery` never throws. All failures are routed to `ModError.ReportInternal`. The call site receives a safe default:

| Method | Value on failure |
|---|---|
| `FindAll<T>` | `Array.Empty<T>()` |
| `FindAllCached<T>` | `Array.Empty<T>()` |
| `FindByName<T>` | `null` |

---

## Method Reference

| Method | Returns | Cached | Notes |
|---|---|---|---|
| `FindAll<T>` | `T[]` | No | All live instances of type |
| `FindAllCached<T>` | `T[]` | Yes | Use for scene-stable types only |
| `FindByName<T>` | `T` or `null` | No | Exact, case-sensitive name match |
