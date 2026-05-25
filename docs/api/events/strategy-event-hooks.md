# StrategyEventHooks API Reference

`StrategyEventHooks` is a static class in the `Menace.SDK` namespace. It wraps Harmony postfix patches on strategy-layer classes (`Roster`, `StoryFaction`, `Squaddies`, `BlackMarket`, `EmotionalStates`, and others) and exposes them as standard C# events and Lua callbacks. You subscribe to the events you care about — the hooks fire automatically once initialized.

---

## Quick Reference

| C# Event | Lua Event Name | Category |
|---|---|---|
| `OnLeaderHired` | `leader_hired` | Roster |
| `OnLeaderDismissed` | `leader_dismissed` | Roster |
| `OnLeaderPermadeath` | `leader_permadeath` | Roster |
| `OnLeaderLevelUp` | `leader_levelup` | Roster |
| `OnFactionTrustChanged` | `faction_trust_changed` | Faction |
| `OnFactionStatusChanged` | `faction_status_changed` | Faction |
| `OnFactionUpgradeUnlocked` | `faction_upgrade_unlocked` | Faction |
| `OnSquaddieKilled` | `squaddie_killed` | Squaddie |
| `OnOperationStarted` | `operation_started` *(disabled)* | Operation |
| `OnOperationFinished` | `operation_finished` *(disabled)* | Operation |
| `OnMissionStarted` | `mission_started` *(disabled)* | Operation |
| `OnMissionFinished` | `mission_finished` *(disabled)* | Operation |
| `OnBlackMarketItemAdded` | `blackmarket_item_added` | Black Market |
| `OnBlackMarketRestocked` | `blackmarket_restocked` | Black Market |
| `OnTriggerEmotion` | `emotion_triggered` | Emotional State |

---

## How to Subscribe (C#)

All events are standard C# `Action` delegates on a static class, so subscription is straightforward:

```csharp
StrategyEventHooks.OnLeaderHired += (leaderPtr) =>
{
    // your logic here
};
```

Parameters are `IntPtr` handles into the game's IL2CPP memory. Use the SDK's `GameObj` wrapper to interact with them.

---

## How to Subscribe (Lua)

Every active event also fires a Lua callback via `LuaScriptEngine`. Use the `on()` function with the event name string:

```lua
on("leader_hired", function(data)
    log(data.leader .. " joined the roster")
end)
```

The `data` table contains named fields — see each event below for the available keys. Most objects provide both a name string and a `_ptr` field (an `int64`) for cases where you need to pass the pointer back to SDK methods.

---

## Quick Start

```csharp
using System;

using MelonLoader;
using HarmonyLib;

using Menace.SDK;

namespace MyPlugin;

public class MyPlugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        StrategyEventHooks.OnLeaderHired += OnLeaderHired;
        StrategyEventHooks.OnFactionTrustChanged += OnFactionTrustChanged;
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }

    private void OnLeaderHired(IntPtr leaderPtr)
    {
        var leader = new GameObj(leaderPtr);
        if (leader.IsNull) return;

        SdkLogger.Msg($"{leader.GetName()} was added to the roster");
    }

    private void OnFactionTrustChanged(IntPtr factionPtr, int delta)
    {
        var faction = new GameObj(factionPtr);
        if (faction.IsNull) return;

        SdkLogger.Msg($"{faction.GetName()} trust changed by {delta}");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload()
    {
        StrategyEventHooks.OnLeaderHired -= OnLeaderHired;
        StrategyEventHooks.OnFactionTrustChanged -= OnFactionTrustChanged;
    }
}
```

---

## Event Reference

### Roster Events

#### `OnLeaderHired`
Fires when a leader is successfully hired into the roster. Does not fire if the hire attempt fails.

```csharp
event Action<IntPtr> OnLeaderHired
// (leader)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Name of the hired leader |
| `leader_ptr` | int64 | Pointer to the leader template |
| `template` | string | Name of the leader template used |

Lua event name: `"leader_hired"`

---

#### `OnLeaderDismissed`
Fires when a leader is successfully dismissed from the roster. Does not fire if the dismiss attempt fails.

```csharp
event Action<IntPtr> OnLeaderDismissed
// (leader)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Name of the dismissed leader |
| `leader_ptr` | int64 | Pointer to the leader |

Lua event name: `"leader_dismissed"`

---

#### `OnLeaderPermadeath`
Fires when a leader is permanently killed and removed from the roster.

```csharp
event Action<IntPtr> OnLeaderPermadeath
// (leader)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Name of the leader who died permanently |
| `leader_ptr` | int64 | Pointer to the leader |

Lua event name: `"leader_permadeath"`

---

#### `OnLeaderLevelUp`
Fires when a leader gains a perk (i.e. levels up).

```csharp
event Action<IntPtr, IntPtr> OnLeaderLevelUp
// (leader, perk)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Name of the leader who leveled up |
| `leader_ptr` | int64 | Pointer to the leader |
| `perk` | string | Name of the perk that was added |

