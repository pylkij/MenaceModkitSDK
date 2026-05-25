# DevModePlugin Guide & API Reference

`DevModePlugin` is a MelonLoader plugin that ships with the Menace modpack loader. It provides in-game cheat and developer tooling through the **Dev Console** panel, a set of **ModSettings** sliders and toggles, and hotkeys for tactical-layer actions. It is automatically registered on initialization — you do not need to call it manually.

---

## How It Works

DevModePlugin uses **reflection** against `Assembly-CSharp` at runtime — no hardcoded IL2CPP offsets. On startup it locates the types it needs (`TacticalState`, `SpawnEntityAction`, `GodModeAction`, `DeleteEntityAction`, `DevSettings`, etc.) by name and caches constructors, methods, and properties. Field reads and writes for gameplay tweaks go through the SDK's `FieldHandle<T, TField>` / `ObjFieldHandle<T, TField>` wrappers.

This design means it tolerates game updates that shift offsets, but it does depend on type and member names remaining stable. If a type cannot be found, that feature degrades gracefully (warnings are logged, UI falls back to a "not ready" state).

---

## Initialization & Setup Lifecycle

DevModePlugin is instantiated and managed by the modpack loader. From its perspective, the lifecycle is:

1. **`Initialize(harmony, logger)`** — called by the loader on startup. Registers the Dev Console panel and all ModSettings. Does not touch the game assembly yet.
2. **`OnSceneLoaded(buildIndex, sceneName)`** — fires on the first scene load. Starts `WaitAndSetupCore()`, a coroutine that retries setup every 2 seconds for up to 30 attempts (60 seconds total).
3. **`TrySetupCore()`** — each attempt: locates `Assembly-CSharp`, resolves all reflection caches, enables cheats (if `AutoEnableCheats` is on), caches action constructors, and loads entity templates. Returns `true` on success; `false` causes another retry.
4. **`_devModeReady = true`** — set at the end of a successful setup or after all retries are exhausted. The Dev Console panel and hotkeys are inactive until this flag is set.
5. **Deferred work** — after a successful setup, two `GameState.RunDelayed` calls are scheduled: gameplay tweaks apply at frame 30, and `RecruitAllLeaders` (if enabled) fires at frame 60.
6. **`OnUpdate()`** — polls hotkeys and handles early diagnostics at frame 60.

> **Note:** `OnSceneLoaded` only starts the coroutine on the *first* scene. Subsequent scene loads do not re-run setup. If you need to react to scene changes in your own code, use `GameState.SceneLoaded` directly.

---

## Settings Reference

Settings are registered under the group `"Dev Mode"` via `ModSettings`. They can be read at runtime with `ModSettings.Get<T>("Dev Mode", key)`.

### Cheats

| Key | Type | Default | Description |
|---|---|---|---|
| `AutoEnableCheats` | `bool` | `true` | Automatically writes `CheatsEnabled = 1` into the game's `DevSettings` array on first successful setup. Disable if you want to enable cheats manually via the game's own developer menu. |

### Spawn Tool

| Key | Type | Default | Description |
|---|---|---|---|
| `DefaultFaction` | `string` | `"Enemy"` | The faction pre-selected when the spawn tool opens. Accepts `"Enemy"`, `"Player"`, or `"Neutral"` (matched by substring against the `FactionType` enum). |
| `ShowAllEntityTypes` | `bool` | `false` | When `false`, only `EntityType.Actor` templates are listed. When `true`, all `EntityTemplate` instances are shown, including vehicles, objects, and other non-actor types. Changing this live reloads the entity list immediately. |

### Recruitment

| Key | Type | Default | Description |
|---|---|---|---|
| `RecruitAllLeaders` | `bool` | `false` | When enabled, adds every `UnitLeaderTemplate` found in the loaded data to the strategy roster's hirable list. Runs once on setup, and again whenever the setting is toggled on. Has no effect outside of strategy mode. |

### Gameplay Tweaks

All three sliders take effect immediately when changed. Original values are stored on first application and used as the base for subsequent multiplier changes — so setting damage to `2.0` then `1.5` correctly yields `1.5×` of the *original* value, not `1.5× of 2.0×`.

| Key | Type | Range | Default | Description |
|---|---|---|---|---|
| `WeaponDamageMult` | `float` | `0.5 – 3.0` | `1.0` | Multiplier applied to `WeaponTemplate.Damage` on all weapons. |
| `PlayerAccuracyBonus` | `float` | `-20 – 40` | `0` | Flat bonus added to `WeaponTemplate.AccuracyBonus` on player-side weapons. Weapons whose template name contains `"enemy"` or `"pirate"` are skipped. |
| `EnemyHealthMult` | `float` | `0.5 – 2.0` | `1.0` | Multiplier applied to `EntityProperties.HitpointsPerElement` on entity templates whose name contains `"enemy"` or `"pirate"`. |

---

## Dev Console Panel

The panel is registered under the name `"Dev Mode"` and is drawn by `DrawDevModePanel`. It appears inside the Dev Console overlay (toggled separately by the console system).

While `_devModeReady` is `false` the panel displays `"Dev Mode loading..."`. Once ready, it shows:

- **Cheats status** — `Cheats: ON` or `Cheats: OFF`, reflecting the live readback from `DevSettings`.
- **Faction selector** — `< Faction: [Name] >` scroll buttons. Cycles through all values from the `FactionType` enum (enemy-type factions listed first, then `Player`, `PlayerAI`, `Neutral`).
- **Entity selector** — `< EntityName [ActorType] (N/Total) >` scroll buttons. Lists all loaded templates filtered by the `ShowAllEntityTypes` setting.
- **Action buttons** — `Spawn`, `God Mode`, `Delete` (see sections below).
- **Status message** — displayed for 5 seconds after any action or error.

