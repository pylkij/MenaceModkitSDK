# GameMethod API Reference

`GameMethod` is a static class in the `Menace.SDK` namespace. It resolves IL2CPP methods via expression trees and invokes them on game objects — both static singletons and live instances. Type and method resolution happens at compile time, so failures surface as compiler errors rather than silent runtime misses.

---

## How It Works

`GameMethod` uses C# expression trees to capture method references at compile time. You pass a lambda that *calls* the method you want — the class extracts the `MethodInfo` from the expression and invokes it via reflection. This means you never write string-based method names; the compiler validates the reference for you.

```csharp
// The expression captures the method reference — the call never actually executes
GameMethod.Call<SomeGameType>(instance, x => x.DoThing());
```

On failure, all methods return a safe default (`null`, `0`, or `false`) and report to `ModError` rather than throwing.

---

## Static Calls

For singletons, manager types, and factory methods where no instance is needed.

### `CallStatic`

```csharp
object CallStatic<TType>(
    Expression<Action<TType>> methodExpr,
    object[] args = null)
```

Invokes a static method and returns the result as `object`. Returns `null` on failure.

```csharp
GameMethod.CallStatic<GameManager>(x => x.ResetState());

GameMethod.CallStatic<UnitFactory>(
    x => x.SpawnUnit(default, default),
    new object[] { prefabId, spawnTile });
```

---

## Instance Calls

For live game objects obtained via `GameObj` or event hook pointers.

### `Call`

```csharp
object Call<TType>(
    object instance,
    Expression<Action<TType>> methodExpr,
    object[] args = null)
```

Invokes an instance method and returns the result as `object`. Returns `null` on failure or if `instance` is null.

```csharp
var result = GameMethod.Call<TacticalManager>(__instance, x => x.IsPlayerTurn());
```

---

## Typed Convenience Wrappers

These wrap `Call` and cast the result to a specific type, returning a safe default if the call fails or the type doesn't match.

### `CallInt`

```csharp
int CallInt<TType>(
    object instance,
    Expression<Func<TType, int>> methodExpr,
    object[] args = null)
```

Returns the result as `int`. Returns `0` on failure.

```csharp
int ap = GameMethod.CallInt<TacticalManager>(__instance, x => x.GetRound());
```

---

### `CallBool`

```csharp
bool CallBool<TType>(
    object instance,
    Expression<Func<TType, bool>> methodExpr,
    object[] args = null)
```

Returns the result as `bool`. Returns `false` on failure.

```csharp
bool alive = GameMethod.CallBool<TacticalManager>(__instance, x => x.IsPlayerTurn());
```

---

## Passing Arguments

All methods accept an optional `object[]` args array. The lambda parameters act as placeholders — their values are ignored at resolution time. Pass the actual arguments in `args`, in the same order as the method signature.

```csharp
// Method signature: void ApplyDamage(int amount, bool isArmourPiercing)
GameMethod.Call<Actor>(
    actorInstance,
    x => x.ApplyDamage(default, default),   // placeholders only
    new object[] { 25, true });              // actual values
```

> **Note:** `default` is the recommended placeholder for value types. For reference type parameters, `null` works equally well. The expression captures the method reference only — argument values in the lambda are discarded.

---

## Error Handling

`GameMethod` never throws. All failures are routed to `ModError.ReportInternal` with context about the type and method involved. The call site receives a safe default:

| Return type | Value on failure |
|---|---|
| `object` | `null` |
| `int` | `0` |
| `bool` | `false` |

A null `instance` is caught before the expression is evaluated and reported separately, so null-check errors are distinguishable from invocation failures in the error log.

---

## Method Reference

| Method | Expression type | Returns | Default on failure |
|---|---|---|---|
| `CallStatic<TType>` | `Action<TType>` | `object` | `null` |
| `Call<TType>` | `Action<TType>` | `object` | `null` |
| `CallInt<TType>` | `Func<TType, int>` | `int` | `0` |
| `CallBool<TType>` | `Func<TType, bool>` | `bool` | `false` |