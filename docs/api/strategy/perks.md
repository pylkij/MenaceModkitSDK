# Perks API Reference

`Perks` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's perk and skill systems and exposes safe access to perk trees, individual perk metadata, and perk manipulation for unit leaders. Field handles are resolved automatically on the first scene load — no setup is required beyond normal plugin initialization.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `GetLeaderPerks(leader)` | `List<PerkInfo>` | Queries |
| `GetPerkInfo(perkTemplate)` | `PerkInfo` | Queries |
| `GetPerkTrees(leader)` | `List<PerkTreeInfo>` | Queries |
| `GetPerkTreeInfo(perkTree)` | `PerkTreeInfo` | Queries |
| `GetAvailablePerks(leader)` | `List<PerkInfo>` | Queries |
| `FindPerkByName(leader, perkName)` | `GameObj<PerkTemplate>` | Queries |
| `GetLastPerk(leader)` | `GameObj<PerkTemplate>` | Queries |
| `CanBePromoted(leader)` | `bool` | Checks |
| `CanBeDemoted(leader)` | `bool` | Checks |
| `HasPerk(leader, perkTemplate)` | `bool` | Checks |
| `AddPerk(leader, perkTemplate, spendPromotionPoints)` | `bool` | Write |
| `RemoveLastPerk(leader)` | `bool` | Write |
| `RegisterConsoleCommands()` | `void` | Dev Tools |

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
        // Perks resolves its field handles automatically on scene load.
        // No setup required beyond this.
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Field handles are already resolved — safe to call immediately
        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull) return;

        var leader = actor.AsTyped<BaseUnitLeader>();
        if (leader.Untyped.IsNull) return;

        var perks = Perks.GetLeaderPerks(leader);
        foreach (var perk in perks)
            SdkLogger.Msg($"Perk: {perk.Title} (Tier {perk.Tier}, AP Cost: {perk.ActionPointCost}, Active: {perk.IsActive})");

        var trees = Perks.GetPerkTrees(leader);
        foreach (var tree in trees)
            SdkLogger.Msg($"Tree: {tree.Name} — {tree.PerkCount} perks");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Data Types

### `PerkInfo`

A snapshot of a single perk's state, returned by `GetLeaderPerks()`, `GetPerkInfo()`, `GetAvailablePerks()`, and as part of `PerkTreeInfo.Perks`.