If no entities were loaded, the selector area is replaced with `"No entities loaded"`.

---

## Hotkeys

Hotkeys are only active while the Dev Console is visible (`DevConsole.IsVisible == true`).

| Key | Action |
|---|---|
| `]` | Cycle entity selection forward |
| `[` | Cycle entity selection backward |
| `\` | Cycle faction selection forward |
| `Enter` | Spawn selected entity (equivalent to clicking **Spawn**) |
| `F2` | Activate God Mode (click a unit after pressing) |
| `F3` | Activate Delete (click a unit after pressing) |

---

## Gameplay Tweaks

Applied via `ApplyGameplayTweaks()`, which is called once on setup (frame 30) and again whenever any of the three gameplay tweak settings change.

The method uses `FieldHandle` reads and writes via the SDK:

```csharp
// Internally resolved once per scene:
_hWeaponDamage    = GameObj<WeaponTemplate>.ResolveField(x => x.Damage);
_hWeaponAccuracy  = GameObj<WeaponTemplate>.ResolveField(x => x.AccuracyBonus);
_hEntityProps     = GameObj<EntityTemplate>.ResolveObjField(x => x.Properties);
_hEntityHp        = GameObj<EntityProperties>.ResolveField(x => x.HitpointsPerElement);
```

Handle resolution happens in `ResolveHandles()`, which is hooked to `GameState.SceneLoaded`. Handles are resolved once and cached in static fields; subsequent calls are no-ops.

**Important:** Base values are only stored on the *first* call to `ApplyGameplayTweaks`. If a weapon or entity template is not alive (fails `CheckAlive()`) at that moment, its original value is never captured and it will not be modified on future calls.

---

## Spawn Tool

Spawns an entity template into the tactical scene at a player-clicked tile.

**How to use:**
1. Open the Dev Console.
2. Use `[` / `]` (or the panel buttons) to select the entity you want to spawn.
3. Use `\` (or the panel buttons) to select the target faction.
4. Press `Enter` or click **Spawn**.
5. The status message changes to `"Click tile to place"` — click a tile in the tactical view.

**Internals:** Calls `TacticalState.StartDevModeAction(new SpawnEntityAction(template, factionEnumValue))`. Returns `"Not in tactical"` if `TacticalState.Get()` returns null (i.e., you are not in a tactical mission). Returns `"Spawn system not ready"` if any required reflection target failed to resolve.

Entity templates are loaded from `DataTemplateLoader.GetAll<EntityTemplate>()` during setup. The list is sorted alphabetically by name. Entries with null or empty names are excluded. The `ShowAllEntityTypes` setting controls whether non-actor types are included.

---

## God Mode

Makes a unit invulnerable. Activates a `GodModeAction` through `TacticalState.StartDevModeAction`, then waits for the player to click a unit.

**How to use:**
1. Open the Dev Console.
2. Press `F2` or click **God Mode**.
3. Status shows `"God Mode - click unit"`.
4. Click any unit in the tactical view to apply.

**Note:** `GodModeAction` supports either a zero-argument or one-argument constructor (with a `GodModeTarget` enum). DevModePlugin resolves whichever variant is present in the current build.

---

## Delete Entity

Removes an entity from the tactical scene. Activates a `DeleteEntityAction` through `TacticalState.StartDevModeAction`, then waits for the player to click a unit.

**How to use:**
1. Open the Dev Console.
2. Press `F3` or click **Delete**.
3. Status shows `"Delete - click unit"`.
4. Click any unit or entity in the tactical view to remove it.

---

## Recruit All Leaders

Adds every `UnitLeaderTemplate` found via `Templates.FindAll<UnitLeaderTemplate>()` to `StrategyState.Roster.m_HirableLeaders`. Leaders already in the hirable list are skipped. Leaders already hired (`m_HiredLeaders`) are not filtered — the Contains check only applies to the hirable list.

**Requirements:**
- Must be in strategy mode (`StrategyState.Get()` must return a non-null value).
- `StrategyState` must expose a `Roster` property with a `m_HirableLeaders` list.

**Triggering:**
- Automatically on setup if `RecruitAllLeaders` is `true` (frame 60 delay).
- Again immediately whenever the `RecruitAllLeaders` setting is toggled on.

The method logs the count of added and skipped leaders, and performs a readback of the final list size.

---

## Diagnostics & Logging

At **frame 60** of the first `OnUpdate` call, an early diagnostic block is logged regardless of ready state:

- Current `sceneSeen` and `devModeReady` flags
- Active scene name
- All currently loaded scenes (name, path, isLoaded)
- Total loaded assemblies and whether `Assembly-CSharp` was found

This runs once and is useful for catching cases where `OnSceneWasLoaded` never fires (e.g., the scene was already loaded before MelonLoader attached).

During `TrySetupCore`, failures at each reflection step produce `SdkLogger.Warning` messages that name the missing type or member. If all 30 setup attempts fail, a final warning is logged and `_devModeReady` is set to `true` anyway (panel will show the loading fallback indefinitely, but won't block other systems).

---

## Integration Notes

**Reading a setting from another plugin:**

```csharp
float damageMult = ModSettings.Get<float>("Dev Mode", "WeaponDamageMult");
bool cheatsOn    = ModSettings.Get<bool>("Dev Mode", "AutoEnableCheats");
```

**Reacting to setting changes:**

```csharp
ModSettings.OnSettingChanged += (modName, key, value) => {
    if (modName != "Dev Mode") return;
    if (key == "WeaponDamageMult") {
        // value is a boxed float
    }
};
```
