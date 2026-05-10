# TacticalEventHooks API Reference

`TacticalEventHooks` is a static class in the `Menace.SDK` namespace. It wraps Harmony postfix patches on `TacticalManager` and exposes them as standard C# events and Lua callbacks. You subscribe to the events you care about — the hooks fire automatically once initialized.

---

## How to Subscribe (C#)

All events are standard C# `Action` delegates on a static class, so subscription is straightforward:

```csharp
TacticalEventHooks.OnActorKilled += (actorPtr, killerPtr, killerFaction) =>
{
    // your logic here
};
```

Parameters are `IntPtr` handles into the game's IL2CPP memory. Use the SDK's `GameObj` wrapper to interact with them.

---

## How to Subscribe (Lua)

Every event also fires a Lua callback via `LuaScriptEngine`. Use the `on()` function with the event name string:

```lua
on("actor_killed", function(data)
    log(data.actor .. " was killed by " .. data.killer)
end)
```

The `data` table contains named fields — see each event below for the available keys. Most objects provide both a name string and a `_ptr` field (an `int64`) for cases where you need to pass the pointer back to SDK methods.

> **Note:** `skill_used` is a special case — its Lua handler receives `actor` and `skill` objects directly rather than a data table. See the Skill Events section.

---

## Event Reference

### Combat Events

#### `OnActorKilled`
Fires when an actor dies.

```csharp
event Action<IntPtr, IntPtr, int> OnActorKilled
// (actor, killer, killerFaction)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the actor who died |
| `actor_ptr` | int64 | Pointer to the dead actor |
| `killer` | string | Name of the killer |
| `killer_ptr` | int64 | Pointer to the killer |
| `faction` | int | Faction ID of the killer |

Lua event name: `"actor_killed"`

---

#### `OnDamageReceived`
Fires when an entity takes damage.

```csharp
event Action<IntPtr, IntPtr, IntPtr, IntPtr> OnDamageReceived
// (target, attacker, skill, damageInfo)
```

| Lua key | Type | Description |
|---|---|---|
| `target` | string | Name of the entity taking damage |
| `target_ptr` | int64 | Pointer to the target |
| `attacker` | string | Name of the attacker |
| `attacker_ptr` | int64 | Pointer to the attacker |
| `skill` | string | Name of the skill used |
| `skill_ptr` | int64 | Pointer to the skill |
| `damage_info_ptr` | int64 | Pointer to the damage info object |

Lua event name: `"damage_received"`

---

#### `OnAttackMissed`
Fires when an attack fails to hit.

```csharp
event Action<IntPtr, IntPtr, IntPtr> OnAttackMissed
// (entity, attacker, skill)
```

| Lua key | Type | Description |
|---|---|---|
| `attacker` | string | Name of the attacker |
| `attacker_ptr` | int64 | Pointer to the attacker |
| `target` | string | Name of the intended target |
| `target_ptr` | int64 | Pointer to the target |
| `skill` | string | Name of the skill used |
| `skill_ptr` | int64 | Pointer to the skill |

Lua event name: `"attack_missed"`

---

#### `OnAttackTileStart`
Fires at the beginning of a tile-targeted attack.

```csharp
event Action<IntPtr, IntPtr, IntPtr, float> OnAttackTileStart
// (attacker, skill, tile, attackDurationInSeconds)
```

| Lua key | Type | Description |
|---|---|---|
| `attacker` | string | Name of the attacker |
| `attacker_ptr` | int64 | Pointer to the attacker |
| `skill` | string | Name of the skill |
| `skill_ptr` | int64 | Pointer to the skill |
| `tile_ptr` | int64 | Pointer to the target tile |
| `attack_duration` | float | Duration of the attack animation in seconds |

Lua event name: `"attack_start"`

---

#### `OnBleedingOut`
Fires when a leader is bleeding out.

```csharp
event Action<IntPtr, int> OnBleedingOut
// (leader, remainingRounds)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the bleeding actor |
| `actor_ptr` | int64 | Pointer to the actor |
| `remaining_rounds` | int | Rounds remaining before death |

Lua event name: `"bleeding_out"`

---

#### `OnStabilized`
Fires when a bleeding-out leader is stabilized.

```csharp
event Action<IntPtr, IntPtr> OnStabilized
// (leader, savior)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the stabilized actor |
| `actor_ptr` | int64 | Pointer to the actor |
| `savior` | string | Name of the actor who performed the stabilization |
| `savior_ptr` | int64 | Pointer to the savior |

Lua event name: `"stabilized"`

---

#### `OnSuppressed`
Fires when an actor becomes suppressed.

```csharp
event Action<IntPtr> OnSuppressed
// (actor)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the suppressed actor |
| `actor_ptr` | int64 | Pointer to the actor |

