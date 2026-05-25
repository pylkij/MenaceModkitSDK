# GameObj & FieldHandle API Reference

`GameObj`, `GameObj<T>`, and the `FieldHandle` family are the core primitives in the `Menace.SDK` namespace for reading and writing fields on live IL2CPP Unity objects. Together they provide a two-layer system: `GameObj` is an untyped safe handle that never throws and returns defaults on failure; `GameObj<T>` is a typed wrapper that adds compile-time field resolution and managed proxy access via expression trees; the `FieldHandle` structs are pre-resolved field accessors that perform fast, offset-direct memory reads and writes with explicit liveness checking.

---

## The Two Layers

| Layer | Type | Error policy | Primary use |
|---|---|---|---|
| Untyped | `GameObj` | Returns defaults, never throws | Event hooks, exploratory reads, interop |
| Typed | `GameObj<T>` | Throws `GameObjException` on misuse | Mod feature code, field handles, managed proxies |

Use `GameObj` at ingress points (hook parameters, pointer sources) where the type is unknown or failure is expected. Promote to `GameObj<T>` once the type is confirmed and build feature logic on top of it. Never reach through a `GameObj<T>` to call untyped reads when a field handle is available.

---

## GameObj — Untyped Safe Handle

`GameObj` wraps a raw IL2CPP `IntPtr`. All reads return defaults on failure; all writes throw on a null or zero-offset pointer. The type never throws from reads.

### Construction

`GameObj` has no public constructor. Use the factory methods:

```csharp
GameObj obj   = GameObj.FromPointer(ptr);  // wrap a known IL2CPP pointer
GameObj null_ = GameObj.Null;              // explicit null sentinel
```

`FromPointer` is the public entry point for wrapping a raw IL2CPP pointer obtained from the game — for example, a pointer surfaced by a native callback, a scan result, or an interop boundary. In practice, `GameObj` instances also arrive as parameters from hook callbacks or are produced by field reads (`ReadObj`) and field handles.

### Liveness Check

```csharp
AliveStatus CheckAlive()
```

Reads `m_CachedPtr` from unmanaged memory to determine whether the underlying Unity native object is still alive. Call this before any meaningful operation on an object obtained from an external source.

```csharp
public enum AliveStatus { Alive, Dead, Unknown }
```

`Unknown` is returned when the cached-pointer offset is unavailable (i.e., `OffsetCache.ObjectCachedPtrOffset` is zero) or the memory read faults. Treat `Unknown` conservatively — do not proceed with reads if the object's origin is untrusted.

```csharp
if (obj.CheckAlive() != AliveStatus.Alive)
    return;
```

### Untyped Field Reads

All read methods are safe: they return the zero-value of their type if the pointer is null, the offset is zero, or a memory exception occurs. Errors are routed to `ModError.ReportInternal`.

```csharp
int   ReadInt   (uint offset)
float ReadFloat (uint offset)
bool  ReadBool  (uint offset)
string ReadString(uint offset)   // returns null on failure
GameObj ReadObj  (uint offset)   // returns GameObj.Null on failure
IntPtr ReadPtr  (uint offset)   // returns IntPtr.Zero on failure
```

```csharp
int health = obj.ReadInt(0x48);
float speed = obj.ReadFloat(0x4C);
bool isActive = obj.ReadBool(0x50);
string label = obj.ReadString(0x58);   // IL2CPP string field
GameObj child = obj.ReadObj(0x60);     // nested object reference
```

Offsets must be known in advance — typically resolved at startup via `OffsetCache` or migrated from an existing `GameObj<T>.FieldAt` call. For new code, resolve offsets through `GameObj<T>` field handles instead.

### Untyped Field Writes

Writes throw `GameObjException` rather than silently doing nothing, because a silent failed write is harder to diagnose than an exception.

```csharp
void WriteInt  (uint offset, int value)
void WriteFloat(uint offset, float value)
void WritePtr  (uint offset, IntPtr value)
```

```csharp
obj.WriteInt(0x48, 100);
obj.WriteFloat(0x4C, 5.5f);
```

### Type and Name Introspection

```csharp
GameType GetGameType()    // IL2CPP class metadata; null on failure
string   GetTypeName()    // fully-qualified name; "<unknown>" on failure
string   GetName()        // UnityEngine.Object.name; null if unavailable
```

```csharp
T As<T>() where T : class
```

Constructs a managed IL2CPP proxy of type `T` over this pointer. Returns `null` if `T` has no `IntPtr` constructor or conversion throws. Prefer `GameObj<T>.AsManaged()` when the type is already established.

