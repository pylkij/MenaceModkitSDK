using System;
using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime;
using Menace.ModpackLoader;
using UnityEngine;

using Menace.SDK.Internal;

namespace Menace.SDK;

// Runtime visual overrides for character prefabs.
public static class CharacterVisuals
{
    // Configuration for a character visual override.
    public class OverrideConfig
    {
        internal Dictionary<string, Mesh> MeshReplacements { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, Material> MaterialReplacements { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<int, Mesh> MeshReplacementsByIndex { get; } = new();
        internal GameObject SourcePrefab { get; set; }

        // Replace a mesh by child GameObject name (e.g., "LOD0", "rmc_default_female_soldier_LOD0").
        public OverrideConfig ReplaceMesh(string childName, Mesh mesh)
        {
            if (mesh != null)
                MeshReplacements[childName] = mesh;
            return this;
        }

        // Replace a mesh by LOD index (0 = highest detail, 3 = lowest).
        public OverrideConfig ReplaceMeshByLodIndex(int lodIndex, Mesh mesh)
        {
            if (mesh != null)
                MeshReplacementsByIndex[lodIndex] = mesh;
            return this;
        }

        // Replace a material by name.
        public OverrideConfig ReplaceMaterial(string materialName, Material material)
        {
            if (material != null)
                MaterialReplacements[materialName] = material;
            return this;
        }

        // Use meshes and materials from a source prefab (e.g., loaded from GLB).
        // Meshes are matched by child name.
        public OverrideConfig UseSourcePrefab(GameObject prefab)
        {
            SourcePrefab = prefab;
            return this;
        }
    }

    private static readonly Dictionary<string, OverrideConfig> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<int> _processedInstances = new();
    private static bool _hooked;

    // Register a visual override.
    public static void RegisterOverride(string prefabName, Action<OverrideConfig> configure)
    {
        EnsureHooked();

        var config = new OverrideConfig();
        configure(config);
        _overrides[prefabName] = config;

        SdkLogger.Msg($"[CharacterVisuals] Registered override for '{prefabName}'");
    }

    // Register an override using a GLB root prefab registered by GlbLoader.
    public static void RegisterOverrideFromGlb(string prefabName, string glbAssetName)
    {
        EnsureHooked();

        var glbPrefab = BundleLoader.GetAsset<GameObject>(glbAssetName);
        if (glbPrefab == null)
        {
            SdkLogger.Warning($"[CharacterVisuals] GLB prefab '{glbAssetName}' not found. Make sure it's loaded first.");
            return;
        }

        var config = new OverrideConfig { SourcePrefab = glbPrefab };
        _overrides[prefabName] = config;

        SdkLogger.Msg($"[CharacterVisuals] Registered GLB override for '{prefabName}' using '{glbAssetName}'");
    }

    // Register an override from explicit mesh/material mappings.
    public static void RegisterOverride(
        string prefabName,
        Dictionary<string, Mesh> meshReplacements = null,
        Dictionary<string, Material> materialReplacements = null)
    {
        RegisterOverride(prefabName, config =>
        {
            if (meshReplacements != null)
            {
                foreach (var (name, mesh) in meshReplacements)
                    config.ReplaceMesh(name, mesh);
            }
            if (materialReplacements != null)
            {
                foreach (var (name, mat) in materialReplacements)
                    config.ReplaceMaterial(name, mat);
            }
        });
    }

    public static bool RemoveOverride(string prefabName)
    {
        return _overrides.Remove(prefabName);
    }

    public static void ClearOverrides()
    {
        _overrides.Clear();
        _processedInstances.Clear();
    }

    public static string[] GetRegisteredOverrides()
    {
        return _overrides.Keys.ToArray();
    }

    // Apply a visual override to an existing entity GameObject.
    public static bool ApplyOverride(GameObject entityGameObject, string prefabName = null)
    {
        if (entityGameObject == null) return false;

        prefabName ??= DetectPrefabName(entityGameObject);
        if (string.IsNullOrEmpty(prefabName)) return false;

        if (!_overrides.TryGetValue(prefabName, out var config))
            return false;

        ApplyOverrideConfig(entityGameObject, config, prefabName);
        return true;
    }

    // Apply registered overrides to already-spawned entities.
    public static int ApplyToExistingEntities()
    {
        if (_overrides.Count == 0) return 0;

        int applied = 0;
        try
        {
            var smrType = Il2CppType.From(typeof(SkinnedMeshRenderer));
            var allSmrs = Resources.FindObjectsOfTypeAll(smrType);

            foreach (var obj in allSmrs)
            {
                try
                {
                    var smr = obj.Cast<SkinnedMeshRenderer>();
                    if (smr == null || smr.gameObject == null) continue;

                    var root = FindEntityRoot(smr.transform);
                    if (root == null) continue;

                    var instanceId = root.GetInstanceID();
                    if (_processedInstances.Contains(instanceId)) continue;

                    var prefabName = DetectPrefabName(root);
                    if (!string.IsNullOrEmpty(prefabName) && _overrides.TryGetValue(prefabName, out var config))
                    {
                        ApplyOverrideConfig(root, config, prefabName);
                        _processedInstances.Add(instanceId);
                        applied++;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.ApplyToExistingEntities", ex.Message);
        }

        if (applied > 0)
            SdkLogger.Msg($"[CharacterVisuals] Applied overrides to {applied} existing entities");

        return applied;
    }

    private static void EnsureHooked()
    {
        if (_hooked) return;

        try
        {
            TacticalEventHooks.OnEntitySpawned += OnEntitySpawned;
            _hooked = true;
            SdkLogger.Msg("[CharacterVisuals] Hooked into entity spawn events");
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.EnsureHooked",
                $"Failed to hook spawn events: {ex.Message}");
        }
    }

    private static void OnEntitySpawned(IntPtr entityPtr)
    {
        if (entityPtr == IntPtr.Zero || _overrides.Count == 0) return;

        try
        {
            var entity = new GameObj(entityPtr);
            var gameObject = GetEntityGameObject(entity);
            if (gameObject == null) return;

            var instanceId = gameObject.GetInstanceID();
            if (_processedInstances.Contains(instanceId)) return;

            var prefabName = DetectPrefabName(gameObject);
            if (string.IsNullOrEmpty(prefabName)) return;

            if (_overrides.TryGetValue(prefabName, out var config))
            {
                ApplyOverrideConfig(gameObject, config, prefabName);
                _processedInstances.Add(instanceId);
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.OnEntitySpawned", ex.Message);
        }
    }

    private static GameObject GetEntityGameObject(GameObj entity)
    {
        try
        {
            var managedType = GameType.Find("Menace.Tactical.Actor")?.ManagedType;
            if (managedType == null) return null;

            var proxy = Il2CppUtils.GetManagedProxy(entity, managedType);
            if (proxy == null) return null;

            var goProp = managedType.GetProperty("gameObject")
                      ?? managedType.GetProperty("GameObject");
            if (goProp != null)
            {
                var go = goProp.GetValue(proxy);
                if (go != null)
                    return ((Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)go).Cast<GameObject>();
            }

            var transformProp = managedType.GetProperty("transform");
            if (transformProp != null)
            {
                var transform = transformProp.GetValue(proxy);
                if (transform != null)
                {
                    var t = ((Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)transform).Cast<Transform>();
                    return t?.gameObject;
                }
            }
        }
        catch { }

        return null;
    }

    private static string DetectPrefabName(GameObject gameObject)
    {
        if (gameObject == null) return null;

        var name = gameObject.name;
        if (string.IsNullOrEmpty(name)) return null;

        if (name.EndsWith("(Clone)"))
            name = name.Substring(0, name.Length - 7).Trim();

        if (_overrides.ContainsKey(name))
            return name;

        var transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;

            var childName = child.name;
            if (!string.IsNullOrEmpty(childName))
            {
                if (childName.EndsWith("(Clone)"))
                    childName = childName.Substring(0, childName.Length - 7).Trim();

                if (_overrides.ContainsKey(childName))
                    return childName;
            }
        }

        return name;
    }

    private static GameObject FindEntityRoot(Transform transform)
    {
        var current = transform;
        while (current != null)
        {
            if (current.GetComponent<Animator>() != null)
                return current.gameObject;

            var name = current.name;
            if (name != null && (name.StartsWith("rmc_") || name.Contains("soldier") || name.Contains("enemy")))
                return current.gameObject;

            current = current.parent;
        }

        return transform?.gameObject;
    }

    private static void ApplyOverrideConfig(GameObject target, OverrideConfig config, string prefabName)
    {
        int meshesReplaced = 0;
        int materialsReplaced = 0;

        try
        {
            var smrs = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (config.SourcePrefab != null)
            {
                ApplyFromSourcePrefab(target, config.SourcePrefab, smrs,
                    ref meshesReplaced, ref materialsReplaced);
            }

            foreach (var smr in smrs)
            {
                if (smr == null) continue;

                var smrName = smr.gameObject.name;

                if (config.MeshReplacements.TryGetValue(smrName, out var mesh))
                {
                    smr.sharedMesh = mesh;
                    meshesReplaced++;
                }
                else
                {
                    foreach (var (pattern, replacementMesh) in config.MeshReplacements)
                    {
                        if (smrName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                            smrName.EndsWith("_" + pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            smr.sharedMesh = replacementMesh;
                            meshesReplaced++;
                            break;
                        }
                    }
                }

                if (config.MaterialReplacements.Count > 0 && smr.sharedMaterials != null)
                {
                    var mats = smr.sharedMaterials;
                    bool changed = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;

                        var matName = mats[i].name;
                        if (config.MaterialReplacements.TryGetValue(matName, out var replacement))
                        {
                            mats[i] = replacement;
                            changed = true;
                            materialsReplaced++;
                        }
                    }

                    if (changed)
                        smr.sharedMaterials = mats;
                }
            }

            if (config.MeshReplacementsByIndex.Count > 0)
            {
                var lodGroup = target.GetComponent<LODGroup>();
                if (lodGroup != null)
                {
                    var lods = lodGroup.GetLODs();
                    for (int i = 0; i < lods.Length && i < 4; i++)
                    {
                        if (!config.MeshReplacementsByIndex.TryGetValue(i, out var mesh))
                            continue;

                        foreach (var renderer in lods[i].renderers)
                        {
                            if (renderer is SkinnedMeshRenderer lodSmr)
                            {
                                lodSmr.sharedMesh = mesh;
                                meshesReplaced++;
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < smrs.Length; i++)
                    {
                        if (config.MeshReplacementsByIndex.TryGetValue(i, out var mesh))
                        {
                            smrs[i].sharedMesh = mesh;
                            meshesReplaced++;
                        }
                    }
                }
            }

            if (meshesReplaced > 0 || materialsReplaced > 0)
            {
                SdkLogger.Msg($"[CharacterVisuals] Applied override to '{prefabName}': " +
                    $"{meshesReplaced} meshes, {materialsReplaced} materials");
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.ApplyOverrideConfig",
                $"Failed to apply override for '{prefabName}': {ex.Message}");
        }
    }

    private static void ApplyFromSourcePrefab(
        GameObject target,
        GameObject source,
        SkinnedMeshRenderer[] targetSmrs,
        ref int meshesReplaced,
        ref int materialsReplaced)
    {
        var sourceSmrs = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (sourceSmrs == null || sourceSmrs.Length == 0)
        {
            var sourceMfs = source.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in sourceMfs)
            {
                if (mf?.sharedMesh == null) continue;

                var mfName = mf.gameObject.name;

                foreach (var targetSmr in targetSmrs)
                {
                    if (targetSmr == null) continue;

                    var targetName = targetSmr.gameObject.name;
                    if (NamesMatch(mfName, targetName))
                    {
                        targetSmr.sharedMesh = mf.sharedMesh;
                        meshesReplaced++;

                        var mr = mf.GetComponent<MeshRenderer>();
                        if (mr?.sharedMaterials != null)
                        {
                            targetSmr.sharedMaterials = mr.sharedMaterials;
                            materialsReplaced += mr.sharedMaterials.Length;
                        }
                        break;
                    }
                }
            }
            return;
        }

        foreach (var sourceSmr in sourceSmrs)
        {
            if (sourceSmr?.sharedMesh == null) continue;

            var sourceName = sourceSmr.gameObject.name;

            foreach (var targetSmr in targetSmrs)
            {
                if (targetSmr == null) continue;

                var targetName = targetSmr.gameObject.name;
                if (NamesMatch(sourceName, targetName))
                {
                    targetSmr.sharedMesh = sourceSmr.sharedMesh;
                    meshesReplaced++;

                    if (sourceSmr.sharedMaterials != null)
                    {
                        targetSmr.sharedMaterials = sourceSmr.sharedMaterials;
                        materialsReplaced += sourceSmr.sharedMaterials.Length;
                    }
                    break;
                }
            }
        }
    }

    private static bool NamesMatch(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            return true;

        if (target.EndsWith(source, StringComparison.OrdinalIgnoreCase))
            return true;
        if (source.EndsWith(target, StringComparison.OrdinalIgnoreCase))
            return true;

        var sourceLod = ExtractLodSuffix(source);
        var targetLod = ExtractLodSuffix(target);
        if (!string.IsNullOrEmpty(sourceLod) && sourceLod == targetLod)
            return true;

        return false;
    }

    private static string ExtractLodSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var idx = name.LastIndexOf("LOD", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0 && idx + 4 <= name.Length)
        {
            var suffix = name.Substring(idx);
            if (suffix.Length >= 4 && char.IsDigit(suffix[3]))
                return suffix.Substring(0, 4).ToUpperInvariant();
        }

        return null;
    }
}
