# Templates API Reference

`Templates` is a static class in the `Menace.SDK` namespace. It wraps IL2CPP calls to the game's `DataTemplateLoader` and guarantees that template types are materialised into memory before any query runs. Call these methods any time after `GameState.SceneLoaded` has fired — the internal `EnsureLoaded` mechanism runs automatically on first access per type.

> **Clone support is not yet part of this API.** The complete clone pipeline — `m_TemplateMaps` registration, `m_TemplateArrays` extension, and ancestor mirroring — is currently implemented in `ModpackLoader` and will be promoted here once that research is verified and stable.

---

## Quick Reference

| Method | Returns | Category |
|---|---|---|
| `FindByID<T>(id)` | `T` | Queries |
| `TryGet<T>(id, out template)` | `bool` | Queries |
| `FindAll<T>()` | `IReadOnlyCollection<T>` | Queries |

---

## Quick Start

The example below is drawn from a real mod that patches `MissionTemplate` records at scene load. It illustrates the two main query patterns: `FindByID` for lookups where absence is a bug, and a null-check fallback consistent with its actual return behaviour.

```csharp
using MelonLoader;
using Menace.SDK;
using Il2CppMenace.Strategy.Missions;

namespace MyPlugin;

public class Plugin : IModpackPlugin
{
    private bool _templatesPatched;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony) { }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (_templatesPatched) return;
        if (sceneName != "Title") return;

        // FindByID — use when absence is always a bug.
        // Returns null and logs a warning on miss; does not throw.
        var missionTemplate = Templates.FindByID<MissionTemplate>("mission.pirates_combat_patrol");
        if (missionTemplate == null)
        {
            SdkLogger.Error("MissionTemplate not found — skipping patch.");
            return;
        }

        var mission = GameObj.FromPointer(missionTemplate.Pointer);
        mission.WriteBool(0xD9, false); // OFFSET_ENEMY_START_IN_SLEEP_MODE

        // TryGet — use when absence requires branch logic.
        // Still logs a warning on miss (see Remarks below).
        if (Templates.TryGet<MissionTemplate>("mission.pirates_sabotage_supply_infrastructure", out var sabotageTemplate))
        {
            var sabotage = GameObj.FromPointer(sabotageTemplate.Pointer);
            sabotage.WriteBool(0xD9, true);
        }

        _templatesPatched = true;
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

---

## Method Reference

### `FindByID<T>(id)`

Returns the template with the given ID, or `null` if no matching template is found. Calls `EnsureLoaded<T>()` before querying, so the type is guaranteed to be materialised.

```csharp
T FindByID<T>(string id) where T : DataTemplate
```

| Parameter | Type | Description |
|---|---|---|
| `id` | `string` | The stable `m_ID` of the template to retrieve. |

Returns `null` and emits a `SdkLogger.Warning` on miss. Does **not** throw `TemplateNotFoundException` — the exception referenced in the XML summary is not implemented in the current version.

> **Note:** Always null-check the return value before dereferencing. If a missing template is always a bug in your mod, log an error and skip the patch rather than proceeding with a null pointer.

---

### `TryGet<T>(id, out template)`

Returns `true` and populates `template` if a matching template is found. Returns `false` and sets `template` to `null` on miss.

```csharp
bool TryGet<T>(string id, out T template) where T : DataTemplate
```

| Parameter | Type | Description |
|---|---|---|
| `id` | `string` | The stable `m_ID` of the template to retrieve. |
| `template` | `out T` | Populated with the result on success; `null` on miss. |

> **Remarks:** Despite the name suggesting a silent probe, `TryGet` emits a `SdkLogger.Warning` on every miss — including cases where absence is an expected runtime condition. If you need a truly silent existence check, query `FindAll<T>()` and filter by ID instead.

---

### `FindAll<T>()`

Returns all loaded templates of type `T`.

```csharp
IReadOnlyCollection<T> FindAll<T>() where T : DataTemplate
```

Returns an `IReadOnlyCollection<T>`. If the internal Il2CPP cast to `Il2CppSystem.Collections.IEnumerable` fails, the method returns an empty collection with no warning or error logged.

> **Known limitation:** If `DataTemplateLoader.GetAll<T>()` returns an object that cannot be cast to `Il2CppSystem.Collections.IEnumerable`, `FindAll` silently returns an empty list. There is currently no fallback path and no diagnostic output. If you receive an unexpectedly empty collection, verify that the template type has entries registered in the game's template maps.

---

## Timing

Templates queries are safe to call any time after `GameState.SceneLoaded` has fired. Field handles are resolved automatically on the first scene load via `Templates.Initialize()`, which is registered internally — no setup is required in your plugin.

Calling any query method before `SceneLoaded` may return `null` or an empty collection, as the underlying `DataTemplateLoader` may not yet be populated.

---

## Error Handling

| Method | Fallback on miss |
|---|---|
| `FindByID<T>` | `null` + `SdkLogger.Warning` |
| `TryGet<T>` | `false`, `out null` + `SdkLogger.Warning` |
| `FindAll<T>` | Empty `IReadOnlyCollection<T>`, no log output |