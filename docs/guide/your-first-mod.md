# Your First Mod

This tutorial will walk you step by step through the creation of your first ever mod for the game MENACE using C#, the MelonLoader, and the Menace Modkit (MMK). We will go step by step through the stages of creating a mod, with each step building on the framework established by the step before. This tutorial will cover setup, planning, implementation, debugging, and patching.

This is **not** an exhaustive reference for all things possible. Instead, think of this tutorial as a starting point to get you comfortable with making a basic mod from which you can explore your own dreams and ambitions. Starting with the basics will save you a lot of time and effort in the future. Trust me: I started my mod making journey by jumping straight in to the deep end and was only saved by a timely intervention.

## Setup

Every mod for MENACE written in C# will follow a similar basic structure. As you get more advanced, these hard and fast rules will become guidelines. But for now, stick to established patterns. Not only does this make your life easier, but it also makes helping you easier for other modders. The easier your code is to read and follow, the better it is as a beginner.

### Directory Structure

The directory structure will be created by the MMK in the staging folder when you click `+ Create New` on the `Mod Loader` tab of the MMK. By default, on Windows this will be `C:\Users\YourUserName\Documents\MenaceModkit\staging`. For now, the only directory we are interested in is `src/`. `build/` will be handled by the MMK when you deploy your mod by clicking `Deploy to Game` in the MMK for the first time.

```
YourFirstMod/
├── assets/
├── build/
│       └── YourFirstMod.dll
├── clones/
├── src/
│       └── YourFirstMod.cs
├── stats/
└── modpack.json
```

### YourFirstMod.cs

Create a `.cs` file in the `src/` directory and add this:

