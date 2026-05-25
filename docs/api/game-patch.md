# GamePatch API Reference

`GamePatch` is a static class in the `Menace.SDK` namespace. It applies Harmony prefix and postfix patches to IL2CPP game methods with hierarchy-aware method discovery. Failed patches log to `ModError` and return `false` rather than throwing.

---

## How It Works

`GamePatch` wraps Harmony's patching API with two additions: a hierarchy walk that finds methods declared on base types when a direct lookup fails, and optional parameter type resolution for methods with overloads. You pass the target type via `typeof()` for compile-time verification of the type itself — the method name remains a string, which is an unavoidable constraint of IL2CPP interop.

```csharp
// Type is verified at compile time; method name is resolved at runtime
GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDeath", hooks.GetMethod(nameof(OnActorKilled_Postfix), flags));
```

On failure, all methods return `false` and report to `ModError` with context about the type and method involved.

---

## Patching

### `Postfix`

```csharp
bool Postfix(
    Harmony harmony,
    Type targetType,
    string methodName,
    MethodInfo patchMethod,
    Type[] parameterTypes = null)
```

Applies a Harmony postfix patch to the named method. Returns `true` if the patch was applied successfully.

```csharp
// When patching multiple methods on the same type, cache typeof() upfront
var tacticalManager = typeof(Il2CppMenace.Tactical.TacticalManager);
var hooks = typeof(TacticalEventHooks); // Your patch class
var flags = BindingFlags.Static | BindingFlags.NonPublic;

GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDeath", hooks.GetMethod(nameof(OnActorKilled_Postfix), flags));
```

---

### `Prefix`

```csharp
bool Prefix(
    Harmony harmony,
    Type targetType,
    string methodName,
    MethodInfo patchMethod,
    Type[] parameterTypes = null)
```

Applies a Harmony prefix patch to the named method. Returns `true` if the patch was applied successfully.

```csharp
// EXAMPLE: replace with a real prefix patch call
GamePatch.Prefix(harmony, someType, "MethodName", hooks.GetMethod(nameof(MyMethod_Prefix), flags));
```

---

## Overload Resolution

When the target method has multiple overloads, `GetMethod` without parameter types throws `AmbiguousMatchException`. Pass `parameterTypes` to resolve the correct overload explicitly.

```csharp
// Method has overloads:
//   void ApplyEffect(EffectType type)
//   void ApplyEffect(EffectType type, int duration)

GamePatch.Postfix(
    harmony,
    typeof(SomeActor),             // EXAMPLE: replace with real type
    "ApplyEffect",                 // EXAMPLE: replace with real method name
    AccessTools.Method(typeof(MyPatches), nameof(MyPatches.ApplyEffect_Postfix)),
    parameterTypes: new[] { typeof(EffectType), typeof(int) });  // EXAMPLE: replace with real parameter types
```

When `parameterTypes` is null (the default), the lookup uses name-only resolution and falls back to a hierarchy walk if the method is declared on a base type.

---

## Hierarchy Walk

If a name-only lookup fails on the declared type, `GamePatch` walks the type hierarchy using `DeclaredOnly` reflection until the method is found or the hierarchy is exhausted. This handles cases where the method you want to patch is defined on a base class rather than the concrete type you hold.

This walk only runs when `parameterTypes` is not supplied. With explicit parameter types the initial lookup is already unambiguous.

---

## Error Handling

`GamePatch` never throws. All failures are routed to `ModError.ReportInternal` with context about the type and method involved. The call site receives `false`.

| Failure condition | Reported as |
|---|---|
| `harmony` is null | `"Harmony instance is null"` |
| Method not found after hierarchy walk | `"Method '{name}' not found on {type}"` |
| Harmony patch throws | Exception message with type and method context |

---

## Method Reference

| Method | Patch type | Returns | Default on failure |
|---|---|---|---|
| `Postfix` | Harmony postfix | `bool` | `false` |
| `Prefix` | Harmony prefix | `bool` | `false` |