```csharp
var unit = obj.As<TacticalUnit>();
```

### Equality and Null Checks

```csharp
bool IsNull                        // true when Pointer == IntPtr.Zero
bool Equals(GameObj other)
static bool operator ==(GameObj, GameObj)
static bool operator !=(GameObj, GameObj)
```

Two `GameObj` instances are equal when their raw pointers are identical.

---

## GameObj\<T\> — Typed Wrapper

`GameObj<T>` constrains `T : Il2CppObjectBase` and adds compile-time field resolution, managed proxy construction, and typed field handle factories. All field resolution methods require IL2CPP class registration to be complete; call them from a mod's `OnSceneLoad` or equivalent initialisation point, not from a static constructor.

### Wrapping

```csharp
static GameObj<T> Wrap(GameObj raw)
static GameObj<T> Wrap(IntPtr ptr)
static bool TryWrap(GameObj raw, out GameObj<T> result)
```

`Wrap` throws `GameObjException` if the pointer is null. `TryWrap` returns `false` cleanly. No type validation against the IL2CPP class hierarchy is performed at this stage — wrapping is a trust-the-caller assertion. Type-safe casting via `il2cpp_class_is_assignable_from` is planned.

```csharp
// From a hook parameter
var typed = GameObj<TacticalUnit>.Wrap(GameObj.FromPointer(__instance.Pointer));

// From an untyped field read
if (GameObj<TacticalUnit>.TryWrap(obj.ReadObj(0x60), out var unit))
    ProcessUnit(unit);
```

### Managed Proxy

```csharp
T AsManaged()
```

Constructs the IL2CPP managed proxy object using a compiled expression-tree constructor (`Func<IntPtr, T>`). The constructor is built lazily on first access and cached. Use this when you need to call managed methods on `T` or pass the object to APIs that expect the managed type.

```csharp
var proxy = typedObj.AsManaged();
proxy.SomeIl2CppMethod();
```

If `T` has no `IntPtr` constructor, `AsManaged` throws `GameObjException`. This is a programming error, not a runtime condition.

### Field Handle Resolution

Field handles are resolved once at startup and reused for every subsequent read/write. Resolution uses expression trees to capture the field name at compile time, then looks up the IL2CPP offset from the native class pointer via `OffsetCache`.

```csharp
static FieldHandle<T, TVal>     ResolveField    <TVal>(Expression<Func<T, TVal>> selector)  where TVal : unmanaged
static ObjFieldHandle<T, TObj>  ResolveObjField <TObj>(Expression<Func<T, TObj>> selector)  where TObj : Il2CppObjectBase
static StringFieldHandle<T>     ResolveStringField    (Expression<Func<T, string>> selector)
```

```csharp
// Resolved once, stored as a static field
private static readonly FieldHandle<TacticalUnit, int> _healthHandle =
    GameObj<TacticalUnit>.ResolveField(x => x.health);

private static readonly ObjFieldHandle<TacticalUnit, Inventory> _inventoryHandle =
    GameObj<TacticalUnit>.ResolveObjField(x => x.inventory);

private static readonly StringFieldHandle<TacticalUnit> _nameHandle =
    GameObj<TacticalUnit>.ResolveStringField(x => x.displayName);
```

Selectors must be direct member accesses (`x => x.FieldName`). Chains, method calls, and computed expressions throw `GameObjException` at resolution time.

### Escape Hatch — FieldAt

```csharp
static FieldHandle<T, TVal> FieldAt<TVal>(uint offset, string name = "unknown") where TVal : unmanaged
```

Creates a field handle from a known offset without verifying it against the IL2CPP class. Use only when porting existing code that already has verified offsets. Replace with `ResolveField` before considering a port complete. Always pass the actual field name so broken offsets after a game update produce actionable log output.

```csharp
// Porting: known offset from prior reverse-engineering
private static readonly FieldHandle<Actor, float> _speedHandle =
    GameObj<Actor>.FieldAt<float>(0x54, "movementSpeed");
// TODO: replace with ResolveField(x => x.movementSpeed)
```

### Equality

`GameObj<T>` equality delegates to the underlying `GameObj` pointer comparison.

```csharp
bool Equals(GameObj<T> other)
static bool operator ==(GameObj<T>, GameObj<T>)
static bool operator !=(GameObj<T>, GameObj<T>)
```

---

## Field Handles

Field handles are pre-resolved, reusable accessors for a specific field at a specific offset on objects of type `T`. Obtain them from `GameObj<T>` factory methods — do not construct directly.

