# StrategyEventHooks API Reference

`StrategyEventHooks` is a static class in the `Menace.SDK` namespace. It wraps Harmony postfix patches on strategy-layer classes (`Roster`, `StoryFaction`, `Squaddies`, `BaseGameEffect`, `BlackMarket`, `EmotionalStates`) and exposes them as standard C# events and Lua callbacks. You subscribe to the events you care about — the hooks fire automatically once initialized.

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

Every event also fires a Lua callback via `LuaScriptEngine`. Use the `on()` function with the event name string:

```lua
on("leader_hired", function(data)
    log(data.leader .. " joined the roster")
end)
```

The `data` table contains named fields — see each event below for the available keys. Most objects provide both a name string and a `_ptr` field (an `int64`) for cases where you need to pass the pointer back to SDK methods.

---

## Event Reference

### Roster Events

#### `OnLeaderHired`
Fires after a leader is successfully hired. Does not fire if the hire attempt fails.

```csharp
event Action<IntPtr> OnLeaderHired
// (leader)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Display name of the hired leader |
| `leader_ptr` | int64 | Pointer to the leader object |
| `template` | string | Name of the leader's template |

Lua event name: `"leader_hired"`

---

#### `OnLeaderDismissed`
Fires after a leader is successfully dismissed. Does not fire if `TryDismissLeader` returns false.

```csharp
event Action<IntPtr> OnLeaderDismissed
// (leader)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Display name of the dismissed leader |
| `leader_ptr` | int64 | Pointer to the leader object |

Lua event name: `"leader_dismissed"`

---

#### `OnLeaderPermadeath`
Fires when a leader dies permanently. Unlike `OnLeaderDismissed`, this always fires — there is no success/failure check on the underlying game call.

