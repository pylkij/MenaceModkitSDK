# A Guide to Event Hooks

Your mod needs to *react* to things. A leader dies permanently. A faction turns hostile. An actor gets suppressed mid-firefight. You could try to detect these moments by polling game state every frame in `OnUpdate` — but that's fragile, expensive, and frankly unnecessary. The SDK gives you a much cleaner tool: event hooks.

This guide will walk you through what they are, how they work, and how to put them to use. By the end, you will have a working mod that responds to both a tactical event and a strategy event, and you will understand why the system is split the way it is.

---

## Two Layers, Two Classes

Before touching any code, it helps to understand why there are two separate hook classes.

MENACE has two distinct gameplay layers. There is the **strategy layer** — your roster, your faction relationships, the metagame between missions. And there is the **tactical layer** — the combat itself, actors moving on tiles, skills firing, rounds ticking over. The SDK mirrors this split exactly:

- **`TacticalEventHooks`** is for everything that happens *inside a mission*.
- **`StrategyEventHooks`** is for everything that happens *between missions*.

The two layers don't run at the same time, so each class only fires events in the appropriate context. Once that clicks, the split stops feeling arbitrary.

---

## Setup

If you have worked through the [Your First Mod](your-first-mod.md) tutorial, your `modpack.json` already references `MelonLoader`, `HarmonyLib`, and `Menace.ModpackLoader`. To use the event hooks, you need to add one more reference:

```json
"references": [
    "MelonLoader",
    "HarmonyLib",
    "Menace.ModpackLoader",
    "Menace.SDK"
]
```

Then add the namespace at the top of your `.cs` file:

```csharp
using Menace.SDK;
```

That is all the setup required. The hook classes are static — there is nothing to instantiate or configure.

---

## How It Works

Every event is a standard C# `Action` delegate. You subscribe with `+=` in `OnInitialize` and unsubscribe with `-=` in `OnUnload`. The hooks fire automatically once subscribed.

```csharp
public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
{
    _log = logger;
    _harmony = harmony;

    TacticalEventHooks.OnActorKilled += OnActorKilled;
}

public void OnUnload()
{
    TacticalEventHooks.OnActorKilled -= OnActorKilled;
}
```

The unsubscribe step is not optional. The hook classes are static, which means your subscriptions persist until you explicitly remove them. If you forget `OnUnload`, your handler can fire against a partially unloaded mod and cause crashes. Make a habit of it now.

Event parameters come through as `IntPtr` handles into the game's IL2CPP memory — you cannot cast these directly to game types. Instead, wrap them in `GameObj` and use the SDK's methods from there:

```csharp
private void OnActorKilled(IntPtr actorPtr, IntPtr killerPtr, int killerFaction)
{
    var actor = new GameObj(actorPtr);
    var killer = new GameObj(killerPtr);

    if (actor.IsNull || killer.IsNull) return;

    _log.Msg($"{actor.GetName()} was killed by {killer.GetName()}");
}
```

**Always check `IsNull` before calling anything on a `GameObj`.** The game can pass null pointers in edge cases. It will cost you nothing to check, and it will save you from crashes that are very annoying to track down.

---

## A Working Example

Let's build something concrete: a mod that tracks kills during a mission, prints a round-by-round summary, and logs a warning to the console whenever a leader is permanently lost. That last part is a strategy layer event — so this example will use both classes at once.

Here is the full plugin:

```csharp
using System;
using System.Collections.Generic;

using MelonLoader;
using HarmonyLib;
using Menace.ModpackLoader;

using Menace.SDK;

namespace KillTracker;

public class Plugin : IModpackPlugin
{
    private static MelonLogger.Instance _log;
    private static HarmonyLib.Harmony _harmony;

    private readonly Dictionary<string, int> _missionKills = new();

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _harmony = harmony;
        _log.Msg("KillTracker loaded.");

        TacticalEventHooks.OnActorKilled += OnActorKilled;
        TacticalEventHooks.OnRoundEnd += OnRoundEnd;
        StrategyEventHooks.OnLeaderPermadeath += OnLeaderPermadeath;
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _missionKills.Clear();
    }

    private void OnActorKilled(IntPtr actorPtr, IntPtr killerPtr, int killerFaction)
    {
        var actor = new GameObj(actorPtr);
        var killer = new GameObj(killerPtr);

        if (actor.IsNull || killer.IsNull) return;

        var killerName = killer.GetName();

        if (!_missionKills.ContainsKey(killerName))
            _missionKills[killerName] = 0;

        _missionKills[killerName]++;
    }

    private void OnRoundEnd(int roundNumber)
    {
        _log.Msg($"--- End of round {roundNumber} ---");

        foreach (var (name, kills) in _missionKills)
            _log.Msg($"  {name}: {kills} kill(s)");
    }

    private void OnLeaderPermadeath(IntPtr leaderPtr)
    {
        var leader = new GameObj(leaderPtr);
        if (leader.IsNull) return;

        _log.Msg($"[PERMADEATH] {leader.GetName()} has been permanently lost.");
    }

    public void OnUpdate() { }
    public void OnGUI() { }

    public void OnUnload()
    {
        TacticalEventHooks.OnActorKilled -= OnActorKilled;
        TacticalEventHooks.OnRoundEnd -= OnRoundEnd;
        StrategyEventHooks.OnLeaderPermadeath -= OnLeaderPermadeath;
    }
}
```

