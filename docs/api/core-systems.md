# Core Systems Reference

`Menace.SDK` provides a set of foundational systems for safely reading, writing, and interacting with IL2CPP game objects. Every higher-level API feature is built on these primitives. This document covers when to use each system, how they relate to each other, and the traps that will burn you if you assume managed C# rules apply here.

---

## The Object Model: Why Nothing Is Just a Pointer

IL2CPP objects exist in native (unmanaged) memory. The IL2CppInterop layer gives you *proxy* objects — managed C# wrappers that hold a `Pointer` (`IntPtr`) into that native memory. This has two important consequences:

**1. Null-checking is not enough.** A pointer can be non-zero and still point to a destroyed object. `someProxy != null` only tells you the C# wrapper exists. It says nothing about whether the native object is alive. This is the most common source of access violations in IL2CPP modding.

**2. You need the SDK's liveness check.** `GameObj.CheckAlive()` reads `m_CachedPtr` from unmanaged memory to confirm the native Unity object is still live. Use it before doing anything meaningful with a pointer.

---

## `GameObj` — The Safe Untyped Handle

**File:** `GameObj.cs`

`GameObj` is a lightweight `readonly struct` wrapping an `IntPtr`. It is the currency of the SDK: event hooks give you `IntPtr` parameters, and `GameObj` is how you work with them safely.

```csharp
var actor = new GameObj(actorPtr);
if (actor.IsNull) return;                          // pointer is zero
if (actor.CheckAlive() != AliveStatus.Alive) return; // native object destroyed
```

### IsNull vs CheckAlive — The Core Trap

| Check | What it tests | When it passes | Use for |
|---|---|---|---|
| `IsNull` | `Pointer == IntPtr.Zero` | Pointer has a value at all | Guard against null pointers before any read |
| `CheckAlive()` | Reads `m_CachedPtr` from native memory | Native Unity object is still alive | Guard before reads/writes on event-delivered pointers |

**Always check both, in order.** `CheckAlive()` will fault if the pointer is zero, so `IsNull` must come first.

`CheckAlive()` returns an `AliveStatus` enum:

| Value | Meaning |
|---|---|
| `Alive` | `m_CachedPtr` is non-zero — safe to proceed |
| `Dead` | `m_CachedPtr` is zero — object has been destroyed |
| `Unknown` | The `m_CachedPtr` offset isn't available yet — can't confirm either way |

`Unknown` is rare but possible during very early init or after certain scene transitions. Treat it as `Dead` unless you have a specific reason not to.

### Reading and Writing Fields

`GameObj` provides offset-based read/write methods for all primitive types. These are the low-level path — prefer `FieldHandle` (see below) for new code.

```csharp
int hp       = obj.ReadInt(hpOffset);
float pct    = obj.ReadFloat(pctOffset);
bool isDead  = obj.ReadBool(deadFlagOffset);
string name  = obj.ReadString(nameOffset);
GameObj sub  = obj.ReadObj(subObjectOffset);
```

Read methods return safe defaults on failure (`0`, `0f`, `false`, `null`, `GameObj.Null`) and never throw. Write methods (`WriteInt`, `WriteFloat`, `WriteBool`, `WritePtr`) **do** throw `GameObjException` if the pointer is null or the offset is zero — call `CheckAlive()` first.

### Type Operations

```csharp
// Exact type match only — does not match subclasses
bool isExact = obj.IsType<SomeIl2CppType>();

// Matches T and any subclass of T
bool isAssignable = obj.IsAssignableTo<SomeIl2CppType>();

// Convert to a managed IL2CppInterop proxy
var proxy = obj.As<SomeIl2CppType>();
```

Both `IsType` and `IsAssignableTo` throw `GameObjException` — not return false — if the type cannot be resolved in IL2CPP metadata. Use a try-catch around type checks done at runtime on unknown objects.

`GetName()` reads `m_Name` directly from native memory with a managed-proxy fallback. Returns `null` (not an exception) on failure.

### `GameObj.FromPointer` and `GameObj.Null`

```csharp
var obj  = GameObj.FromPointer(somePtr); // named factory — prefer over direct constructor
var empty = GameObj.Null;                // default/sentinel, Pointer == IntPtr.Zero
```

---

## `GameObj<T>` — The Typed Handle

**File:** `GameObjTyped.cs`

`GameObj<T>` is a typed wrapper over `GameObj` that binds a specific IL2CppInterop proxy type `T`. It adds field resolution via expression trees and a managed proxy accessor. Use it when you know the concrete type of an object.

```csharp
GameObj<TacticalActor> typedActor = GameObj<TacticalActor>.Wrap(actorPtr);
TacticalActor proxy = typedActor.AsManaged();
```

### Wrapping

```csharp
// Throws GameObjException if raw pointer is null
GameObj<TacticalActor> typed = GameObj<TacticalActor>.Wrap(actorPtr);

// Non-throwing; returns false if pointer is null
if (GameObj<TacticalActor>.TryWrap(raw, out var typed)) { ... }
```