```csharp
public class PerkInfo
{
    public string Name { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }
    public int ActionPointCost { get; set; }
    public bool IsActive { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Internal name from the underlying `PerkTemplate` object. Use this for stable lookups and comparisons. |
| `Title` | `string` | Localized display title, resolved from the `LocalizedLine` field via `GetRawDefaultTranslation()`. Falls back to `Name` if no translation is available. Use this for display purposes. |
| `Description` | `string` | Localized description text. `null` if no description is defined. |
| `Tier` | `int` | Tier level (1–4) within the perk tree. Only populated when returned via `PerkTreeInfo.Perks`; `0` when returned from `GetLeaderPerks()` or `GetPerkInfo()` directly. |
| `ActionPointCost` | `int` | Action point cost to activate this perk, if it is an active perk. |
| `IsActive` | `bool` | `true` if this is an active perk (player-triggered); `false` if it is a passive. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying `PerkTemplate` object. Use this to correlate entries across calls, e.g. when comparing learned perks against tree perks in `GetAvailablePerks()`. |

> **Note:** `Tier` is sourced from the `Perk` wrapper object in the tree, not from `PerkTemplate` itself. It is only populated when perk info is gathered through a tree traversal (`GetPerkTrees`, `GetPerkTreeInfo`, `GetAvailablePerks`). Perks retrieved via `GetLeaderPerks` or `GetPerkInfo` directly will have `Tier = 0`.

---

### `PerkTreeInfo`

Describes a full perk tree available to a unit leader, returned by `GetPerkTrees()` and `GetPerkTreeInfo()`.

```csharp
public class PerkTreeInfo
{
    public string Name { get; set; }
    public int PerkCount { get; set; }
    public List<PerkInfo> Perks { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Internal name of the `PerkTreeTemplate`. |
| `PerkCount` | `int` | Total number of perks in the tree, including those not yet learned. |
| `Perks` | `List<PerkInfo>` | All perks in this tree. Each entry includes its `Tier`. Empty if the tree has no perks or they cannot be read. |
| `Pointer` | `IntPtr` | Raw native pointer to the underlying `PerkTreeTemplate` object. |

---

## Method Reference

### Queries

#### `GetLeaderPerks(leader)`

Returns all perks currently learned by the specified unit leader, read directly from `BaseUnitLeader.m_Perks`.

```csharp
List<PerkInfo> GetLeaderPerks(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |

Returns an empty list if the leader handle is null, the perk list cannot be read, or the leader has no learned perks. `Tier` will be `0` for all returned entries; use `GetPerkTrees` if tier information is needed.

---

#### `GetPerkInfo(perkTemplate)`

Reads localized metadata and flags from a single `PerkTemplate` handle.

```csharp
PerkInfo GetPerkInfo(GameObj<PerkTemplate> perkTemplate)
```

| Parameter | Type | Description |
|---|---|---|
| `perkTemplate` | `GameObj<PerkTemplate>` | The perk template to read. |

Returns `null` if the handle is null or field reads fail. Called internally by `GetLeaderPerks`, `GetPerkTrees`, and `GetPerkTreeInfo`; exposed publicly for callers that already hold a typed perk template handle.

---

#### `GetPerkTrees(leader)`

Returns all perk trees available to a leader, sourced from the leader's `UnitLeaderTemplate`. Each tree includes all its perks with tier information populated.

```csharp
List<PerkTreeInfo> GetPerkTrees(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader whose template to query. |

Returns an empty list if the leader handle is null, the leader template cannot be read, or no perk trees are defined.

---

#### `GetPerkTreeInfo(perkTree)`

Reads a single perk tree, populating all of its `Perk` entries with metadata and tier values.

```csharp
PerkTreeInfo GetPerkTreeInfo(GameObj<PerkTreeTemplate> perkTree)
```

| Parameter | Type | Description |
|---|---|---|
| `perkTree` | `GameObj<PerkTreeTemplate>` | The typed perk tree handle to read. |

Returns `null` if the handle is null. Returns a `PerkTreeInfo` with an empty `Perks` list if the tree's perk array cannot be read. Called internally by `GetPerkTrees`; exposed publicly for callers that already hold a typed tree handle.

---

#### `GetAvailablePerks(leader)`

Returns all perks in the leader's perk trees that have not yet been learned. Availability is determined by comparing `Pointer` values of tree perks against the leader's current learned perk list.

```csharp
List<PerkInfo> GetAvailablePerks(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |

Returns an empty list if the leader handle is null, or if the leader has learned all perks in their trees. Returned entries include `Tier` values. Use `CanBePromoted` to check whether the leader actually has promotion points available before acting on this list.

---

#### `FindPerkByName(leader, perkName)`

Searches all of a leader's perk trees for a perk whose `Name` or `Title` contains the given string (case-insensitive). If no match is found, logs a warning listing the first ten perks seen.

```csharp
GameObj<PerkTemplate> FindPerkByName(GameObj<BaseUnitLeader> leader, string perkName)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader whose perk trees to search. |
| `perkName` | `string` | Partial or full name/title to search for. |

Returns a default (null) `GameObj<PerkTemplate>` if the leader handle is null, `perkName` is null or empty, or no match is found. Searches both `Name` and `Title` fields; useful for dev tooling and console commands where exact IDs may not be known.

---

#### `GetLastPerk(leader)`

Returns a typed handle to the most recently added perk on the leader.

```csharp
GameObj<PerkTemplate> GetLastPerk(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to query. |

Returns a default (null) `GameObj<PerkTemplate>` if the leader handle is null, or if the leader has no learned perks. Intended as a companion to `RemoveLastPerk` — call this first to inspect or log the perk before removing it.

---

### Checks

#### `CanBePromoted(leader)`

Checks whether the leader has room to learn additional perks, delegating to `BaseUnitLeader.CanBePromoted()`.

```csharp
bool CanBePromoted(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to check. |

Returns `false` if the leader handle is null or the call fails.

---

#### `CanBeDemoted(leader)`

Checks whether the leader has at least one perk that can be removed, delegating to `BaseUnitLeader.CanBeDemoted()`.

```csharp
bool CanBeDemoted(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to check. |

Returns `false` if the leader handle is null or the call fails. Always call this before `RemoveLastPerk` to avoid a no-op.

---

#### `HasPerk(leader, perkTemplate)`

Checks whether the leader has already learned a specific perk, delegating to `BaseUnitLeader.HasPerk()`.

```csharp
bool HasPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perkTemplate)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to check. |
| `perkTemplate` | `GameObj<PerkTemplate>` | The perk template to look for. |

Returns `false` if either handle is null or the call fails.

---

### Write Operations

Write methods perform a liveness check on all handles before invoking the underlying game method. They are no-ops if any handle is null.

#### `AddPerk(leader, perkTemplate, spendPromotionPoints)`

Adds a perk to a unit leader by invoking `BaseUnitLeader.AddPerk()`.

```csharp
bool AddPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perkTemplate, bool spendPromotionPoints = true)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to add the perk to. |
| `perkTemplate` | `GameObj<PerkTemplate>` | The perk template to add. |
| `spendPromotionPoints` | `bool` | Whether to deduct promotion points as part of the operation. Defaults to `true`. Pass `false` to add the perk without consuming promotion points. |

Returns `true` if the call succeeded; `false` if either handle is null or an exception is thrown. Does not validate whether the leader already has the perk or meets tier prerequisites — call `HasPerk` and `CanBePromoted` beforehand if those checks matter.

---

#### `RemoveLastPerk(leader)`

Removes the most recently added perk from a unit leader by invoking `BaseUnitLeader.TryRemoveLastPerk()`.

```csharp
bool RemoveLastPerk(GameObj<BaseUnitLeader> leader)
```

| Parameter | Type | Description |
|---|---|---|
| `leader` | `GameObj<BaseUnitLeader>` | The unit leader to demote. |

Returns `true` if removal succeeded; `false` if the leader handle is null, `TryRemoveLastPerk` returns false, or an exception is thrown. Always guard with `CanBeDemoted` before calling. To inspect which perk will be removed, call `GetLastPerk` first.

---

## Console Commands

`RegisterConsoleCommands()` registers the following dev console commands. Call it once during `OnInitialize` or `OnSceneLoaded`.

| Command | Arguments | Description |
|---|---|---|
| `perks` | `<nickname>` | Print all learned perks for the named unit leader. |
| `perktrees` | `<nickname>` | Print all perk trees available to the named leader, with tier-annotated perk listings. |
| `availableperks` | `<nickname>` | Print all perks the leader has not yet learned, grouped by tier, alongside their promotion eligibility. |
| `addperk` | `<nickname> <perk>` | Add a perk to the named leader at no promotion point cost. Perk is matched by name or title (case-insensitive, partial match). |
| `removeperk` | `<nickname>` | Remove the most recently added perk from the named leader. |

Example session:

```
> perks Kovacs
Kovacs's Perks (2):
  Iron Will
  Suppression Tactics [Active]

> perktrees Kovacs
Kovacs's Perk Trees (2):
  perk_tree_infantry (4 perks):
    T1: Combat Instincts
    T2: Iron Will
    T3: Suppression Tactics
    T4: Battlefield Commander
  perk_tree_support (4 perks):
    T1: Field Medic
    T2: Logistics
    T3: Fire Support
    T4: Combined Arms

> availableperks Kovacs
Available Perks (6) - Can Promote: True
  Tier 1:
    Field Medic
  Tier 3:
    Battlefield Commander
    Fire Support
  Tier 4:
    Combined Arms
    Logistics
    Battlefield Commander

> addperk Kovacs Iron Will
Perk 'Iron Will' already in Kovacs's perk list

> addperk Kovacs Field Medic
Added perk 'Field Medic' to Kovacs

> removeperk Kovacs
Removed perk 'Field Medic' from Kovacs
```

---

## Error Handling

All methods catch internal exceptions and report them via `SdkLogger.Error`. No method propagates exceptions to the caller.

| Method | Fallback on error |
|---|---|
| `GetLeaderPerks` | Empty `List<PerkInfo>` |
| `GetPerkInfo` | `null` |
| `GetPerkTrees` | Empty `List<PerkTreeInfo>` |
| `GetPerkTreeInfo` | `null` |
| `GetAvailablePerks` | Empty `List<PerkInfo>` |
| `FindPerkByName` | Default `GameObj<PerkTemplate>` |
| `GetLastPerk` | Default `GameObj<PerkTemplate>` |
| `CanBePromoted` | `false` |
| `CanBeDemoted` | `false` |
| `HasPerk` | `false` |
| `AddPerk` | `false` |
| `RemoveLastPerk` | `false` |