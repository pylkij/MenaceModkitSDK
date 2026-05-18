using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using Menace.SDK;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Template cloning: deep-copies existing game templates (ScriptableObjects) via
/// UnityEngine.Object.Instantiate() and registers them in the DataTemplateLoader
/// registry so the game treats them as first-class templates.
/// </summary>
public partial class ModpackLoaderMod
{
    // Tracks which modpack+templateType clone sets have been applied
    private readonly HashSet<string> _appliedCloneKeys = new();

    private static uint _templateIdOffset = 0;

    private static uint GetTemplateIdOffset(IntPtr objectPointer)
    {
        if (_templateIdOffset != 0)
            return _templateIdOffset;

        var klass = IL2CPP.il2cpp_object_get_class(objectPointer);
        if (klass == IntPtr.Zero)
            return 0;

        _templateIdOffset = OffsetCache.GetOrResolve(klass, "m_ID");
        return _templateIdOffset;
    }

    private static bool TryWriteTemplateId(
    UnityEngine.Object clone,
    UnityEngine.Object nativeAsset,
    MelonLogger.Instance log)
    {
        if (clone is not Il2CppObjectBase cloneBase || nativeAsset is not Il2CppObjectBase assetBase)
            return false;

        var offset = GetTemplateIdOffset(assetBase.Pointer);
        if (offset == 0)
        {
            log.Warning("TryWriteTemplateId: could not resolve m_ID offset.");
            return false;
        }

        var sourceIdPtr = GameObj.FromPointer(assetBase.Pointer).ReadPtr(offset);
        if (sourceIdPtr == IntPtr.Zero)
        {
            log.Warning("TryWriteTemplateId: native asset m_ID pointer is zero.");
            return false;
        }

        GameObj.FromPointer(cloneBase.Pointer).WritePtr(offset, sourceIdPtr);
        return true;
    }

