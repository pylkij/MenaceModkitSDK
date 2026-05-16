# GameObj Beginner's Guide

This guide explains how to read and write data on live game objects in Menace mods. It assumes you're new to both C# and modding — no prior experience required.

---

## What Problem Does This Solve?

Menace runs on a technology called IL2CPP, which compiles the game's C# code into native machine code for performance. The downside: you can't just access game objects like normal C# objects at runtime. Their data lives at specific memory locations, and you need to know *where* to find it.

The `GameObj` system gives you a safe way to reach into a live game object and read or change its fields — things like a unit's health, movement speed, or current tile — without crashing the game if something goes wrong.

---

## Core Concepts

Before looking at code, it helps to understand three things:

**Pointers** are memory addresses — a number that says "the data you want is at this location." A `GameObj` wraps one of these addresses so you don't have to work with raw numbers directly.

**Offsets** are how you find a specific field inside an object. If a unit object starts at address 1000 and its health is stored 72 bytes in, the offset for health is 72. The SDK resolves these offsets for you automatically.

**Handles** are pre-resolved field accessors. You set them up once when your mod loads, then use them repeatedly to read and write a specific field. Think of a handle like a bookmark — resolve it once, use it anywhere.

---

## The Two Types You'll Use

### GameObj — the safe, untyped handle

`GameObj` wraps any game object pointer. It's safe to use even if something goes wrong: reads return a zero value or null rather than crashing, and errors are logged automatically.

```csharp
// You usually get a GameObj from a hook or from reading a field —
// but you can also wrap a raw pointer yourself:
GameObj obj = GameObj.FromPointer(somePointer);
```

This is the type you'll see arriving as parameters in hook callbacks. It's also what `ReadObj` returns when you follow a reference field to another object.

### GameObj\<T\> — the typed wrapper

`GameObj<T>` is a typed version of `GameObj`. The `T` is the specific game class you're working with — like `TacticalUnit` or `Inventory`. The typed version is what you use to set up field handles and get access to the managed object.

```csharp
// Promote an untyped GameObj to a typed one
if (GameObj<TacticalUnit>.TryWrap(raw, out var unit))
{
    // 'unit' is now a GameObj<TacticalUnit>
}
```

If the pointer is null, `TryWrap` returns `false` safely. Use this pattern whenever you're not certain the object is valid.

---

## Checking If an Object Is Still Alive

Game objects can be destroyed at any time. Before doing anything important with an object you received from outside your own code, check whether it's still alive:

```csharp
if (obj.CheckAlive() != AliveStatus.Alive)
    return; // object is gone — stop here
```

`CheckAlive` reads a special pointer inside the object that Unity uses to track whether the native object still exists. It returns one of three values:

- `Alive` — the object exists, proceed
- `Dead` — the object has been destroyed, stop
- `Unknown` — the SDK couldn't check (treat this cautiously)

---

## Setting Up Field Handles

Field handles let you read and write specific fields on a game object. You set them up once when your mod loads — not every time you need to read a value.

The expression `x => x.health` looks like a function call, but it isn't. It's a way of pointing at the field by name so the SDK can find its memory offset automatically. The compiler checks that the field exists, so you get an error at compile time rather than a silent miss at runtime.

```csharp
// Set these up once, in a static class or your mod's Init method
private static FieldHandle<TacticalUnit, int>     _health;
private static FieldHandle<TacticalUnit, float>   _speed;
private static StringFieldHandle<TacticalUnit>    _displayName;
private static ObjFieldHandle<TacticalUnit, Tile> _currentTile;

public static void Init()
{
    _health      = GameObj<TacticalUnit>.ResolveField(x => x.health);
    _speed       = GameObj<TacticalUnit>.ResolveField(x => x.movementSpeed);
    _displayName = GameObj<TacticalUnit>.ResolveStringField(x => x.displayName);
    _currentTile = GameObj<TacticalUnit>.ResolveObjField(x => x.currentTile);
}
```

Call `Init()` from your mod's scene-load or startup hook — not from a static constructor. The game's type system needs to be ready before offsets can be resolved.

There are three resolve methods depending on what kind of field you're working with:

| Field type | Method to use |
|---|---|
| Numbers, booleans, enums, structs | `ResolveField` |
| References to other game objects | `ResolveObjField` |
| Text strings | `ResolveStringField` |

---

## Reading Fields

