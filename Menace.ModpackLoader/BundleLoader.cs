using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Il2CppInterop.Runtime;
using Menace.SDK;
using UnityEngine;

namespace Menace.ModpackLoader;

// AssetBundle loader and runtime asset registry.
public static class BundleLoader
{
    private static readonly List<AssetBundle> _loadedBundles = new();

    private static readonly Dictionary<string, List<UnityEngine.Object>> _assetsByName
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, UnityEngine.Object> _assetsByTypeAndName
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> _assetSourceModpack
        = new(StringComparer.OrdinalIgnoreCase);

    public static int LoadedBundleCount => _loadedBundles.Count;

    public static int LoadedAssetCount => _assetsByTypeAndName.Count;

    public static void LoadBundles(string modpackDir, string modpackName)
    {
        if (!Directory.Exists(modpackDir))
            return;

        var bundleFiles = Directory.GetFiles(modpackDir, "*.bundle", SearchOption.AllDirectories);

        foreach (var bundlePath in bundleFiles)
        {
            var bundleFileName = Path.GetFileName(bundlePath);
            AssetBundle bundle = null;

            try
            {
                SdkLogger.Msg($"  [{modpackName}] Loading bundle: {bundleFileName}");
                bundle = AssetBundle.LoadFromFile(bundlePath);
            }
            catch (Exception loadEx)
            {
                SdkLogger.Error($"  [{modpackName}] LoadFromFile failed for {bundleFileName}: {loadEx.Message}");
                continue;
            }

            if (bundle == null)
            {
                SdkLogger.Warning($"  [{modpackName}] Failed to load bundle: {bundleFileName}");
                continue;
            }

            _loadedBundles.Add(bundle);
            SdkLogger.Msg($"  [{modpackName}] Loaded bundle: {bundleFileName}");

            string[] assetNames = null;
            try
            {
                assetNames = bundle.GetAllAssetNames();
            }
            catch (Exception namesEx)
            {
                SdkLogger.Warning($"    GetAllAssetNames failed: {namesEx.Message}");
            }

            if (assetNames != null && assetNames.Length > 0)
            {
                SdkLogger.Msg($"    Contains {assetNames.Length} asset(s)");

                foreach (var assetPath in assetNames)
                {
                    try
                    {
                        var asset = bundle.LoadAsset(assetPath);
                        if (asset != null)
                            RegisterAsset(asset, modpackName);
                    }
                    catch (Exception assetEx)
                    {
                        SdkLogger.Warning($"    Failed to load asset '{assetPath}': {assetEx.Message}");
                    }
                }
            }
            else
            {
                SdkLogger.Msg($"    No asset names, trying LoadAllAssets fallback...");
                try
                {
                    var allAssets = bundle.LoadAllAssets();
                    if (allAssets != null)
                    {
                        SdkLogger.Msg($"    Contains {allAssets.Length} asset(s) (fallback)");
                        foreach (var asset in allAssets)
                        {
                            if (asset == null) continue;
                            RegisterAsset(asset, modpackName);
                        }
                    }
                }
                catch (Exception fallbackEx)
                {
                    SdkLogger.Warning($"    LoadAllAssets fallback failed: {fallbackEx.Message}");
                }
            }
        }
    }

    private static void RegisterAsset(UnityEngine.Object asset, string modpackName)
    {
        var assetName = asset.name;
        var typeName = asset.GetIl2CppType().Name;
        SdkLogger.Msg($"    - {assetName} ({typeName})");

        RegisterAssetInternal(assetName, asset, typeName, modpackName);
    }

    public static void RegisterAsset(string name, UnityEngine.Object asset, string typeName, string modpackName = "Runtime")
    {
        SdkLogger.Msg($"  [BundleLoader] RegisterAsset: '{name}' type={typeName} asset={asset?.name ?? "null"} from={modpackName}");
        RegisterAssetInternal(name, asset, typeName, modpackName);
    }

    private static void RegisterAssetInternal(string assetName, UnityEngine.Object asset, string typeName, string modpackName)
    {
        if (!_assetsByName.TryGetValue(assetName, out var list))
        {
            list = new List<UnityEngine.Object>();
            _assetsByName[assetName] = list;
        }
        list.Add(asset);

        var key = $"{typeName}:{assetName}";
        _assetsByTypeAndName[key] = asset;
        _assetSourceModpack[key] = modpackName;
    }

    public static UnityEngine.Object GetAsset(string name)
    {
        if (_assetsByName.TryGetValue(name, out var list) && list.Count > 0)
            return list[0];
        return null;
    }

    public static T GetAsset<T>(string name) where T : UnityEngine.Object
    {
        var typeName = Il2CppType.From(typeof(T)).Name;
        var key = $"{typeName}:{name}";
        if (_assetsByTypeAndName.TryGetValue(key, out var obj))
        {
            try { return obj.Cast<T>(); }
            catch { return null; }
        }

        if (_assetsByName.TryGetValue(name, out var list))
        {
            foreach (var item in list)
            {
                try { return item.Cast<T>(); }
                catch { }
            }
        }

        return null;
    }

    public static List<UnityEngine.Object> GetAllAssets(string name)
    {
        if (_assetsByName.TryGetValue(name, out var list))
            return new List<UnityEngine.Object>(list);
        return new List<UnityEngine.Object>();
    }

    public static List<UnityEngine.Object> GetAssetsByType(string typeName)
    {
        var results = new List<UnityEngine.Object>();
        var prefix = $"{typeName}:";
        foreach (var kvp in _assetsByTypeAndName)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                results.Add(kvp.Value);
        }

        return results;
    }

    public static bool HasAsset(string name)
    {
        return _assetsByName.ContainsKey(name);
    }

    public static void UnloadAll()
    {
        foreach (var bundle in _loadedBundles)
        {
            try
            {
                bundle.Unload(false);
            }
            catch { }
        }
        _loadedBundles.Clear();
        _assetsByName.Clear();
        _assetsByTypeAndName.Clear();
        _assetSourceModpack.Clear();
    }
}