Lua event name: `"leader_levelup"`

---

### Faction Events

#### `OnFactionTrustChanged`
Fires when a faction's trust value changes. Does not fire for zero-delta changes.

```csharp
event Action<IntPtr, int> OnFactionTrustChanged
// (faction, delta)
```

| Lua key | Type | Description |
|---|---|---|
| `faction` | string | Name of the faction |
| `faction_ptr` | int64 | Pointer to the faction |
| `delta` | int | Amount of trust change (positive or negative) |

Lua event name: `"faction_trust_changed"`

---

#### `OnFactionStatusChanged`
Fires when a faction's status is set (e.g. Allied, Hostile, Neutral).

```csharp
event Action<IntPtr, int> OnFactionStatusChanged
// (faction, newStatus)
```

| Lua key | Type | Description |
|---|---|---|
| `faction` | string | Name of the faction |
| `faction_ptr` | int64 | Pointer to the faction |
| `status` | int | New status value |

Lua event name: `"faction_status_changed"`

---

#### `OnFactionUpgradeUnlocked`
Fires when a faction upgrade is unlocked.

```csharp
event Action<IntPtr, IntPtr> OnFactionUpgradeUnlocked
// (faction, upgrade)
```

| Lua key | Type | Description |
|---|---|---|
| `faction` | string | Name of the faction |
| `faction_ptr` | int64 | Pointer to the faction |
| `upgrade` | string | Name of the unlocked upgrade |
| `upgrade_ptr` | int64 | Pointer to the upgrade |

Lua event name: `"faction_upgrade_unlocked"`

---

### Squaddie Events

#### `OnSquaddieKilled`
Fires when a squaddie is successfully killed. Does not fire if the kill call returns false.

```csharp
event Action<int> OnSquaddieKilled
// (squaddieId)
```

| Lua key | Type | Description |
|---|---|---|
| `squaddie_id` | int | ID of the squaddie who was killed |

Lua event name: `"squaddie_killed"`

---

### Operation / Mission Events

> **Note:** Operation and mission events are currently **disabled** in the source. The underlying patches are commented out due to a crash caused by patching `BaseGameEffect`. The C# events and Lua callback infrastructure are in place for when a safe patch point is identified. Do not rely on these events firing at runtime.

#### `OnOperationStarted` *(disabled)*
Intended to fire when an operation begins.

```csharp
event Action<IntPtr> OnOperationStarted
// (operation)
```

Lua event name: `"operation_started"` *(not currently fired)*

---

#### `OnOperationFinished` *(disabled)*
Intended to fire when an operation concludes.

```csharp
event Action<IntPtr> OnOperationFinished
// (operation)
```

Lua event name: `"operation_finished"` *(not currently fired)*

---

#### `OnMissionStarted` *(disabled)*
Intended to fire when a mission within an operation begins.

```csharp
event Action<IntPtr, IntPtr> OnMissionStarted
// (operation, mission)
```

Lua event name: `"mission_started"` *(not currently fired)*

---

#### `OnMissionFinished` *(disabled)*
Intended to fire when a mission within an operation concludes, along with its result.

```csharp
event Action<IntPtr, IntPtr, IntPtr> OnMissionFinished
// (operation, mission, missionResult)
```

Lua event name: `"mission_finished"` *(not currently fired)*

---

### Black Market Events

#### `OnBlackMarketItemAdded`
Fires when a single item is added to the Black Market inventory.

```csharp
event Action<IntPtr> OnBlackMarketItemAdded
// (item)
```

| Lua key | Type | Description |
|---|---|---|
| `item` | string | Name of the item added |
| `item_ptr` | int64 | Pointer to the item |

Lua event name: `"blackmarket_item_added"`

---

#### `OnBlackMarketRestocked`
Fires when the Black Market inventory is fully restocked (i.e. `FillUp` completes). Takes no arguments.

```csharp
event Action OnBlackMarketRestocked
```

The Lua event fires with an empty data table.

Lua event name: `"blackmarket_restocked"`

---

### Emotional State Events

#### `OnTriggerEmotion`
Fires when an emotional state trigger is evaluated against a target, such as when a scripted story beat or mission condition checks character emotions.

```csharp
event Action<IntPtr, IntPtr, IntPtr, IntPtr> OnTriggerEmotion
// (trigger, target, random, mission)
```

| Lua key | Type | Description |
|---|---|---|
| `trigger` | string | Name of the emotion trigger |
| `trigger_ptr` | int64 | Pointer to the trigger |
| `target` | string | Name of the target being evaluated |
| `target_ptr` | int64 | Pointer to the target |
| `mission` | string | Name of the associated mission |
| `mission_ptr` | int64 | Pointer to the mission |

> **Note:** The `random` parameter is passed through to the C# event but is not exposed in the Lua data table.

Lua event name: `"emotion_triggered"`