Once you have a handle and a typed object, reading a field is one line:

```csharp
int hp = _health.Read(unit);
float speed = _speed.Read(unit);
string name = _displayName.Read(unit);
```

`Read` will throw an error if the object isn't alive or the handle wasn't set up correctly. If you're not sure whether the object is valid, use `TryRead` instead:

```csharp
if (_health.TryRead(unit, out int hp))
{
    // hp is valid, use it here
}
```

`TryRead` never throws — it just returns `false` if something is wrong.

For object reference fields, the same pattern applies:

```csharp
// Read another object that this unit holds a reference to
if (_currentTile.TryRead(unit, out GameObj<Tile> tile))
{
    // work with 'tile' here
}
```

---

## Writing Fields

Writing works the same way as reading:

```csharp
_health.Write(unit, 100);       // set health to 100
_speed.Write(unit, 5.5f);       // set speed to 5.5
```

Write will throw if the object isn't alive. Always check liveness before writing if there's any chance the object could have been destroyed.

String fields are read-only — there is no `StringFieldHandle.Write`.

---

## A Complete Example

Here's a full mod patch that ties everything together:

```csharp
// 1. Declare handles at the top of your class
private static FieldHandle<TacticalUnit, int>   _health;
private static FieldHandle<TacticalUnit, float> _speed;
private static StringFieldHandle<TacticalUnit>  _name;

// 2. Resolve them when your mod loads
public static void Init()
{
    _health = GameObj<TacticalUnit>.ResolveField(x => x.health);
    _speed  = GameObj<TacticalUnit>.ResolveField(x => x.movementSpeed);
    _name   = GameObj<TacticalUnit>.ResolveStringField(x => x.displayName);
}

// 3. Use them inside a hook
[HarmonyPatch(typeof(TacticalUnit), nameof(TacticalUnit.OnTurnStart))]
static void Postfix(TacticalUnit __instance)
{
    // Wrap the hook parameter into a GameObj
    var raw = GameObj.FromPointer(__instance.Pointer);

    // Check liveness
    if (raw.CheckAlive() != AliveStatus.Alive)
        return;

    // Promote to typed
    if (!GameObj<TacticalUnit>.TryWrap(raw, out var unit))
        return;

    // Read fields
    if (!_name.TryRead(unit, out string name)) return;
    if (!_health.TryRead(unit, out int hp)) return;

    // Log and modify
    Log.Info($"{name} started their turn with {hp} HP");
    _speed.Write(unit, 6.0f); // give everyone a little extra speed
}
```

---

## When Things Go Wrong

The SDK is designed so that mistakes produce clear errors rather than silent failures or crashes.

If a field handle can't find the field you named, it throws an error when `Init()` runs — before your mod does anything to the game. This means bad field names surface immediately rather than during play.

If you try to read from a destroyed object, `Read` throws a `GameObjException` with a message naming the field. Check `CheckAlive()` or use `TryRead` to avoid this.

If you use `GameObj` untyped reads (like `ReadInt`, `ReadFloat`) without a handle, the worst that happens is you get a zero value back and an entry in the error log. These are provided as a fallback and are safe, but field handles are always preferred for real feature code.

---

## Quick Reference

**Setting up:**
```csharp
// Value field (int, float, bool, enum...)
FieldHandle<TUnit, int> h = GameObj<TUnit>.ResolveField(x => x.fieldName);

// Object reference field
ObjFieldHandle<TUnit, TOther> h = GameObj<TUnit>.ResolveObjField(x => x.fieldName);

// String field
StringFieldHandle<TUnit> h = GameObj<TUnit>.ResolveStringField(x => x.fieldName);
```

**Using:**
```csharp
int val = handle.Read(unit);                    // throws on failure
handle.TryRead(unit, out int val)               // safe, returns bool
handle.Write(unit, newValue);                   // throws on failure
```

**Wrapping:**
```csharp
GameObj raw = GameObj.FromPointer(ptr);
GameObj<T>.TryWrap(raw, out var typed)          // safe, returns bool
GameObj<T>.Wrap(raw)                            // throws if null
```

**Liveness:**
```csharp
obj.CheckAlive() == AliveStatus.Alive           // safe to proceed
obj.CheckAlive() == AliveStatus.Dead            // object is gone
obj.CheckAlive() == AliveStatus.Unknown         // treat as dead
```