Lua event name: `"suppressed"`

---

#### `OnSuppressionApplied`
Fires when suppression is applied to an actor, including the amount.

```csharp
event Action<IntPtr, float, IntPtr> OnSuppressionApplied
// (actor, change, suppressor)
```

| Lua key | Type | Description |
|---|---|---|
| `target` | string | Name of the suppressed actor |
| `target_ptr` | int64 | Pointer to the target |
| `attacker` | string | Name of the suppressor |
| `attacker_ptr` | int64 | Pointer to the suppressor |
| `amount` | float | Amount of suppression applied |

Lua event name: `"suppression_applied"`

---

### Actor State Events

#### `OnActorStateChanged`
Fires when an actor transitions between states (e.g. idle, moving, dead).

```csharp
event Action<IntPtr, int, int> OnActorStateChanged
// (actor, oldState, newState)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the actor |
| `actor_ptr` | int64 | Pointer to the actor |
| `old_state` | int | Previous state value |
| `new_state` | int | New state value |

Lua event name: `"actor_state_changed"`

---

#### `OnMoraleStateChanged`
Fires when an actor's morale state changes.

```csharp
event Action<IntPtr, int> OnMoraleStateChanged
// (actor, moraleState)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the actor |
| `actor_ptr` | int64 | Pointer to the actor |
| `state` | int | New morale state value |

Lua event name: `"morale_changed"`

---

#### `OnHitpointsChanged`
Fires when an entity's HP changes.

```csharp
event Action<IntPtr, float, int> OnHitpointsChanged
// (entity, hitpointsPct, animationDurationInMs)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the entity |
| `actor_ptr` | int64 | Pointer to the entity |
| `hitpoints_pct` | float | Current HP as a percentage (0.0–1.0) |
| `animation_duration_ms` | int | Duration of the HP change animation in milliseconds |

Lua event name: `"hp_changed"`

---

#### `OnArmorChanged`
Fires when an entity's armor changes.

```csharp
event Action<IntPtr, float, int, int> OnArmorChanged
// (entity, armorDurability, armor, animationDurationInMs)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the entity |
| `actor_ptr` | int64 | Pointer to the entity |
| `armor_durability` | float | Current armor durability |
| `armor` | int | Current armor value |
| `animation_duration_ms` | int | Duration of the armor change animation in milliseconds |

Lua event name: `"armor_changed"`

---

#### `OnActionPointsChanged`
Fires when an actor's action points change.

```csharp
event Action<IntPtr, int, int> OnActionPointsChanged
// (actor, oldAp, newAp)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the actor |
| `actor_ptr` | int64 | Pointer to the actor |
| `old_ap` | int | Previous AP value |
| `new_ap` | int | New AP value |
| `delta` | int | Change in AP (`new_ap - old_ap`) |

Lua event name: `"ap_changed"`

---

### Visibility Events

#### `OnDiscovered`
Fires when a hidden entity is spotted by another.

```csharp
event Action<IntPtr, IntPtr> OnDiscovered
// (entity, discoverer)
```

| Lua key | Type | Description |
|---|---|---|
| `discovered` | string | Name of the entity that was spotted |
| `discovered_ptr` | int64 | Pointer to the discovered entity |
| `discoverer` | string | Name of the entity that spotted them |
| `discoverer_ptr` | int64 | Pointer to the discoverer |

Lua event name: `"discovered"`

---

#### `OnVisibleToPlayer`
Fires when an actor becomes visible to the player.

```csharp
event Action<IntPtr> OnVisibleToPlayer
// (actor)
```

| Lua key | Type | Description |
|---|---|---|
| `entity` | string | Name of the entity |
| `entity_ptr` | int64 | Pointer to the entity |

Lua event name: `"visible_to_player"`

---

#### `OnHiddenToPlayer`
Fires when an actor is no longer visible to the player.

```csharp
event Action<IntPtr> OnHiddenToPlayer
// (actor)
```

| Lua key | Type | Description |
|---|---|---|
| `entity` | string | Name of the entity |
| `entity_ptr` | int64 | Pointer to the entity |

Lua event name: `"hidden_from_player"`

---

### Movement Events

#### `OnMovementStarted`
Fires when an actor begins moving.

```csharp
event Action<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> OnMovementStarted
// (actor, fromTile, toTile, movementAction, container)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the moving actor |
| `actor_ptr` | int64 | Pointer to the actor |
| `from_ptr` | int64 | Pointer to the origin tile |
| `to_ptr` | int64 | Pointer to the destination tile |
| `action_ptr` | int64 | Pointer to the movement action |
| `container_ptr` | int64 | Pointer to the movement container |

Lua event name: `"movement_started"`

---

#### `OnMovementFinished`
Fires when an actor completes a move and arrives at a tile.

