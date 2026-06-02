# Roster API Reference

`Roster` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's roster and unit management systems, exposing safe access to hired leaders, hirable templates, squaddies, perks, and leader availability. Field handles are resolved automatically on the first `GameState.SceneLoaded` event — no setup is required in your plugin.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetRoster()` | `GameObj<Roster>` | Core |
| `GetHiredLeaders()` | `List<UnitLeaderInfo>` | Queries |
| `GetLeaderInfo(leader)` | `UnitLeaderInfo` | Queries |
| `GetHiredCount()` | `int` | Queries |
| `GetAvailableCount()` | `int` | Queries |
| `GetPerks(leader)` | `List<string>` | Queries |
| `GetStatusName(status)` | `string` | Queries |
| `FindByNicknameTyped(nickname)` | `GameObj<BaseUnitLeader>` | Queries |
| `FindByTemplateId(templateId)` | `GameObj<BaseUnitLeader>` | Queries |
| `GetLeaderTemplate(leader)` | `GameObj<UnitLeaderTemplate>` | Queries |
| `GetHirableLeaders()` | `List<UnitLeaderTemplateInfo>` | Queries |
| `GetTemplateInfo(template)` | `UnitLeaderTemplateInfo` | Queries |
| `FindHirableByTemplateId(templateId)` | `GameObj<UnitLeaderTemplate>` | Queries |
| `HireLeader(template)` | `GameObj<BaseUnitLeader>` | Write |
| `DismissLeader(leader)` | `bool` | Write |
| `AddHirableLeader(template)` | `bool` | Write |
| `HealLeader(leader)` | `bool` | Write |
| `SetLeaderAvailable(leader, available)` | `bool` | Write |
| `AddPerk(leader, perk)` | `bool` | Write |
| `RemovePerk(leader, perkName)` | `bool` | Write |
| `FindPerk(perkName)` | `PerkTemplate` | Queries |
| `GetSquaddies(leader)` | `List<SquaddieInfo>` | Squaddies |
| `GetSquaddieInfo(squaddie)` | `SquaddieInfo` | Squaddies |
| `GetSquaddieCount(leader)` | `int` | Squaddies |
| `AddSquaddie(leader, squaddie)` | `bool` | Squaddies |
| `RemoveSquaddie(leader, squaddie)` | `bool` | Squaddies |
| `RegisterConsoleCommands()` | `void` | Dev Tools |

---

## Status Constants

`Roster` exposes integer constants for leader status values, used in `UnitLeaderInfo.Status`.

| Constant | Value | Meaning |
|---|---|---|
| `STATUS_HIRED` | `0` | Leader is currently hired. |
| `STATUS_AVAILABLE` | `1` | Leader is available for hire. |
| `STATUS_DEAD` | `2` | Leader has died. |
| `STATUS_DISMISSED` | `3` | Leader has been dismissed. |
| `STATUS_AWAITING_BURIAL` | `4` | Leader is dead and awaiting burial. |

Use `GetStatusName(status)` to convert a status integer to its string label.

---

## Quick Start