> **Note:** `Wrap` does not yet validate that the pointer actually points to a `T` (type validation via `il2cpp_class_is_assignable_from` is planned). It is a trust-the-caller assertion. If you pass the wrong pointer type, you will get garbage reads, not a clean exception.

### Field Resolution

Rather than hardcoding memory offsets, use expression-based field resolution. Offsets are resolved once against the live IL2CPP metadata and cached:

```csharp
// Resolved at startup, cached for the session
static readonly FieldHandle<TacticalActor, int> _hActionPoints =
    GameObj<TacticalActor>.ResolveField(x => x.m_ActionPoints);

static readonly StringFieldHandle<TacticalActor> _hActorName =
    GameObj<TacticalActor>.ResolveStringField(x => x.m_DisplayName);

static readonly ObjFieldHandle<TacticalActor, TacticalUnit> _hUnit =
    GameObj<TacticalActor>.ResolveObjField(x => x.m_Unit);
```

Selectors must be direct member accesses (`x => x.Field`). Chains, method calls, and computed expressions throw at resolution time, which is the intended behavior — you want the failure early.

### `FieldAt` — The Escape Hatch

```csharp
static readonly FieldHandle<TacticalActor, int> _hLegacy =
    GameObj<TacticalActor>.FieldAt<int>(0x78, "m_SomeField");
```

`FieldAt` bypasses expression-based resolution for porting existing code with known offsets. It skips startup validation, so a silently wrong offset will produce bad reads with no diagnostic. Replace every `FieldAt` call with `ResolveField` before considering a port complete. The `name` parameter is mandatory in practice — always pass the real field name so broken offsets after a game update produce actionable log output.

---

## `FieldHandle`, `ObjFieldHandle`, `StringFieldHandle` — Typed Field Accessors

**File:** `FieldHandle.cs`

Field handles are resolved once and reused. They provide typed `Read`, `TryRead`, and `Write` operations that validate liveness before touching memory.

### `FieldHandle<T, TVal>` — Unmanaged value fields

```csharp
int ap = _hActionPoints.Read(typedActor);          // throws if object is not Alive
if (_hActionPoints.TryRead(typedActor, out int ap)) // safe non-throwing path
{ ... }
_hActionPoints.Write(typedActor, ap - 1);           // throws if not Alive or offset zero
```

Use `TryRead` when the object's liveness is uncertain. Use `Read` when you've already confirmed liveness and want a clean value or a clear exception — not a silent zero.

### `ObjFieldHandle<T, TObj>` — Reference-type (nested object) fields

```csharp
GameObj<TacticalUnit> unit = _hUnit.Read(typedActor);
// throws if not Alive, offset zero, or field pointer is null

if (_hUnit.TryRead(typedActor, out var unit)) { ... }
// use TryRead when the field may legitimately be unset (optional relationships)
```

The `Read` overload throws on a null field pointer. If a nested object is optional (may not always be set), always use `TryRead`.

### `StringFieldHandle<T>` — IL2CPP string fields

Same interface as `ObjFieldHandle`, but converts the native string pointer to a managed `string` via `IL2CPP.Il2CppStringToManaged`. Write is not supported — IL2CPP string interning makes in-place string writes unsafe.

---

## `GameQuery` — Scene Object Discovery

**File:** `GameQuery.cs`

`GameQuery` wraps `Resources.FindObjectsOfTypeAll` for safe, typed discovery of live game objects.

```csharp
// Find all actors in the scene
TacticalActor[] actors = GameQuery.FindAll<TacticalActor>();

// Find a specific object by Unity name
TacticalActor player = GameQuery.FindByName<TacticalActor>("PlayerLeader");

// Cached variant — result is reused until scene load
TacticalActor[] cached = GameQuery.FindAllCached<TacticalActor>();
```

### The Cache

`FindAllCached<T>` stores results per-type in a static slot that is cleared automatically on scene load (via `ModpackLoaderMod.OnSceneWasLoaded`). The cache is appropriate for objects that persist through a scene but should never be held across scene transitions. Use `FindAll<T>` when you need a fresh query.

`FindAll<T>` returns `Array.Empty<T>()` on any failure — it never throws.

---

## `GameState` — Scene Awareness and Deferred Execution

**File:** `GameState.cs`

`GameState` provides scene lifecycle events, scene identity helpers, and frame-deferred callbacks. It is the correct place to gate any logic that requires a specific scene to be active.

### Scene Identity

```csharp
GameState.IsTactical    // true while in the combat scene
GameState.IsStrategy    // true in campaign scenes (not tactical, not menu/loading)
GameState.IsScene("Tactical")   // case-insensitive scene name match
GameState.CurrentScene          // raw scene name string
```

### Lifecycle Events

```csharp
GameState.SceneLoaded += sceneName => { /* fires on every scene load */ };
GameState.TacticalReady += () => { /* fires ~30 frames after Tactical loads */ };
```

`TacticalReady` exists because `TacticalManager` is not fully initialized the moment the scene loads.