### FieldHandle\<T, TVal\> — Unmanaged Values

For primitive and value-type fields (`int`, `float`, `bool`, `Vector3`, enums, etc.).

```csharp
TVal Read   (GameObj<T> obj)
bool TryRead(GameObj<T> obj, out TVal result)
void Write  (GameObj<T> obj, TVal value)
```

`Read` and `Write` throw `GameObjException` if the object is not alive or the offset is zero. Use `TryRead` when the object's liveness is uncertain and you want a non-throwing path.

```csharp
int hp = _healthHandle.Read(unit);

if (_healthHandle.TryRead(unit, out int hp))
    ShowHealthBar(hp);

_healthHandle.Write(unit, 100);
```

### ObjFieldHandle\<T, TObj\> — Object References

For fields that hold a reference to another IL2CPP object.

```csharp
GameObj<TObj> Read   (GameObj<T> obj)
bool          TryRead(GameObj<T> obj, out GameObj<TObj> result)
void          Write  (GameObj<T> obj, GameObj<TObj> value)
```

`Read` throws if the object is not alive, the offset is zero, or the field pointer is null. Use `TryRead` if the field may legitimately be unset at runtime.

```csharp
var inv = _inventoryHandle.Read(unit);   // throws if inventory is null

if (_inventoryHandle.TryRead(unit, out var inv))
    ProcessInventory(inv);

_inventoryHandle.Write(unit, newInventory);
```

### StringFieldHandle\<T\> — IL2CPP Strings

For fields that hold a pointer to an IL2CPP managed string. Converts the native pointer to a managed `string` via `IL2CPP.Il2CppStringToManaged`. Write is not supported.

```csharp
string Read   (GameObj<T> obj)
bool   TryRead(GameObj<T> obj, out string result)
```

`Read` throws if the object is not alive, the offset is zero, or the string pointer is null.

```csharp
string name = _nameHandle.Read(unit);

if (_nameHandle.TryRead(unit, out string name))
    ShowUnitName(name);
```

---

## Error Handling

`GameObj` untyped reads never throw — they return defaults and log via `ModError.ReportInternal`. `GameObj<T>` factory methods and field handles throw `GameObjException` on misuse. The distinction is intentional: the untyped layer is a safe ingress surface; the typed layer fails loudly so programming errors surface during development.

| Operation | On failure |
|---|---|
| `GameObj` reads | Returns `0` / `false` / `null` / `GameObj.Null` |
| `GameObj` writes | Throws `GameObjException` |
| `GameObj<T>.Wrap` | Throws `GameObjException` if null |
| `GameObj<T>.TryWrap` | Returns `false` |
| `FieldHandle.Read` / `Write` | Throws `GameObjException` |
| `FieldHandle.TryRead` | Returns `false` |
| `ResolveField` (bad selector) | Throws `GameObjException` at startup |
| `ResolveField` (field not found) | Throws `GameObjException` at startup |

---

## Initialisation Pattern

Field handles must be resolved after IL2CPP class registration is complete. The recommended pattern is a static initialiser called from a known-safe point in the mod lifecycle:

```csharp
internal static class UnitHandles
{
    public static FieldHandle<TacticalUnit, int>      Health;
    public static FieldHandle<TacticalUnit, float>    Speed;
    public static ObjFieldHandle<TacticalUnit, Tile>  CurrentTile;
    public static StringFieldHandle<TacticalUnit>     DisplayName;

    public static void Init()
    {
        Health      = GameObj<TacticalUnit>.ResolveField(x => x.health);
        Speed       = GameObj<TacticalUnit>.ResolveField(x => x.movementSpeed);
        CurrentTile = GameObj<TacticalUnit>.ResolveObjField(x => x.currentTile);
        DisplayName = GameObj<TacticalUnit>.ResolveStringField(x => x.displayName);
    }
}

// In mod entry point, after scene load:
UnitHandles.Init();
```

Resolving inside a `static readonly` field initialiser risks `TypeInitializationException` if `T`'s IL2CPP registration has not completed. Use an explicit `Init()` method instead.

---

## Combined Usage Example