```csharp
using System;
using MelonLoader;
using Menace.SDK;
using Menace.ModpackLoader;

namespace YourFirstMod;

public class Plugin : IModpackPlugin
{
    private SdkLog _log;
    private HarmonyLib.Harmony _harmony;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = new SdkLog("YourFirstMod", logger);
        _harmony = harmony;
        _log.Msg("YourFirstMod loaded.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

This is your mod. Currently, it doesn't do much: it will compile, it will be successfully loaded, and it will print a log line to let you know it was successful to the MelonLoader log (`Menace/MelonLoader/Latest.log`).

Before moving on, let's take a moment to understand what you are looking at. Your mod implements `IModpackPlugin`, which is the contract between your code and the Modpack Loader. Each method is a lifecycle hook that the loader calls at a specific point:

| Method | When it fires |
|---|---|
| `OnInitialize` | Once, when your mod is first loaded. Wire up events and patches here. |
| `OnSceneLoaded` | Each time a Unity scene loads. Receives the scene's build index and name. |
| `OnUpdate` | Every frame. Use sparingly — this runs constantly. |
| `OnGUI` | Every frame during the GUI pass. For on-screen overlays. |
| `OnUnload` | When your mod is unloaded. Always clean up subscriptions here. |

The `MelonLogger.Instance` and `HarmonyLib.Harmony` instances are injected by the loader rather than constructed by you. You don't need to worry about creating them — just store them and use them.

### modpack.json

This tells the MMK what it needs to do: what files to compile, where they are located, and what the mod contains.

```json
{
  "manifestVersion": 2,
  "name": "YourFirstMod",
  "version": "1.0.0",
  "author": "YourName",
  "description": "A basic guide to getting started modding the game MENACE using C#",
  "createdDate": "2026-03-31T10:12:17.4186251-07:00",
  "modifiedDate": "2026-03-31T10:12:17.4186531-07:00",
  "loadOrder": 100,
  "dependencies": [],
  "code": {
    "sources": [
      "src/YourFirstMod.cs"
    ],
    "references": [
      "MelonLoader",
      "HarmonyLib",
      "Menace.ModpackLoader"
    ],
    "prebuiltDlls": [],
    "hasAnySources": true,
    "hasAnyPrebuilt": false,
    "hasAnyCode": true
  },
  "patches": {},
  "bundles": [],
  "assets": {},
  "securityStatus": "SourceVerified",
  "repositoryType": "None",
  "hasCode": true,
  "hasPatches": false,
  "hasBundles": false,
  "hasAssets": false
}
```

The fields you will actually touch are `name`, `version`, `author`, `description`, `sources`, and `references`. The rest is managed by the MMK.

### First Launch

Hit `Deploy to Game` once all of your files are in place. The MMK will compile `YourFirstMod.dll`. I find that you have to deploy any new `.dll` twice — often, the first compile will glitch, so just `Undeploy` and then `Deploy to Game` again.

Now, launch the game and get all the way to the Title Screen. Open `Latest.log` and check for these lines:

```
[10:20:57.497] [Menace_Modpack_Loader] Loading modpacks from: C:\Program Files (x86)\Steam\steamapps\common\Menace\Mods
[10:20:57.564] [Menace_Modpack_Loader]   Loaded [v2]: YourFirstMod v1.0.0 (order: 100)
[10:20:57.573] [Menace_Modpack_Loader]   [YourFirstMod] Loaded DLL: YourFirstMod.dll [source-verified]
[10:20:57.575] [Menace_Modpack_Loader]   [YourFirstMod] Discovered plugin: Plugin
[10:20:57.578] [Menace_Modpack_Loader] Loaded 1 modpack(s)
[10:20:57.579] [YourFirstMod] YourFirstMod loaded.
```

Congratulations, you have modded MENACE. Your mod doesn't do anything yet, but it works. A solid start.

## Planning

Now that we have a mod that compiles, loads, and prints a log message, let's move on to the planning stage. In this step, we will identify a problem, propose a solution, and collect the required information to implement the proposed solution. We don't want to write any code at this stage. Instead, try to keep the descriptions in plain language.

### The Problem

When attacking a tile with a hidden enemy on it with direct fire weapons, units suffer no accuracy penalty — this idea comes courtesy of Beagle.

### The Proposed Solution

To counteract this, we want a mod which:

1. Detects when an attack is made.
2. Checks if the tile being attacked has a unit on it.
3. Checks if that unit is hidden to the attacker.
4. Checks if the attack is a direct fire attack (not indirect fire).
5. Applies a 20% accuracy penalty.

### The How

The next step during the planning process is identifying **how** all of these steps might be accomplished using the available tools. The first place to check should always be the [Menace SDK](https://pylkij.github.io/MenaceModkitSDK/guide/what-is-menace-sdk). This is a fantastic resource for modding that allows us to avoid as much IL2CPP shenanigans as possible. It is exposed through the `Menace.SDK` namespace and requires no additional effort on the part of the modder.

Next, we will review the [API Documentation](https://pylkij.github.io/MenaceModkitSDK/api/) and see what parts should help us accomplish each of our goals:

1. [TacticalEventHooks](https://pylkij.github.io/MenaceModkitSDK/guide/tactical-event-hooks)
    - This is how we will detect when an attack is made.
    - We will subscribe to `OnAttackTileStart` during `OnInitialize`.
    - Conveniently, this event delivers the attacker, the skill used, and the target tile as `IntPtr` handles.
2. [TileMap](https://pylkij.github.io/MenaceModkitSDK/api/tactical/tile-map)
    - This is how we will check if the tile has an actor on it.
    - `GetActorOnTile` will tell us who is on the tile, and will return `GameObj.Null` if the tile is empty.
3. [LineOfSight](https://pylkij.github.io/MenaceModkitSDK/api/tactical/line-of-sight)
    - This is how we will check if the attacker can see the target.
    - `CanActorSee` takes an `actor` and a `target` and returns `true` or `false`.
4. The skill pointer from `OnAttackTileStart` — we will use this to check `IsLineOfFireNeeded` on the skill template to determine if the attack is direct fire.
5. For the accuracy penalty itself, we will need to patch `GetAccuracy` directly using Harmony. This is covered in the Patching section below.

## Implementation

In order for the code to do what we want, we need to take all of these parts and make them work together. First, let's add the SDK namespace:

```csharp
using System;

