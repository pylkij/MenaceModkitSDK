# Error Logging

This guide covers how to log errors and exceptions in your mod using MelonLoader's built-in logger. If you followed *Your First Mod*, you already have everything you need — `_log` is your logger, and you have been using it since the beginning.

## The Four Log Levels

`MelonLogger.Instance` has four methods, each of which maps to a different severity:

| Method | When to use it |
|---|---|
| `_log.Msg` | Normal operation. Things going as expected. |
| `_log.Warning` | Something is off, but your mod can continue. |
| `_log.Error` | Something went wrong. Worth investigating. |
| `_log.Error` (with exception) | Something threw. Always log these. |

In `Latest.log`, each level is visually distinct, which makes scanning a log much faster when something goes wrong.

## Logging an Exception

The most important habit to build early is logging exceptions properly. When something throws, you want the full picture: what you were trying to do, and exactly what the exception says.

```csharp
try
{
    var result = SomeSDKMethod();
}
catch (Exception ex)
{
    _log.Error("Failed to call SomeSDKMethod:");
    _log.Error(ex.ToString());
}
```

`ex.ToString()` gives you the exception type, message, and full stack trace — everything you need to pinpoint the problem. `ex.Message` alone is often not enough, since it strips the stack trace.

In `Latest.log` this will look like:

```
[14:01:12.110] [YourMod] Failed to call SomeSDKMethod:
[14:01:12.111] [YourMod] System.NullReferenceException: Object reference not set to an instance of an object.
  at YourMod.Plugin.OnAttackTileStart (System.IntPtr attackerPtr ...) [0x00042] in YourFirstMod.cs:87
```

That second line is where you want to look. The file name and line number will take you directly to the problem.

## Where to Wrap in Try/Catch

You do not need to wrap everything. Focus on the places where failures are most likely and most damaging:

**Patch setup.** If a patch fails to apply, the rest of the mod will behave incorrectly in ways that are hard to diagnose. Always wrap patch setup and log the result clearly:

```csharp
try
{
    Patch_GetAccuracy();
}
catch (Exception ex)
{
    _log.Error("Failed to patch GetAccuracy — mod will not function correctly:");
    _log.Error(ex.ToString());
}
```

**Event handlers.** An unhandled exception in an event handler can cause problems for other mods listening to the same event, or for the game itself. Wrap the body of your handlers:

```csharp
private void OnAttackTileStart(IntPtr attackerPtr, IntPtr skillPtr, IntPtr tilePtr, float duration)
{
    try
    {
        // your logic here
    }
    catch (Exception ex)
    {
        _log.Error("Exception in OnAttackTileStart:");
        _log.Error(ex.ToString());
    }
}
```

**Reflection.** Any time you use `GetProperty`, `GetMethod`, or `GetValue`, the result can be null or the call can throw. Treat it as guilty until proven innocent.

You generally do not need try/catch inside `OnUpdate` for simple logic, or around SDK calls that have their own documented null returns — checking for `GameObj.Null` is enough there.

## Writing Useful Error Messages

The error message you write is as important as logging the exception itself. When you are reading a log at 2am trying to figure out why your mod broke, you will thank yourself for being specific.

**Bad:**
```csharp
_log.Error("Something went wrong.");
```

**Good:**
```csharp
_log.Error($"Failed to resolve actor on tile ({tileObj}) — skipping attack handler.");
```

Include whatever context you have: what you were trying to do, what values were involved, and what the mod will do as a result (skip, abort, fall back).

## Debug Logging

As covered in *Your First Mod*, keeping debug log lines behind a flag is better than deleting them. They are invaluable when a user reports a bug or when a game update breaks something under you.

```csharp
private static bool _debugLogging = false;
```

```csharp
if (_debugLogging) _log.Msg($"Actor resolved: {target}");
```

The rule for what stays ungated:

- Errors and warnings always surface — a user seeing an error line in a log is useful, not alarming.
- Patch confirmations stay ungated. `[YourMod] Patched GetAccuracy.` lets users verify the mod is active at a glance.
- Everything else goes behind the flag.

## Putting It Together

A well-instrumented `OnInitialize` looks like this:

```csharp
public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
{
    _log = logger;
    _harmony = harmony;
    _log.Msg("YourMod loaded.");

    TacticalEventHooks.OnAttackTileStart += OnAttackTileStart;

    try
    {
        Patch_GetAccuracy();
    }
    catch (Exception ex)
    {
        _log.Error("Failed to patch GetAccuracy — accuracy debuff will not apply:");
        _log.Error(ex.ToString());
    }
}
```

And a well-instrumented handler:

```csharp
private void OnAttackTileStart(IntPtr attackerPtr, IntPtr skillPtr, IntPtr tilePtr, float duration)
{
    try
    {
        var attackerObj = new GameObj(attackerPtr);
        var tileObj = new GameObj(tilePtr);

        var target = TileMap.GetActorOnTile(tileObj);
        if (target == GameObj.Null)
        {
            if (_debugLogging) _log.Msg("No target on tile, skipping.");
            return;
        }

        // rest of your logic
    }
    catch (Exception ex)
    {
        _log.Error("Exception in OnAttackTileStart:");
        _log.Error(ex.ToString());
    }
}
```

When something goes wrong, `Latest.log` will tell you exactly where, exactly what threw, and exactly what state the mod was in. That is the goal.