```csharp
using MelonLoader;
using Menace.SDK;

namespace MyPlugin;

public class MyPlugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        // Roster resolves its field handles automatically on scene load.
        // No setup required beyond this.
        Roster.RegisterConsoleCommands();
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately.
        var leaders = Roster.GetHiredLeaders();
        foreach (var leader in leaders)
        {
            var perks = Roster.GetPerks(Roster.FindByNicknameTyped(leader.Nickname));
            SdkLogger.Msg($"{leader.Nickname} ({leader.RankName}) - {leader.PerkCount} perks, HP: {leader.HealthPercent:P0}, Deployable: {leader.IsDeployable}");
        }
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `UnitLeaderInfo`

A snapshot of a hired unit leader's state, returned by `GetHiredLeaders()` and `GetLeaderInfo()`.

```csharp
public class UnitLeaderInfo
{
    public string TemplateId { get; set; }
    public string Nickname { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; }
    public int Rank { get; set; }
    public string RankName { get; set; }
    public int PerkCount { get; set; }
    public float HealthPercent { get; set; }
    public bool IsDeployable { get; set; }
    public bool IsUnavailable { get; set; }
    public int SquaddieCount { get; set; }
    public int DeployCost { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateId` | `string` | Stable `m_ID` from the leader's `UnitLeaderTemplate`. Use this for all template lookups. |
| `Nickname` | `string` | The leader's in-game nickname. |
| `Status` | `int` | Leader status code. Compare against the `STATUS_*` constants. |
| `StatusName` | `string` | Human-readable status label (e.g. `"Hired"`, `"Dead"`). Populated by `GetHiredLeaders()`; `null` when returned directly from `GetLeaderInfo()`. |
| `Rank` | `int` | Numeric rank value. |
| `RankName` | `string` | Display name of the leader's current rank. `null` if the rank template could not be resolved. |
| `PerkCount` | `int` | Number of perks currently assigned to this leader. |
| `HealthPercent` | `float` | Current health as a fraction of maximum (0.0–1.0). |
| `IsDeployable` | `bool` | Whether the leader can currently be deployed. |
| `IsUnavailable` | `bool` | Whether the leader is currently marked unavailable. |
| `SquaddieCount` | `int` | Number of squaddies assigned to this leader. Not populated by `GetLeaderInfo()`; use `GetSquaddieCount()` to retrieve it. |
| `DeployCost` | `int` | Deploy cost for this leader. Not currently populated by `GetLeaderInfo()`. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying leader object. |

---

### `UnitLeaderTemplateInfo`

Describes a unit leader template, returned by `GetHirableLeaders()` and `GetTemplateInfo()`.

```csharp
public class UnitLeaderTemplateInfo
{
    public string TemplateId { get; set; }
    public string DisplayName { get; set; }
    public int HiringCost { get; set; }
    public int Rarity { get; set; }
    public int MinCampaignProgress { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `TemplateId` | `string` | Stable `m_ID` from the template. Use this for all template lookups — never display names. |
| `DisplayName` | `string` | Localized display name of the leader. Falls back to `TemplateId` if the localized title cannot be resolved. |
| `HiringCost` | `int` | Cost to hire this leader. |
| `Rarity` | `int` | Rarity value of this leader template. |
| `MinCampaignProgress` | `int` | Minimum campaign progress required for this leader to appear in the hire pool. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying template object. |

---

### `SquaddieInfo`

Describes a single squaddie assigned to a leader, returned by `GetSquaddies()` and `GetSquaddieInfo()`.

```csharp
public class SquaddieInfo
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public string Gender { get; set; }
    public string HomePlanet { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `FirstName` | `string` | The squaddie's first name. |
| `LastName` | `string` | The squaddie's last name. |
| `FullName` | `string` | Convenience concatenation of `FirstName` and `LastName`. |
| `Gender` | `string` | The squaddie's gender. |
| `HomePlanet` | `string` | The squaddie's home planet. `null` or empty if not available. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying squaddie object. |

---

## Method Reference

### Core

#### `GetRoster()`

Returns the current `Roster` instance from `StrategyState`. This is the root handle from which all roster data is accessed. Most callers should prefer the higher-level query methods; use `GetRoster()` directly when you need to perform operations the SDK does not yet expose.

```csharp
GameObj<Il2CppMenace.Strategy.Roster> GetRoster()
```

Returns a default (null) `GameObj` if `StrategyState` is unavailable or the read fails.

---

### Queries

#### `GetHiredLeaders()`

Returns a snapshot of all currently hired unit leaders.

```csharp
List<UnitLeaderInfo> GetHiredLeaders()
```

Each entry has its `Status` set to `STATUS_HIRED` and `StatusName` set to `"Hired"`. Returns an empty list if the roster is unavailable or no leaders are hired.

---

#### `GetLeaderInfo(leader)`

Reads the full state of a single unit leader into a `UnitLeaderInfo` snapshot.

```csharp
UnitLeaderInfo GetLeaderInfo(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to query. |

Returns `null` if the leader handle is not alive or the read fails. Note that `StatusName` and `SquaddieCount` are not populated by this method — use `GetStatusName()` and `GetSquaddieCount()` respectively if you need them.

---

#### `GetHiredCount()`

Returns the total number of currently hired leaders.

```csharp
int GetHiredCount()
```

---

#### `GetAvailableCount()`

Returns the number of hired leaders that are currently deployable.

```csharp
int GetAvailableCount()
```

---

#### `GetPerks(leader)`

Returns the names of all perks assigned to a leader.

```csharp
List<string> GetPerks(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to query. |

Returns an empty list if the leader handle is not alive, the perk list cannot be read, or the leader has no perks.

---

#### `GetStatusName(status)`

Converts a status integer to its human-readable label.

```csharp
string GetStatusName(int status)
```

| Parameter | Type | Description |
|---|---|---|
| `status` | `int` | A status code, typically from `UnitLeaderInfo.Status`. |

Returns a string like `"Hired"` or `"Awaiting Burial"`. Returns `"Status {n}"` for unrecognised values.

---

#### `FindByNicknameTyped(nickname)`

Finds a hired leader by nickname using a case-insensitive substring match.

```csharp
GameObj<BaseUnitLeader> FindByNicknameTyped(string nickname)
```

| Parameter | Type | Description |
|---|---|---|
| `nickname` | `string` | A full or partial nickname to search for. |

Returns the first hired leader whose nickname contains the search string. Returns a default (null) `GameObj` if no match is found, and logs a warning listing available nicknames.

---

#### `FindByTemplateId(templateId)`

Finds a hired leader by their template ID (exact match).

```csharp
GameObj<BaseUnitLeader> FindByTemplateId(string templateId)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | The exact `m_ID` of the template to search for. |

Returns a default (null) `GameObj` if no match is found.

---

#### `GetLeaderTemplate(leader)`

Returns the `UnitLeaderTemplate` object for a given leader.

```csharp
GameObj<UnitLeaderTemplate> GetLeaderTemplate(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader whose template to retrieve. |

Returns a default (null) `GameObj` if the leader handle is not alive or the template cannot be read.

---

#### `GetHirableLeaders()`

Returns all leader templates currently in the hire pool.

```csharp
List<UnitLeaderTemplateInfo> GetHirableLeaders()
```

Returns an empty list if the roster is unavailable or the hire pool is empty.

---

#### `GetTemplateInfo(template)`

Reads the full state of a single leader template into a `UnitLeaderTemplateInfo` snapshot.

```csharp
UnitLeaderTemplateInfo GetTemplateInfo(GameObj<UnitLeaderTemplate> template)
```

| Parameter | Type | Description |
|---|---|---|
| `template` | `GameObj<UnitLeaderTemplate>` | The template to query. |

Returns `null` if the template handle is not alive or the read fails.

---

#### `FindHirableByTemplateId(templateId)`

Finds a leader template in the hire pool by template ID (exact match).

```csharp
GameObj<UnitLeaderTemplate> FindHirableByTemplateId(string templateId)
```

| Parameter | Type | Description |
|---|---|---|
| `templateId` | `string` | The exact `m_ID` of the template to search for. |

Returns a default (null) `GameObj` if no match is found.

---

#### `FindPerk(perkName)`

Finds a perk template by name using `GameQuery`.

```csharp
PerkTemplate FindPerk(string perkName)
```

| Parameter | Type | Description |
|---|---|---|
| `perkName` | `string` | The name of the perk to find. |

Returns `null` if `perkName` is null or empty, or if no matching perk is found.

---

### Write Operations

#### `HireLeader(template)`

Hires a leader from a template, invoking the game's `HireLeader` method on the `Roster`.

```csharp
GameObj<BaseUnitLeader> HireLeader(GameObj<UnitLeaderTemplate> template)
```

| Parameter | Type | Description |
|---|---|---|
| `template` | `GameObj<UnitLeaderTemplate>` | The template to hire from. Typically obtained from `FindHirableByTemplateId()`. |

Returns the newly hired leader. Returns a default (null) `GameObj` if the template handle is not alive or the hire call fails.

---

#### `DismissLeader(leader)`

Dismisses a hired leader, invoking the game's `TryDismissLeader` method on the `Roster`.

```csharp
bool DismissLeader(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to dismiss. |

Returns `true` if the game's dismiss method returned `true`. Returns `false` if the leader handle is not alive or the call fails.

---

#### `AddHirableLeader(template)`

Adds a leader template to the hire pool, invoking the game's `AddHirableLeader` method on the `Roster`.

```csharp
bool AddHirableLeader(GameObj<UnitLeaderTemplate> template)
```

| Parameter | Type | Description |
|---|---|---|
| `template` | `GameObj<UnitLeaderTemplate>` | The template to add to the hire pool. |

Returns `false` if the template handle is not alive or the call fails.

---

#### `HealLeader(leader)`

Fully restores a leader's health by setting their health status to 0.

```csharp
bool HealLeader(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to heal. |

Returns `false` if the leader handle is not alive or the call fails.

---

#### `SetLeaderAvailable(leader, available)`

Sets a leader's availability by writing directly to the `m_UnavailableDuration` fields.

```csharp
bool SetLeaderAvailable(GameObj<BaseUnitLeader> leader, bool available)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to modify. |
| `available` | `bool` | `true` to clear unavailability (zeroes both duration fields); `false` to mark unavailable. |

Returns `false` if the leader handle is not alive or the write fails.

> **Note:** Passing `false` writes a hardcoded minimum value (`1`) to `m_UnavailableDuration.Operations` and `0` to `m_UnavailableDuration.Missions`. This marks the leader as unavailable but does not set a meaningful game duration — it is primarily useful for testing.

---

#### `AddPerk(leader, perk)`

Adds a perk to a leader.

```csharp
bool AddPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perk)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to modify. |
| `perk` | `GameObj<PerkTemplate>` | The perk template to add. Obtain via `FindPerk()`. |

Returns `false` if either handle is not alive or the call fails.

---

#### `RemovePerk(leader, perkName)`

Removes the first perk whose name contains `perkName` (case-insensitive) from a leader.

```csharp
bool RemovePerk(GameObj<BaseUnitLeader> leader, string perkName)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to modify. |
| `perkName` | `string` | A full or partial perk name to match against. |

Returns `false` if the leader handle is not alive, `perkName` is null or empty, no matching perk is found, or the call fails.

---

### Squaddie Management

#### `GetSquaddies(leader)`

Returns all squaddies assigned to a leader, resolved via the `Squaddies` manager on `StrategyState`.

```csharp
List<SquaddieInfo> GetSquaddies(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader whose squaddies to retrieve. |

Returns an empty list if the leader handle is not alive, the squaddies manager is unavailable, or the leader has no squaddies. Returns `null` if the leader handle check fails entirely.

---

#### `GetSquaddieInfo(squaddie)`

Reads the full state of a single squaddie into a `SquaddieInfo` snapshot.

```csharp
SquaddieInfo GetSquaddieInfo(GameObj<Squaddie> squaddie)
```

| Parameter | Type | Description |
|---|---|---|
| `squaddie` | `GameObj<Squaddie>` | The squaddie to query. |

Returns `null` if the squaddie handle is not alive or the read fails.

---

#### `GetSquaddieCount(leader)`

Returns the number of squaddies assigned to a leader.

```csharp
int GetSquaddieCount(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to query. |

Returns `0` if the leader handle is not alive or the call fails.

---

#### `AddSquaddie(leader, squaddie)`

Assigns a squaddie to a leader by invoking `TryAddSquaddie` on the leader proxy.

```csharp
bool AddSquaddie(GameObj<BaseUnitLeader> leader, GameObj<Squaddie> squaddie)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to assign the squaddie to. |
| `squaddie` | `GameObj<Squaddie>` | The squaddie to assign. |

Returns `false` if either handle is not alive or the call fails.

---

#### `RemoveSquaddie(leader, squaddie)`

Removes a squaddie from a leader by invoking `TryRemoveSquaddie` on the leader proxy.

```csharp
bool RemoveSquaddie(GameObj<BaseUnitLeader> leader, GameObj<Squaddie> squaddie)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The leader to remove the squaddie from. |
| `squaddie` | `GameObj<Squaddie>` | The squaddie to remove. |

Returns `false` if either handle is not alive or the call fails.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `roster` | *(none)* | List all hired leaders with rank, perk count, status, and squaddie count. |
| `unit` | `<nickname>` | Print full info for a leader including rank, health, perks, and deploy status. |
| `available` | *(none)* | Print the number of leaders ready for deployment out of total hired. |
| `hirable` | *(none)* | List all leader templates currently in the hire pool. |
| `hire` | `<template>` | Hire a leader by template ID. |
| `dismiss` | `<nickname>` | Dismiss a hired leader by nickname. |
| `squaddies` | `<nickname>` | List all squaddies assigned to a leader. |
| `healleader` | `<nickname>` | Heal a leader to full health. |
| `addperk` | `<nickname> <perk>` | Add a perk to a leader by name. |
| `removeperk` | `<nickname> <perk>` | Remove a perk from a leader by name. |
| `setavailable` | `<nickname> <true/false>` | Set a leader's availability status. |

Example session:

```
> roster
Hired Units (3):
  Vance - Sergeant (2 perks) [Ready]
  Okoro - Corporal (1 perks) [Unavailable]
  Mira - Private (0 perks) [Ready] (+4 squaddies)

> unit Vance
Unit: Vance
Template: leader_squad_vance
Rank: Sergeant (Rank 2)
Health: 100%
Deploy Cost: 0
Deployable: True, Unavailable: False
Squaddies: 0
Perks (2): Iron Will, Quick Reload

> squaddies Mira
Mira's Squaddies (4):
  Eli Rook (from Vaspar Prime)
  Dara Senn (from Caldwell)
  Holt Vex (from Caldwell)
  Nessa Wain (from Thandor)

> hirable
Available for Hire (2):
  Gunner Hatch (Rarity: 15%)
  Sable Krix
```

---

## Error Handling

All methods are wrapped in try/catch. Failures inside SDK infrastructure are reported via `SdkLogger`.

| Method | Fallback on error |
|---|---|
| `GetRoster` | Default (null) `GameObj` |
| `GetHiredLeaders` | Empty list |
| `GetLeaderInfo` | `null` |
| `GetHiredCount` | `0` (via `GetHiredLeaders`) |
| `GetAvailableCount` | `0` (via `GetHiredLeaders`) |
| `GetPerks` | Empty list |
| `FindByNicknameTyped` | Default (null) `GameObj` |
| `FindByTemplateId` | Default (null) `GameObj` |
| `GetLeaderTemplate` | Default (null) `GameObj` |
| `GetHirableLeaders` | Empty list |
| `GetTemplateInfo` | `null` |
| `FindHirableByTemplateId` | Default (null) `GameObj` |
| `HireLeader` | Default (null) `GameObj` |
| `DismissLeader` | `false` |
| `AddHirableLeader` | `false` |
| `HealLeader` | `false` |
| `SetLeaderAvailable` | `false` |
| `AddPerk` | `false` |
| `RemovePerk` | `false` |
| `GetSquaddies` | Empty list |
| `GetSquaddieInfo` | `null` |
| `GetSquaddieCount` | `0` |
| `AddSquaddie` | `false` |
| `RemoveSquaddie` | `false` |