using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK; // <-- New

namespace YourFirstMod;
```

Next, let's subscribe to `OnAttackTileStart`. The SDK handles all of the underlying Harmony wiring for you — all you need to do is subscribe to the event:

```csharp
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = new SdkLog("YourFirstMod", logger);
        _harmony = harmony; // we will need this for later
        _log.Msg("YourFirstMod loaded.");

        TacticalEventHooks.OnAttackTileStart += OnAttackTileStart;
    }
```

Any time you subscribe to an event, you also need to clean up when your mod is unloaded. This prevents stale handlers from causing problems:

```csharp
    public void OnUnload()
    {
        TacticalEventHooks.OnAttackTileStart -= OnAttackTileStart;
    }
```

Now, let's write the handler. The full signature for `OnAttackTileStart` is:

```csharp
Action<IntPtr, IntPtr, IntPtr, float>
// (attacker, skill, tile, attackDurationInSeconds)
```

The parameters arrive as raw `IntPtr` handles into the game's IL2CPP memory. The SDK's `GameObj` wrapper is how we interact with them:

```csharp
    // When an attack starts
    private void OnAttackTileStart(IntPtr attackerPtr, IntPtr skillPtr, IntPtr tilePtr, float duration)
    {
        _log.Msg("An attack has started.");

        var attackerObj = new GameObj(attackerPtr);
        var tileObj = new GameObj(tilePtr);
        var skillObj = new GameObj(skillPtr);

        // Is there an actor on the target tile?
        var target = TileMap.GetActorOnTile(tileObj);
        if (target == GameObj.Null)
        {
            _log.Msg("There was no target on that tile.");
            return;
        }

        // Can the attacker see the target?
        if (LineOfSight.CanActorSee(attackerObj, target))
        {
            _log.Msg("The attacker can see the target.");
            return;
        }

        // Is this a direct fire attack?
        // We check IsLineOfFireNeeded on the skill template. If line of fire
        // is not needed (i.e. it's an indirect fire skill), we skip the debuff.
        var templateProp = skillObj.GetType().GetProperty("m_Template",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (templateProp != null)
        {
            var template = templateProp.GetValue(skillObj);
            if (template != null)
            {
                var losProp = template.GetType().GetProperty("IsLineOfFireNeeded",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (losProp != null)
                {
                    bool isLoFNeeded = (bool)losProp.GetValue(template);
                    if (!isLoFNeeded)
                    {
                        _log.Msg("Skill does not require line of fire, skipping debuff.");
                        return;
                    }
                }
            }
        }

        // Set the flag — the accuracy patch will apply the debuff
        _applyAccuracyDebuff = true;
    }
```

And, add `System.Reflection` to your using declarations:

```csharp
using System;
using System.Reflection;
```

We now know whether to apply the debuff, but we still need to actually apply it. That requires patching directly, which we will cover next.

## Patching

`Intercept.OnGetAccuracy` is the SDK's intended route for modifying accuracy, but it is not yet stable. Fortunately, all the SDK is doing under the hood is patching — so we can do the same thing ourselves, just a little more directly.

There are different types of Harmony patches. The two you will use most are `Prefix` and `Postfix`. Think of it this way: am I trying to intercept the method *before* it does anything, or am I trying to modify its *result* after it runs? We want to modify the result, so we use a `Postfix`.

Because our patch method will be called by Harmony outside of the normal instance context, `_log`, `_harmony`, and our new `_applyAccuracyDebuff` flag all need to be `static`:

```csharp
    private static SdkLog _log;
    private static HarmonyLib.Harmony _harmony;
    private static bool _applyAccuracyDebuff = false;
```

Now, let's set up the patch in `OnInitialize`. We will use `GameState.FindManagedType` to locate the target type at runtime — more on why this matters in the Debugging section below. Once we have the type, `GamePatch.Postfix` handles the method lookup, the `HarmonyMethod` wiring, and error reporting for us:

```csharp
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = new SdkLog("YourFirstMod", logger);
        _harmony = harmony;
        _log.Msg("YourFirstMod loaded.");

        TacticalEventHooks.OnAttackTileStart += OnAttackTileStart;

        try
        {
            Patch_OnGetAccuracy();
        }
        catch (Exception ex)
        {
            _log.Error("Failed to patch GetAccuracy:");
            _log.Error(ex.ToString());
        }
    }

    private static void Patch_OnGetAccuracy()
    {
        var accuracyPostfix = typeof(Plugin).GetMethod(nameof(OnGetAccuracy_Postfix),
            BindingFlags.Static | BindingFlags.NonPublic);

        bool ok = GamePatch.Postfix(_harmony, typeof(Il2CppMenace.Tactical.EntityProperties), "GetAccuracy", accuracyPostfix);
        if (ok)
            _log.Msg("Patched GetAccuracy.");
    }
```

And the postfix itself — Harmony injects `__result` as a `ref` parameter, letting us modify the return value of the original method:

```csharp
    private static void OnGetAccuracy_Postfix(object __instance, ref float __result)
    {
        if (_applyAccuracyDebuff)
        {
            __result *= 0.8f;
            _applyAccuracyDebuff = false;
            _log.Msg("Accuracy debuff applied.");
        }
    }
```

The flow is: `OnAttackTileStart` sets `_applyAccuracyDebuff = true` when conditions are met, the game calls `GetAccuracy` to calculate the shot, our postfix fires and multiplies the result by 0.8, then resets the flag.

## Smoke Test

Let's deploy, get in game, and try it.

```
[13:22:10.632] [YourFirstMod] Patched GetAccuracy.
```

Now let's make sure the event hook is working too. Load into a tactical mission and make an attack:

```
[14:01:12.110] [YourFirstMod] An attack has started.
[14:01:12.110] [YourFirstMod] There was no target on that tile.
```

Good. Let's try the cases one by one:

1. An empty tile: `[YourFirstMod] There was no target on that tile.` ✓
2. A visible enemy: `[YourFirstMod] The attacker can see the target.` ✓
3. An indirect fire skill against a hidden enemy: `[YourFirstMod] Skill does not require line of fire, skipping debuff.` ✓
4. A direct fire skill against a hidden enemy:

```
[14:42:16.044] [YourFirstMod] An attack has started.
[14:42:16.045] [YourFirstMod] Accuracy debuff applied.
```

All four cases behave correctly. 

## Cleanup

We probably don't want users getting spammed with development log lines when they use our mod. There are two options:

1. Remove them.
2. Leave them behind a debug logging gate.

I typically go for option 2, as it helps me check if I break things when adding features later, or if game code changes under me. Add a new flag:

```csharp
    private static SdkLog _log;
    private static HarmonyLib.Harmony _harmony;
    private static bool _applyAccuracyDebuff = false;
    private static bool _debugLogging = false;
```

Then gate the noisy lines: `if (_debugLogging) _log.Msg("some message");`

Leave a few lines ungated — errors are always useful to surface, and the `"Patched ..."` confirmation is nice for users to see so they can verify the mod is active.

All that's left is to `Export Modpack` and share it with others!

## Closing Thoughts

By getting to this part of the tutorial, you have learned how to:

1. Set up a new mod for the first time.
2. Understand the `IModpackPlugin` lifecycle.
3. Plan a mod to solve a real problem.
4. Research the required tools using the SDK documentation.
5. Implement your plan using SDK event hooks.
6. Reach beyond the SDK with a direct Harmony patch when you need to.
8. Leave behind clean log lines for everyone who follows.

Coding is an iterative process, and it will only get easier with time and practice. Each problem solved is a future problem identified. Each solution is a tool you can apply to a future problem. So go forth, and create some cool stuff.