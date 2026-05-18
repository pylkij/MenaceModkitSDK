using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tools;
using MelonLoader;
using Menace.SDK;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

using DataTemplateLoader = Il2CppMenace.Tools.DataTemplateLoader;
using DataTemplate = Il2CppMenace.Tools.DataTemplate;
using Il2CppDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppMenace.Tools.DataTemplate>;

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

    private static bool TryExtendTemplateArray(Type templateType, DataTemplate clone, MelonLogger.Instance log)
    {
        var singleton = DataTemplateLoader.GetSingleton();
        if (singleton == null)
            return false;

        var arrays = singleton.m_TemplateArrays;
        if (arrays == null)
        {
            log.Warning("TryExtendTemplateArray: m_TemplateArrays is null.");
            return false;
        }

        var il2cppType = Il2CppType.From(templateType);
        if (il2cppType == null)
            return false;

        var arraysType = arrays.GetType();
        var tryGet = arraysType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m =>
            {
                if (m.Name != "TryGetValue") return false;
                var p = m.GetParameters();
                return p.Length == 2 && p[1].ParameterType.IsByRef;
            });

        if (tryGet == null)
        {
            log.Warning($"TryExtendTemplateArray: TryGetValue not found on {arraysType.FullName}.");
            return false;
        }

        var lookup = new object[] { il2cppType, null };
        if (!(bool)tryGet.Invoke(arrays, lookup) || lookup[1] is not Il2CppObjectBase oldArray)
        {
            log.Warning($"TryExtendTemplateArray: no existing array for '{templateType.Name}'.");
            return false;
        }

        var oldArrayType = oldArray.GetType();
        var lengthProp = oldArrayType.GetProperty("Length")
            ?? oldArrayType.GetProperty("Count");
        if (lengthProp == null)
        {
            log.Warning($"TryExtendTemplateArray: no Length/Count on {oldArrayType.FullName}.");
            return false;
        }

        int oldLength;
        try { oldLength = (int)lengthProp.GetValue(oldArray); }
        catch (Exception ex)
        {
            log.Warning($"TryExtendTemplateArray: reading Length threw: {ex.Message}");
            return false;
        }

        // Read element class from the existing native array — do NOT derive it from
        // templateType. The game stores arrays whose IL2CPP element class is the
        // concrete subtype. Using the wrong element class here causes GetAll<T> to
        // hang on new-game start.
        var oldArrayPointer = oldArray.Pointer;
        if (oldArrayPointer == IntPtr.Zero)
            return false;

        var arrayClass = IL2CPP.il2cpp_object_get_class(oldArrayPointer);
        var elementClass = IL2CPP.il2cpp_class_get_element_class(arrayClass);
        if (elementClass == IntPtr.Zero)
        {
            log.Warning($"TryExtendTemplateArray: element class is null for '{templateType.Name}'.");
            return false;
        }

        var newNativeArray = IL2CPP.il2cpp_array_new(elementClass, (ulong)(oldLength + 1));
        if (newNativeArray == IntPtr.Zero)
        {
            log.Warning($"TryExtendTemplateArray: il2cpp_array_new returned null.");
            return false;
        }

        var wrapperCtor = oldArrayType.GetConstructor(new[] { typeof(IntPtr) });
        if (wrapperCtor == null)
        {
            log.Warning($"TryExtendTemplateArray: no IntPtr ctor on {oldArrayType.FullName}.");
            return false;
        }

        object newArray;
        try { newArray = wrapperCtor.Invoke(new object[] { newNativeArray }); }
        catch (Exception ex)
        {
            log.Warning($"TryExtendTemplateArray: ctor threw: {ex.InnerException?.Message ?? ex.Message}");
            return false;
        }

        // Find the int-indexed property on the array wrapper
        var indexer = oldArrayType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p =>
            {
                var idx = p.GetIndexParameters();
                return idx.Length == 1 && idx[0].ParameterType == typeof(int);
            });

        if (indexer == null)
        {
            log.Warning($"TryExtendTemplateArray: no int indexer on {oldArrayType.FullName}.");
            return false;
        }

        try
        {
            var slot = new object[1];
            for (var i = 0; i < oldLength; i++)
            {
                slot[0] = i;
                indexer.SetValue(newArray, indexer.GetValue(oldArray, slot), slot);
            }
            slot[0] = oldLength;
            indexer.SetValue(newArray, clone, slot);
        }
        catch (Exception ex)
        {
            log.Warning($"TryExtendTemplateArray: copy threw: {ex.InnerException?.Message ?? ex.Message}");
            return false;
        }

        // Replace the slot in m_TemplateArrays in place
        var dictIndexer = arraysType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p =>
            {
                var idx = p.GetIndexParameters();
                return idx.Length == 1 && idx[0].ParameterType == il2cppType.GetType();
            });

        if (dictIndexer == null)
        {
            log.Warning($"TryExtendTemplateArray: no Il2CppType-keyed indexer on {arraysType.FullName}.");
            return false;
        }

        try { dictIndexer.SetValue(arrays, newArray, new object[] { il2cppType }); }
        catch (Exception ex)
        {
            log.Warning($"TryExtendTemplateArray: dict write threw: {ex.InnerException?.Message ?? ex.Message}");
            return false;
        }

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
    /// Registers a native asset clone into DataTemplateLoader's internal registry.
    /// Inserts into m_TemplateMaps[templateType][cloneId] so Get&lt;T&gt;/TryGet&lt;T&gt; resolve it,
    /// and extends m_TemplateArrays[templateType] so GetAll&lt;T&gt; consumers see it.
    /// </summary>
    private bool RegisterInLoader(Assembly gameAssembly, UnityEngine.Object nativeAsset, Type templateType, string cloneId, MelonLogger.Instance log)
    {
        if (!EnsureSlotMaterialised(gameAssembly, templateType, out var innerMap, log))
            return false;

        if (innerMap.ContainsKey(cloneId))
        {
            log.Msg($"RegisterInLoader: '{cloneId}' already registered, skipping.");
            return true;
        }

        // Cast clone to the template type
        var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
        var castClone = genericTryCast.Invoke(nativeAsset, null);
        if (castClone == null)
        {
            log.Warning($"RegisterInLoader: failed to cast clone to {templateType.Name}.");
            return false;
        }

        innerMap[cloneId] = castClone as DataTemplate;

        if (!innerMap.ContainsKey(cloneId))
        {
            log.Warning($"RegisterInLoader: post-write verification failed for '{cloneId}'.");
            return false;
        }

        log.Msg($"RegisterInLoader: '{cloneId}' registered in m_TemplateMaps.");
        if (!TryExtendTemplateArray(templateType, castClone as DataTemplate, log))
        {
            log.Warning(
                $"RegisterInLoader: m_TemplateArrays extend failed for '{cloneId}' — "
                + $"GetAll<{templateType.Name}> consumers will not see this clone.");
            // Non-fatal: m_TemplateMaps insertion succeeded, Get<T>/TryGet<T> will still resolve
        }
        return true;
    }

    private static bool TryGetInnerMap(Type templateType, out Il2CppDictionary innerMap)
    {
        innerMap = null;

        var singleton = DataTemplateLoader.GetSingleton();
        if (singleton == null)
            return false;

        var templateMaps = singleton.m_TemplateMaps;
        if (templateMaps == null)
            return false;

        var il2cppType = Il2CppType.From(templateType);
        if (il2cppType == null)
            return false;

        return templateMaps.TryGetValue(il2cppType, out innerMap) && innerMap != null;
    }

    private static bool EnsureSlotMaterialised(Assembly gameAssembly, Type templateType, out Il2CppDictionary innerMap, MelonLogger.Instance log)
    {
        if (TryGetInnerMap(templateType, out innerMap))
            return true;

        EnsureTemplatesLoaded(gameAssembly, templateType);

        if (TryGetInnerMap(templateType, out innerMap))
            return true;

        log.Warning(
            $"EnsureSlotMaterialised: slot for '{templateType.Name}' not present after GetAll<T>() — will retry next scene.");
        return false;
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
                _appliedCloneKeys.Add(cloneKey);
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
                    _appliedCloneKeys.Add(cloneKey);
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