```csharp
event Action<IntPtr> OnLeaderPermadeath
// (leader)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Display name of the leader |
| `leader_ptr` | int64 | Pointer to the leader object |

Lua event name: `"leader_permadeath"`

---

#### `OnLeaderLevelUp`
Fires when a perk is added to a leader.

```csharp
event Action<IntPtr, IntPtr> OnLeaderLevelUp
// (leader, perk)
```

| Lua key | Type | Description |
|---|---|---|
| `leader` | string | Display name of the leader |
| `leader_ptr` | int64 | Pointer to the leader object |
| `perk` | string | Name of the perk that was added |

Lua event name: `"leader_levelup"`

---

### Faction Events

#### `OnFactionTrustChanged`
Fires after a faction's trust value changes. Does not fire when `delta` is `0`.

```csharp
event Action<IntPtr, int> OnFactionTrustChanged
// (faction, delta)
```

| Lua key | Type | Description |
|---|---|---|
| `faction` | string | Display name of the faction |
| `faction_ptr` | int64 | Pointer to the faction object |
| `delta` | int | Amount trust changed by (positive = gained, negative = lost) |

Lua event name: `"faction_trust_changed"`

---

#### `OnFactionStatusChanged`
Fires after a faction's status is set.

```csharp
event Action<IntPtr, int> OnFactionStatusChanged
// (faction, newStatus)
```

| Lua key | Type | Description |
|---|---|---|
| `faction` | string | Display name of the faction |
| `faction_ptr` | int64 | Pointer to the faction object |
| `status` | int | The new status value |

Lua event name: `"faction_status_changed"`

---

#### `OnFactionUpgradeUnlocked`
Fires when a faction unlocks an upgrade.

```csharp
event Action<IntPtr, IntPtr> OnFactionUpgradeUnlocked
// (faction, upgrade)
```

| Lua key | Type | Description |
|---|---|---|
| `faction` | string | Display name of the faction |
| `faction_ptr` | int64 | Pointer to the faction object |
| `upgrade` | string | Name of the unlocked upgrade |
| `upgrade_ptr` | int64 | Pointer to the upgrade object |

Lua event name: `"faction_upgrade_unlocked"`

---

### Squaddie Events

#### `OnSquaddieKilled`
Fires after a squaddie is successfully killed. Does not fire if the kill attempt returns false.

```csharp
event Action<int> OnSquaddieKilled
// (squaddieId)
```

| Lua key | Type | Description |
|---|---|---|
| `squaddie_id` | int | ID of the killed squaddie |

Lua event name: `"squaddie_killed"`

---

#### `OnSquaddieAdded`
Fires after a squaddie is added to the alive pool. The count is the total alive count after the addition, not a delta.

```csharp
event Action<int> OnSquaddieAdded
// (count)
```

| Lua key | Type | Description |
|---|---|---|
| `squaddie` | string | Name of the squaddie that was added |
| `alive_count` | int | Total alive squaddie count after the addition |

Lua event name: `"squaddie_added"`

---

### Operation / Mission Events

#### `OnOperationStarted`
Fires when an operation begins.

```csharp
event Action<IntPtr> OnOperationStarted
// (operation)
```

| Lua key | Type | Description |
|---|---|---|
| `operation` | string | Name of the operation |
| `operation_ptr` | int64 | Pointer to the operation object |

Lua event name: `"operation_started"`

---

#### `OnOperationFinished`
Fires when an operation ends.

```csharp
event Action<IntPtr> OnOperationFinished
// (operation)
```

| Lua key | Type | Description |
|---|---|---|
| `operation` | string | Name of the operation |
| `operation_ptr` | int64 | Pointer to the operation object |

Lua event name: `"operation_finished"`

---

#### `OnMissionStarted`
Fires when a mission within an operation begins.

```csharp
event Action<IntPtr, IntPtr> OnMissionStarted
// (operation, mission)
```

| Lua key | Type | Description |
|---|---|---|
| `operation` | string | Name of the parent operation |
| `operation_ptr` | int64 | Pointer to the operation object |
| `mission` | string | Name of the mission |
| `mission_ptr` | int64 | Pointer to the mission object |

Lua event name: `"mission_started"`

---

#### `OnMissionFinished`
Fires when a mission ends, including a reference to the result object.

```csharp
event Action<IntPtr, IntPtr, IntPtr> OnMissionFinished
// (operation, mission, missionResult)
```

| Lua key | Type | Description |
|---|---|---|
| `operation` | string | Name of the parent operation |
| `operation_ptr` | int64 | Pointer to the operation object |
| `mission` | string | Name of the mission |
| `mission_ptr` | int64 | Pointer to the mission object |
| `mission_result` | string | Name/ID of the result object |
| `mission_result_ptr` | int64 | Pointer to the result object |

Lua event name: `"mission_finished"`

---

### Black Market Events

#### `OnBlackMarketItemAdded`
Fires when a single item is added to the black market.

```csharp
event Action<IntPtr> OnBlackMarketItemAdded
// (item)
```

| Lua key | Type | Description |
|---|---|---|
| `item` | string | Name of the item |
| `item_ptr` | int64 | Pointer to the item object |

Lua event name: `"blackmarket_item_added"`

---

#### `OnBlackMarketRestocked`
Fires when the black market is fully restocked. Takes no arguments — use this as a signal to re-read the whole market state.

```csharp
event Action OnBlackMarketRestocked
```

The `data` table for this event is empty.

Lua event name: `"blackmarket_restocked"`

---

### Emotional State Events

#### `OnTriggerEmotion`
Fires when an emotional state is triggered on a target.

```csharp
event Action<IntPtr, IntPtr, IntPtr, IntPtr> OnTriggerEmotion
// (trigger, target, random, mission)
```

| Lua key | Type | Description |
|---|---|---|
| `trigger` | string | Name of the emotion trigger |
| `trigger_ptr` | int64 | Pointer to the trigger object |
| `target` | string | Name of the target entity |
| `target_ptr` | int64 | Pointer to the target object |
| `mission` | string | Name of the mission context, or `<null>` if none |
| `mission_ptr` | int64 | Pointer to the mission object (0 if none) |

> **Note:** The `random` parameter is not included in the Lua data table. It is available via the C# event only.

Lua event name: `"emotion_triggered"`

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
| `OnSquaddieAdded` | `squaddie_added` | Squaddie |
| `OnOperationStarted` | `operation_started` | Operation |
| `OnOperationFinished` | `operation_finished` | Operation |
| `OnMissionStarted` | `mission_started` | Operation |
| `OnMissionFinished` | `mission_finished` | Operation |
| `OnBlackMarketItemAdded` | `blackmarket_item_added` | Black Market |
| `OnBlackMarketRestocked` | `blackmarket_restocked` | Black Market |
| `OnTriggerEmotion` | `emotion_triggered` | Emotional State |