```csharp
event Action<IntPtr, IntPtr> OnMovementFinished
// (actor, tile)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the actor |
| `actor_ptr` | int64 | Pointer to the actor |
| `tile_ptr` | int64 | Pointer to the tile they arrived at |

Lua event name: `"move_complete"`

---

### Skill Events

#### `OnSkillUsed`
Fires when an actor activates a skill.

```csharp
event Action<IntPtr, IntPtr, IntPtr> OnSkillUsed
// (actor, skill, targetTile)
```

**Lua special case:** This event passes `actor` and `skill` as live objects rather than a data table, so you can call methods on them directly:

```lua
on("skill_used", function(actor, skill)
    if skill.is_attack and not skill.is_silent then
        actor:add_effect("concealment", -3, 1)
    end
end)
```

Lua event name: `"skill_used"`

---

#### `OnSkillCompleted`
Fires after a skill finishes executing.

```csharp
event Action<IntPtr> OnSkillCompleted
// (skill)
```

| Lua key | Type | Description |
|---|---|---|
| `skill` | string | Name of the completed skill |
| `skill_ptr` | int64 | Pointer to the skill |

Lua event name: `"skill_complete"`

---

#### `OnSkillAdded`
Fires when a skill is added to an actor (e.g. from an effect or ability).

```csharp
event Action<IntPtr, IntPtr, IntPtr, bool> OnSkillAdded
// (receiver, skill, source, success)
```

| Lua key | Type | Description |
|---|---|---|
| `actor` | string | Name of the actor receiving the skill |
| `actor_ptr` | int64 | Pointer to the actor |
| `skill` | string | Name of the skill added |
| `skill_ptr` | int64 | Pointer to the skill |
| `source` | string | Name of the source that granted the skill |
| `source_ptr` | int64 | Pointer to the source |
| `success` | bool | Whether the skill was successfully added |

Lua event name: `"skill_added"`

---

### Offmap Events

#### `OnOffmapAbilityUsed`
Fires when an offmap ability is activated.

```csharp
event Action<IntPtr, IntPtr> OnOffmapAbilityUsed
// (ability, targetTile)
```

| Lua key | Type | Description |
|---|---|---|
| `ability` | string | Name of the ability |
| `ability_ptr` | int64 | Pointer to the ability |
| `tile_ptr` | int64 | Pointer to the target tile |

Lua event name: `"offmap_ability_used"`

---

#### `OnOffmapAbilityCanceled`
Fires when an offmap ability is canceled before completing.

```csharp
event Action<IntPtr> OnOffmapAbilityCanceled
// (ability)
```

| Lua key | Type | Description |
|---|---|---|
| `ability` | string | Name of the ability |
| `ability_ptr` | int64 | Pointer to the ability |

Lua event name: `"offmap_ability_canceled"`

---

#### `OnOffmapAbilityUpdateUsability`
Fires when the usability state of offmap abilities should be recalculated. Takes no arguments.

```csharp
event Action OnOffmapAbilityUpdateUsability
```

No Lua event is fired for this one.

---

### Turn / Round Events

#### `OnActiveActorChanged`
Fires when the active actor changes (i.e. whose turn it is). Only fires if the new actor pointer is valid.

```csharp
event Action<IntPtr> OnActiveActorChanged
// (actor)
```

No Lua event is fired directly — use `OnTurnStart` or `OnPlayerTurn`/`OnAITurn` for Lua.

---

#### `OnTurnStart`
Fires at the start of an actor's turn, after `OnActiveActorChanged`.

```csharp
event Action<IntPtr> OnTurnStart
// (actor)
```

No Lua data table — subscribe via C# or rely on `OnPlayerTurn` / `OnAITurn` in Lua.

---

#### `OnPlayerTurn`
Fires at the start of the player's turn.

```csharp
event Action OnPlayerTurn
```

No Lua event (fires as part of the `SetActiveActor` hook alongside `OnTurnStart`).

---

#### `OnAITurn`
Fires at the start of the AI's turn.

```csharp
event Action OnAITurn
```

No Lua event (same hook as `OnPlayerTurn`).

---

#### `OnTurnEnd`
Fires at the end of an actor's turn. Also fires the legacy `LuaScriptEngine.OnTurnEnd(faction, factionName)` path for backward compatibility.

```csharp
event Action<IntPtr> OnTurnEnd
// (actor)
```

No Lua data table via the standard `FireLuaEvent` path — the legacy Lua hook is called directly instead.

---

#### `OnRoundStart`
Fires after the round number increments.

```csharp
event Action<int> OnRoundStart
// (roundNumber)
```

| Lua key | Type | Description |
|---|---|---|
| `round` | int | The new round number |

Lua event name: `"round_start"`

---

#### `OnRoundEnd`
Fires before the round number increments — use this if you need the current round value before it changes.

```csharp
event Action<int> OnRoundEnd
// (roundNumber)
```

| Lua key | Type | Description |
|---|---|---|
| `round` | int | The round number that is ending |

Lua event name: `"round_end"`

---

### Mission Events

#### `OnObjectiveStateChanged`
Fires when a mission objective changes state.

```csharp
event Action<IntPtr, int, int> OnObjectiveStateChanged
// (objective, oldState, newState)
```

| Lua key | Type | Description |
|---|---|---|
| `objective` | string | Name of the objective |
| `objective_ptr` | int64 | Pointer to the objective |
| `state` | int | New state value |

Lua event name: `"objective_changed"`

---

#### `OnEntitySpawned`
Fires when a new entity is spawned into the tactical scene.

```csharp
event Action<IntPtr> OnEntitySpawned
// (entity)
```

| Lua key | Type | Description |
|---|---|---|
| `entity` | string | Name of the spawned entity |
| `entity_ptr` | int64 | Pointer to the entity |

Lua event name: `"entity_spawned"`

---

#### `OnElementDeath`
Fires when a sub-element of an entity is destroyed (e.g. a vehicle component).

```csharp
event Action<IntPtr, IntPtr, IntPtr, IntPtr> OnElementDeath
// (entity, element, attacker, damageInfo)
```

| Lua key | Type | Description |
|---|---|---|
| `entity` | string | Name of the parent entity |
| `entity_ptr` | int64 | Pointer to the parent entity |
| `element` | string | Name of the destroyed element |
| `element_ptr` | int64 | Pointer to the element |
| `attacker` | string | Name of the attacker |
| `attacker_ptr` | int64 | Pointer to the attacker |
| `damage_info_ptr` | int64 | Pointer to the damage info object |

Lua event name: `"element_destroyed"`

---

#### `OnElementMalfunction`
Fires when a sub-element malfunctions.

```csharp
event Action<IntPtr, IntPtr> OnElementMalfunction
// (element, skill)
```

| Lua key | Type | Description |
|---|---|---|
| `element` | string | Name of the malfunctioning element |
| `element_ptr` | int64 | Pointer to the element |
| `skill` | string | Name of the skill that caused the malfunction |
| `skill_ptr` | int64 | Pointer to the skill |

Lua event name: `"element_malfunction"`

---

## Quick Reference

| C# Event | Lua Event Name | Category |
|---|---|---|
| `OnActorKilled` | `actor_killed` | Combat |
| `OnDamageReceived` | `damage_received` | Combat |
| `OnAttackMissed` | `attack_missed` | Combat |
| `OnAttackTileStart` | `attack_start` | Combat |
| `OnBleedingOut` | `bleeding_out` | Combat |
| `OnStabilized` | `stabilized` | Combat |
| `OnSuppressed` | `suppressed` | Combat |
| `OnSuppressionApplied` | `suppression_applied` | Combat |
| `OnActorStateChanged` | `actor_state_changed` | Actor State |
| `OnMoraleStateChanged` | `morale_changed` | Actor State |
| `OnHitpointsChanged` | `hp_changed` | Actor State |
| `OnArmorChanged` | `armor_changed` | Actor State |
| `OnActionPointsChanged` | `ap_changed` | Actor State |
| `OnDiscovered` | `discovered` | Visibility |
| `OnVisibleToPlayer` | `visible_to_player` | Visibility |
| `OnHiddenToPlayer` | `hidden_from_player` | Visibility |
| `OnMovementStarted` | `movement_started` | Movement |
| `OnMovementFinished` | `move_complete` | Movement |
| `OnSkillUsed` | `skill_used` *(object args)* | Skill |
| `OnSkillCompleted` | `skill_complete` | Skill |
| `OnSkillAdded` | `skill_added` | Skill |
| `OnOffmapAbilityUsed` | `offmap_ability_used` | Offmap |
| `OnOffmapAbilityCanceled` | `offmap_ability_canceled` | Offmap |
| `OnOffmapAbilityUpdateUsability` | *(none)* | Offmap |
| `OnActiveActorChanged` | *(none)* | Turn/Round |
| `OnTurnStart` | *(none)* | Turn/Round |
| `OnPlayerTurn` | *(none)* | Turn/Round |
| `OnAITurn` | *(none)* | Turn/Round |
| `OnTurnEnd` | *(legacy path)* | Turn/Round |
| `OnRoundStart` | `round_start` | Turn/Round |
| `OnRoundEnd` | `round_end` | Turn/Round |
| `OnObjectiveStateChanged` | `objective_changed` | Mission |
| `OnEntitySpawned` | `entity_spawned` | Mission |
| `OnElementDeath` | `element_destroyed` | Mission |
| `OnElementMalfunction` | `element_malfunction` | Mission |