    /// <summary>
    /// Call DataTemplateLoader.GetAll&lt;T&gt;() to ensure the type's templates are loaded
    /// into the internal registry before we try to register clones.
    /// </summary>
    private static void EnsureTemplatesLoaded(Assembly gameAssembly, Type templateType)
    {
        try
        {
            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                SdkLogger.Warning("  DataTemplateLoader class not found in Assembly-CSharp");
                return;
            }

            var getAllMethod = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

            if (getAllMethod == null)
            {
                SdkLogger.Warning("  DataTemplateLoader.GetAll method not found");
                return;
            }

            var genericMethod = getAllMethod.MakeGenericMethod(templateType);
            genericMethod.Invoke(null, null);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"  EnsureTemplatesLoaded({templateType.Name}): {ex.Message}");
        }
    }

    /// <summary>
    /// Register a cloned template in DataTemplateLoader's internal registry.
    /// DataTemplateLoader has two dictionaries:
    /// - Offset 0x10: Dictionary&lt;Type, DataTemplate[]&gt; - all templates array
    /// - Offset 0x18: Dictionary&lt;Type, Dictionary&lt;string, DataTemplate&gt;&gt; - name lookup
    /// </summary>
    private bool RegisterInLoader(Assembly gameAssembly, UnityEngine.Object nativeAsset, Type templateType, string cloneId, MelonLogger.Instance log)
    {
        var loaderType = gameAssembly.GetTypes()
            .FirstOrDefault(t => t.FullName == "Menace.Tools.DataTemplateLoader"
                              || t.Name == "DataTemplateLoader");
        if (loaderType == null)
            return false;

        var getSingleton = loaderType.GetMethod(
            "GetSingleton", BindingFlags.NonPublic | BindingFlags.Static);
        if (getSingleton == null)
        {
            log.Warning("RegisterInLoader: GetSingleton not found.");
            return false;
        }

        var singleton = getSingleton.Invoke(null, null);
        if (singleton == null)
        {
            log.Warning("RegisterInLoader: GetSingleton returned null.");
            return false;
        }

        if (!EnsureSlotMaterialised(gameAssembly, singleton, templateType, out var innerMap, log))
            return false;

        // Check idempotency — if clone ID already exists, registration is complete
        var innerMapType = innerMap.GetType();
        var tryGet = innerMapType.GetMethod("TryGetValue");
        if (tryGet != null)
        {
            var checkArgs = new object[] { cloneId, null };
            if ((bool)tryGet.Invoke(innerMap, checkArgs))
            {
                log.Msg($"RegisterInLoader: '{cloneId}' already registered, skipping.");
                return true;
            }
        }

        // Cast clone to the template type
        var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
        var castClone = genericTryCast.Invoke(nativeAsset, null);
        if (castClone == null)
        {
            log.Warning($"RegisterInLoader: failed to cast clone to {templateType.Name}.");
            return false;
        }

        // Insert into inner map
        var indexer = innerMapType.GetProperty("Item");
        if (indexer == null)
        {
            log.Warning("RegisterInLoader: no Item indexer on inner map.");
            return false;
        }

        indexer.SetValue(innerMap, castClone, new object[] { cloneId });

        // Verify the write landed
        if (tryGet != null)
        {
            var verifyArgs = new object[] { cloneId, null };
            if (!(bool)tryGet.Invoke(innerMap, verifyArgs))
            {
                log.Warning($"RegisterInLoader: post-write verification failed for '{cloneId}'.");
                return false;
            }
        }

        log.Msg($"RegisterInLoader: '{cloneId}' registered in m_TemplateMaps.");
        return true;
    }

    private static object GetTemplateMaps(object singleton)
    {
        var field = singleton.GetType().GetField(
            "m_TemplateMaps",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(singleton);
    }

    private static object GetTemplateArrays(object singleton)
    {
        var field = singleton.GetType().GetField(
            "m_TemplateArrays",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(singleton);
    }

    private static bool TryGetInnerMap(
    object singleton,
    Type templateType,
    out object innerMap)
    {
        innerMap = null;

        var outerDict = GetTemplateMaps(singleton);
        if (outerDict == null)
            return false;

        var il2cppType = Il2CppType.From(templateType);
        if (il2cppType == null)
            return false;

        var tryGet = outerDict.GetType().GetMethod("TryGetValue");
        if (tryGet == null)
            return false;

        var args = new object[] { il2cppType, null };
        var found = (bool)tryGet.Invoke(outerDict, args);
        innerMap = args[1];
        return found && innerMap != null;
    }

    private static bool EnsureSlotMaterialised(Assembly gameAssembly, object singleton, Type templateType, out object innerMap, MelonLogger.Instance log)
    {
        if (TryGetInnerMap(singleton, templateType, out innerMap))
            return true;

        // Slot doesn't exist yet — force DataTemplateLoader.GetAll<T>() to create it
        EnsureTemplatesLoaded(gameAssembly, templateType);

        if (TryGetInnerMap(singleton, templateType, out innerMap))
            return true;

        log.Warning(
            $"EnsureSlotMaterialised: slot for '{templateType.Name}' not present after GetAll<T>() — will retry next scene.");
        return false;
    }

    /// <summary>
    /// Find an instance field by trying multiple name variants.
    /// </summary>
    private static FieldInfo FindInstanceField(Type type, params string[] names)
    {
        foreach (var name in names)
        {
            var field = type.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field;
        }
        return null;
    }

    /// <summary>
    /// Register clone templates from native assets (resources.assets) with DataTemplateLoader.
    /// Clones are embedded in resources.assets by BundleCompiler and registered in ResourceManager.
    /// We use Resources.Load() to retrieve them, using paths from the asset manifest.
    /// </summary>
    private void RegisterBundleClones()
    {
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            SdkLogger.Warning("RegisterBundleClones: Assembly-CSharp not found");
            return;
        }

        int registered = 0;

        // First, try to use the manifest for accurate resource paths
        foreach (var entry in CompiledAssetLoader.GetCloneEntries())
        {
            var cloneKey = $"native:{entry.TemplateType}:{entry.Name}";
            if (_appliedCloneKeys.Contains(cloneKey))
                continue;

            if (string.IsNullOrEmpty(entry.TemplateType))
            {
                SdkLogger.Warning($"  RegisterBundleClones: clone '{entry.Name}' has no template type");
                continue;
            }

            var templateType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == entry.TemplateType && !t.IsAbstract);

            if (templateType == null)
            {
                SdkLogger.Warning($"  RegisterBundleClones: type '{entry.TemplateType}' not found");
                continue;
            }

            try
            {
                // Ensure the game has loaded templates of this type
                EnsureTemplatesLoaded(gameAssembly, templateType);

                // Load using the manifest's resource path
                var il2cppType = Il2CppType.From(templateType);
                UnityEngine.Object cloneAsset = null;

                if (!string.IsNullOrEmpty(entry.ResourcePath))
                {
                    cloneAsset = Resources.Load(entry.ResourcePath, il2cppType);
                }

                if (cloneAsset == null)
                {
                    // Fallback: try standard folder naming
                    var fallbackPath = $"data/templates/{entry.TemplateType.ToLowerInvariant()}/{entry.Name}";
                    cloneAsset = Resources.Load(fallbackPath, il2cppType);
                }

                if (cloneAsset == null)
                {
                    SdkLogger.Warning($"  Clone '{entry.Name}' not found (tried: {entry.ResourcePath})");
                    continue;
                }

                // Register in DataTemplateLoader
                RegisterInLoader(gameAssembly, cloneAsset, templateType, entry.Name, LoggerInstance);
                registered++;

                SdkLogger.Msg($"  Registered native clone: {entry.Name} ({entry.TemplateType})");
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"  RegisterBundleClones '{entry.Name}': {ex.Message}");
            }
        }

        // Fallback: also check modpack clone definitions in case manifest is missing
        var clonesByType = new Dictionary<string, Dictionary<string, string>>();
        foreach (var modpack in _loadedModpacks.Values)
        {
            if (modpack.Clones == null) continue;
            foreach (var (templateTypeName, cloneMap) in modpack.Clones)
            {
                if (!clonesByType.TryGetValue(templateTypeName, out var existingMap))
                {
                    existingMap = new Dictionary<string, string>();
                    clonesByType[templateTypeName] = existingMap;
                }
                foreach (var (cloneName, sourceName) in cloneMap)
                {
                    existingMap[cloneName] = sourceName;
                }
            }
        }

        foreach (var (templateTypeName, cloneMap) in clonesByType)
        {
            var templateType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

            if (templateType == null)
                continue;

            EnsureTemplatesLoaded(gameAssembly, templateType);
            var il2cppType = Il2CppType.From(templateType);

            foreach (var (cloneName, sourceName) in cloneMap)
            {
                var cloneKey = $"native:{templateTypeName}:{cloneName}";
                if (_appliedCloneKeys.Contains(cloneKey))
                    continue;

                try
                {
                    var clonePath = $"data/templates/{templateTypeName.ToLowerInvariant()}/{cloneName}";
                    var cloneAsset = Resources.Load(clonePath, il2cppType);

                    if (cloneAsset == null)
                        continue; // Already logged by manifest path or not in resources

                    RegisterInLoader(gameAssembly, cloneAsset, templateType, cloneName, LoggerInstance);
                    registered++;

                    SdkLogger.Msg($"  Registered native clone (fallback): {cloneName} ({templateTypeName})");
                }
                catch (Exception ex)
                {
                    SdkLogger.Warning($"  RegisterBundleClones fallback '{cloneName}': {ex.Message}");
                }
            }
        }

        if (registered > 0)
        {
            SdkLogger.Msg($"Registered {registered} clone(s) from native assets");
            InvalidateNameLookupCache();
        }
    }
}