A few things worth pointing out here.

`OnSceneLoaded` is used to reset `_missionKills` between missions. Tactical state — who's alive, what happened this mission — should not carry over when a new scene loads. Strategy state, like tracking permadeaths across a campaign, can be kept alive as long as the plugin is loaded. Keep in mind that this only lasts as long as the game is running. If you want state to survive between sessions, you will need to write it to disk.

`OnRoundEnd` fires *before* the round counter increments. That means the `roundNumber` it passes is the round that just finished, which is exactly what we want for a summary. If you used `OnRoundStart` instead, you would get the new round number — not wrong, just a different moment.

`OnLeaderPermadeath` is a strategy event. It will never fire during a tactical mission. It fires when you are back in the metagame layer and the game processes the leader's death. You subscribe to it exactly the same way as a tactical event — the SDK handles the context difference for you.

---

## Smoke Test

Deploy, launch the game, and check the log for the confirmation line:

```
[13:22:10.632] [KillTracker] KillTracker loaded.
```

Load into a mission and play through a round or two. After each round ends you should see something like:

```
[14:01:55.201] [KillTracker] --- End of round 1 ---
[14:01:55.201] [KillTracker]   Rewa: 2 kill(s)
[14:01:55.201] [KillTracker]   Yaz: 1 kill(s)
```

And if a leader is permanently lost during the strategy phase:

```
[14:42:16.044] [KillTracker] [PERMADEATH] Singh has been permanently lost.
```

All three events working. That is a solid foundation to build from.

---

## What's Available

The two classes expose a lot of events between them. Here are the ones you will reach for most often.

### Tactical

| Event | When it fires |
|---|---|
| `OnActorKilled` | An actor dies |
| `OnDamageReceived` | Any entity takes damage |
| `OnBleedingOut` | A leader enters bleed-out state |
| `OnStabilized` | A bleeding-out leader is stabilized |
| `OnSuppressed` | An actor becomes suppressed |
| `OnSkillUsed` | An actor activates a skill |
| `OnMovementFinished` | An actor completes a move |
| `OnRoundStart` / `OnRoundEnd` | Round boundaries |
| `OnPlayerTurn` / `OnAITurn` | Whose turn it is |
| `OnObjectiveStateChanged` | A mission objective changes state |
| `OnEntitySpawned` | A new entity enters the scene |

### Strategy

| Event | When it fires |
|---|---|
| `OnLeaderHired` | A leader joins the roster |
| `OnLeaderDismissed` | A leader is removed from the roster |
| `OnLeaderPermadeath` | A leader dies permanently |
| `OnLeaderLevelUp` | A leader gains a perk |
| `OnFactionTrustChanged` | A faction's trust value changes |
| `OnFactionStatusChanged` | A faction changes to Allied, Hostile, Neutral, etc. |
| `OnBlackMarketRestocked` | The Black Market refreshes its inventory |

For the complete list of events and their parameter signatures, see the [TacticalEventHooks](https://pylkij.github.io/MenaceModkitSDK/api/events/tactical-event-hooks) and [StrategyEventHooks](https://pylkij.github.io/MenaceModkitSDK/api/events/strategy-event-hooks API references.

---

## One Thing to Be Aware Of

`StrategyEventHooks` lists four events — `OnOperationStarted`, `OnOperationFinished`, `OnMissionStarted`, and `OnMissionFinished` — that are **currently disabled**. The underlying patches were commented out due to a crash in the base game code. They appear in the API reference because the infrastructure is in place for when a safe patch point is found, but they will not fire at runtime. Do not build anything that depends on them.