### Deferred Execution

```csharp
// Run callback after N frames
GameState.RunDelayed(5, () => { /* safe to call game objects now */ });

// Run callback when a condition becomes true, polling each frame
// Gives up after maxAttempts (default 30) frames
GameState.RunWhen(
    () => SomeManager.IsReady,
    () => { /* now safe to proceed */ },
    maxAttempts: 60
);
```

`RunWhen` is useful when you need to wait for a singleton or manager to be available without writing a polling loop yourself. If the condition never becomes true within `maxAttempts`, the callback is silently dropped — set a generous limit and log inside your callback if it's important.

---

## `GameType` — IL2CPP Type System Access

**File:** `GameType.cs`

`GameType` wraps an IL2CPP class pointer and provides type-level operations: name resolution, managed proxy type lookup, and identity comparison. You rarely construct these directly — they come back from `GameObj.GetGameType()` or `GameType.Of<T>()`.

```csharp
GameType actorType = GameType.Of<TacticalActor>();
GameType runtimeType = someObj.GetGameType();

bool same = actorType.ClassPointer == runtimeType.ClassPointer;
```

`GameType` results are cached by pointer to avoid repeated FFI calls. `IsValid` is false when the underlying class pointer is zero — always check before doing type system work on an unknown object.

---

## `GameMethod` — Calling IL2CPP Methods via Reflection

**File:** `GameMethod.cs`

`GameMethod` provides expression-tree-based method invocation. Method resolution happens at compile time; failures surface as compiler errors rather than runtime exceptions.

```csharp
// Returns boxed object, null on failure
object result = GameMethod.Call<TacticalActor>(instance, x => x.SomeMethod());

// Typed convenience wrappers
int value = GameMethod.CallInt<TacticalActor>(instance, x => x.GetActionPoints());
bool ok    = GameMethod.CallBool<TacticalActor>(instance, x => x.IsAlive());

// Static factory/singleton calls
object singleton = GameMethod.CallStatic<TacticalManager>(x => x.GetInstance());
```

All overloads return a safe default on failure (`null`, `0`, `false`) and log internally via `ModError`. They do not throw.

---

## `Templates` — DataTemplate Lifecycle

**File:** `Templates.cs`

`Templates` provides guaranteed-loaded access to `DataTemplate` assets. The game lazily loads templates, so querying a type before it has been materialised returns nothing. `Templates` handles this transparently.

```csharp
// By ID — logs a warning if not found, returns null
SkillTemplate skill = Templates.FindByID<SkillTemplate>("medic_stabilize");

// Non-throwing path — use when absence is a valid runtime condition
if (Templates.TryGet<SkillTemplate>("medic_stabilize", out var skill)) { ... }

// All templates of a type
IReadOnlyCollection<SkillTemplate> all = Templates.FindAll<SkillTemplate>();
```

> Clone support (creating new templates at runtime) is not yet part of this API. The full clone pipeline — `m_TemplateMaps` registration, `m_TemplateArrays` extension, ancestor mirroring — is under development and will be promoted here once stable. Do not attempt to register cloned templates manually against the internals `Templates` relies on.

---

## Common Patterns

### Safe event handler skeleton

```csharp
TacticalEventHooks.OnActorKilled += (actorPtr, killerPtr, killerFaction) =>
{
    var actor = GameObj.FromPointer(actorPtr);
    var killer = GameObj.FromPointer(killerPtr);

    // Step 1: null check
    if (actor.IsNull || killer.IsNull) return;

    // Step 2: liveness check
    if (actor.CheckAlive() != AliveStatus.Alive) return;
    if (killer.CheckAlive() != AliveStatus.Alive) return;

    // Step 3: now safe to read
    SdkLogger.Msg($"{actor.GetName()} killed by {killer.GetName()}");
};
```

### Resolving field handles at startup

Resolve handles once, during initialization, not inside hot event handlers.

```csharp
static FieldHandle<TacticalActor, int> _hAP;

public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
{
    _hAP = GameObj<TacticalActor>.ResolveField(x => x.m_ActionPoints);
}
```

---

## Quick Reference: Which Type to Use

| You want to… | Use |
|---|---|
| Hold a reference to any game object | `GameObj` |
| Read/write fields on a known type | `GameObj<T>` + `FieldHandle` |
| Check if an object is alive | `GameObj.CheckAlive()` |
| Find all objects of a type in scene | `GameQuery.FindAll<T>()` |
| Check what scene you're in | `GameState.IsTactical` / `IsScene()` |
| Wait for the tactical scene to be ready | `GameState.TacticalReady` |
| Defer work by N frames | `GameState.RunDelayed()` |
| Wait for a condition to be true | `GameState.RunWhen()` |
| Call a method on a game object | `GameMethod.Call<T>()` |
| Load a DataTemplate by ID | `Templates.FindByID<T>()` |
| Inspect an object's IL2CPP type | `GameObj.GetGameType()` / `GameType.Of<T>()` |