```csharp
// --- Hook entry point (untyped ingress) ---
[HarmonyPatch(typeof(TacticalManager), nameof(TacticalManager.EndTurn))]
static void Postfix(TacticalManager __instance)
{
    var raw = GameObj.FromPointer(__instance.Pointer);

    if (raw.CheckAlive() != AliveStatus.Alive)
        return;

    // Promote to typed wrapper
    if (!GameObj<TacticalManager>.TryWrap(raw, out var manager))
        return;

    // Read an object field to get the active unit
    if (!Handles.ActiveUnit.TryRead(manager, out var unitObj))
        return;

    // Read value fields via pre-resolved handles
    int hp = Handles.Health.Read(unitObj);
    float speed = Handles.Speed.Read(unitObj);

    // Read a string field
    if (Handles.DisplayName.TryRead(unitObj, out string name))
        Log.Info($"{name}: HP={hp}, Speed={speed}");

    // Write a value field
    Handles.Speed.Write(unitObj, speed * 1.1f);

    // Obtain a managed proxy for IL2CPP method calls
    var proxy = unitObj.AsManaged();
    proxy.RefreshActionPoints();
}
```

---

## Combining with GameMethod

`GameObj<T>.AsManaged()` and `GameMethod` are complementary. Use field handles for direct memory reads and writes; use `GameMethod` when the operation is best expressed as an IL2CPP method call.

```csharp
// Field handle: direct memory read — prefer for hot paths
int ap = Handles.ActionPoints.Read(unit);

// GameMethod: invoke a managed method — prefer when a method encapsulates logic
bool canAct = GameMethod.CallBool<TacticalUnit>(unit.Untyped.As<TacticalUnit>(),
    x => x.CanTakeAction());
```

See the `GameMethod` API Reference for the full method surface.

---

## Method Reference

### GameObj

| Method | Returns | Notes |
|---|---|---|
| `CheckAlive()` | `AliveStatus` | Reads `m_CachedPtr`; `Unknown` if offset unavailable |
| `ReadInt(offset)` | `int` | Returns `0` on failure |
| `ReadFloat(offset)` | `float` | Returns `0f` on failure |
| `ReadBool(offset)` | `bool` | Returns `false` on failure |
| `ReadString(offset)` | `string` | Returns `null` on failure |
| `ReadObj(offset)` | `GameObj` | Returns `GameObj.Null` on failure |
| `ReadPtr(offset)` | `IntPtr` | Returns `IntPtr.Zero` on failure |
| `WriteInt(offset, value)` | `void` | Throws on null pointer or zero offset |
| `WriteFloat(offset, value)` | `void` | Throws on null pointer or zero offset |
| `WritePtr(offset, value)` | `void` | Throws on null pointer or zero offset |
| `GetGameType()` | `GameType` | `null` on failure |
| `GetTypeName()` | `string` | `"<unknown>"` on failure |
| `GetName()` | `string` | `null` on failure |
| `As<T>()` | `T` | `null` on failure or missing constructor |

### GameObj\<T\>

| Method | Returns | Notes |
|---|---|---|
| `Wrap(GameObj)` | `GameObj<T>` | Throws if pointer is null |
| `Wrap(IntPtr)` | `GameObj<T>` | Throws if pointer is null |
| `TryWrap(GameObj, out result)` | `bool` | Safe; returns `false` if null |
| `AsManaged()` | `T` | Throws if `T` has no `IntPtr` constructor |
| `ResolveField<TVal>(selector)` | `FieldHandle<T, TVal>` | Throws at startup on bad selector or missing field |
| `ResolveObjField<TObj>(selector)` | `ObjFieldHandle<T, TObj>` | Throws at startup on bad selector or missing field |
| `ResolveStringField(selector)` | `StringFieldHandle<T>` | Throws at startup on bad selector or missing field |
| `FieldAt<TVal>(offset, name)` | `FieldHandle<T, TVal>` | Escape hatch; no IL2CPP validation |

### FieldHandle\<T, TVal\>

| Method | Returns | Notes |
|---|---|---|
| `Read(obj)` | `TVal` | Throws if not alive or offset zero |
| `TryRead(obj, out result)` | `bool` | Safe; returns `false` on failure |
| `Write(obj, value)` | `void` | Throws if not alive or offset zero |

### ObjFieldHandle\<T, TObj\>

| Method | Returns | Notes |
|---|---|---|
| `Read(obj)` | `GameObj<TObj>` | Throws if not alive, offset zero, or field null |
| `TryRead(obj, out result)` | `bool` | Safe; returns `false` on failure |
| `Write(obj, value)` | `void` | Throws if not alive or offset zero |

### StringFieldHandle\<T\>

| Method | Returns | Notes |
|---|---|---|
| `Read(obj)` | `string` | Throws if not alive, offset zero, or string null |
| `TryRead(obj, out result)` | `bool` | Safe; returns `false` on failure |
