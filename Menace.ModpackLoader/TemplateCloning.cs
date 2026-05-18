using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK;
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

    /// <summary>
    /// Call DataTemplateLoader.GetAll&lt;T&gt;() to ensure the type's templates are loaded
    /// into the internal registry before we try to register clones.
    /// </summary>
    private void EnsureTemplatesLoaded(Assembly gameAssembly, Type templateType)
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
    private void RegisterInLoader(Assembly gameAssembly, UnityEngine.Object clone, Type templateType, string name)
    {
        try
        {
            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.FullName == "Menace.Tools.DataTemplateLoader" ||
                                     t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                // Not a fatal error - clone still exists in Resources
                return;
            }

            // Get singleton via GetSingleton() method
            var getSingleton = loaderType.GetMethod("GetSingleton",
                BindingFlags.Public | BindingFlags.Static);

            if (getSingleton == null)
            {
                SdkLogger.Warning("  RegisterInLoader: GetSingleton method not found");
                return;
            }

            var singleton = getSingleton.Invoke(null, null);
            if (singleton == null)
            {
                SdkLogger.Warning("  RegisterInLoader: GetSingleton returned null");
                return;
            }

            // Access the name lookup dictionary at offset 0x18
            // It's Dictionary<Type, Dictionary<string, DataTemplate>>
            var singletonPtr = ((Il2CppObjectBase)singleton).Pointer;
            if (singletonPtr == IntPtr.Zero)
            {
                SdkLogger.Warning("  RegisterInLoader: singleton pointer is null");
                return;
            }

            // Read the dictionary pointer at offset 0x18
            var nameLookupPtr = Marshal.ReadIntPtr(singletonPtr + 0x18);
            if (nameLookupPtr == IntPtr.Zero)
            {
                SdkLogger.Warning("  RegisterInLoader: name lookup dictionary is null");
                return;
            }

            // Cast clone to the template type
            var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
            var castClone = genericTryCast.Invoke(clone, null);
            if (castClone == null)
            {
                SdkLogger.Warning($"  RegisterInLoader: failed to cast clone to {templateType.Name}");
                return;
            }

            // Try using reflection on the outer dictionary to get/create inner dictionary
            // and add the clone
            var outerDictType = singleton.GetType().GetField("m_NameLookup",
                BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType;

            // Get inner dictionary for this type via indexer or TryGetValue
            bool registered = TryRegisterViaReflection(gameAssembly, singleton, templateType, castClone, name);

            if (registered)
            {
                SdkLogger.Msg($"    Registered '{name}' in DataTemplateLoader");
            }
            else
            {
                SdkLogger.Warning($"  RegisterInLoader: could not register '{name}' — " +
                    "clone exists in memory but may not be findable via DataTemplateLoader.Get()");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"  RegisterInLoader failed for '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Try to register a clone using reflection on the DataTemplateLoader fields.
    /// The DataTemplateLoader has a field m_TemplateMaps of type
    /// Dictionary&lt;Type, Dictionary&lt;string, DataTemplate&gt;&gt;
    /// We need to add our clone to the inner dictionary for the template type.
    /// </summary>
    private bool TryRegisterViaReflection(Assembly gameAssembly, object singleton, Type templateType, object castClone, string name)
    {
        try
        {
            var loaderType = singleton.GetType();

            // First approach: try to find the field by known names
            var mapField = FindInstanceField(loaderType,
                "m_TemplateMaps", "m_NameLookup", "_templateMaps", "_nameLookup",
                "TemplateMaps", "NameLookup", "templateMaps", "nameLookup");

            if (mapField != null)
            {
                var outerDict = mapField.GetValue(singleton);
                if (outerDict != null)
                {
                    var il2cppType = Il2CppType.From(templateType);
                    if (TryAddToOuterDictionary(gameAssembly, templateType, outerDict, il2cppType, castClone, name))
                        return true;
                }
            }

            // Second approach: scan all dictionary fields for compatible types
            var fields = loaderType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                if (!fieldType.IsGenericType) continue;

                // Look for Dictionary<Type, Dictionary<string, T>>
                // In IL2CPP, the type names will be different, so check by structure
                var genArgs = fieldType.GetGenericArguments();
                if (genArgs.Length != 2) continue;

                // Check if first arg is Type-like (System.Type or Il2CppSystem.Type)
                var firstArgName = genArgs[0].Name;
                if (!firstArgName.Contains("Type")) continue;

                // Check if second arg is also a Dictionary
                if (!genArgs[1].IsGenericType) continue;
                var innerGenDef = genArgs[1].GetGenericTypeDefinition();
                var innerDefName = innerGenDef.Name;
                if (!innerDefName.StartsWith("Dictionary")) continue;

                var outerDict = field.GetValue(singleton);
                if (outerDict == null) continue;

                var il2cppType = Il2CppType.From(templateType);
                if (TryAddToOuterDictionary(gameAssembly, templateType, outerDict, il2cppType, castClone, name))
                    return true;
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    TryRegisterViaReflection: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Try to add a clone to an outer dictionary (Type -> inner dict) and then
    /// to the inner dictionary (string -> DataTemplate).
    /// If the inner dictionary doesn't exist, calls GetAll&lt;T&gt;() to force load
    /// templates and retries once.
    /// </summary>
    private bool TryAddToOuterDictionary(Assembly gameAssembly, Type templateType, object outerDict, object il2cppType, object castClone, string name)
    {
        try
        {
            var outerDictType = outerDict.GetType();
            var tryGetMethod = outerDictType.GetMethod("TryGetValue");

            if (tryGetMethod == null)
                return false;

            // Check if inner dict exists for this template type
            var parameters = new object[] { il2cppType, null };
            var exists = (bool)tryGetMethod.Invoke(outerDict, parameters);

            object innerDict;
            if (exists)
            {
                innerDict = parameters[1];
            }
            else
            {
                // Inner dictionary doesn't exist for this type
                // Force load templates via GetAll<T>() and retry
                SdkLogger.Msg($"    No inner dictionary for {templateType.Name}, forcing GetAll<T>()...");
                EnsureTemplatesLoaded(gameAssembly, templateType);

                // Retry the lookup
                parameters = new object[] { il2cppType, null };
                exists = (bool)tryGetMethod.Invoke(outerDict, parameters);

                if (!exists)
                {
                    SdkLogger.Warning($"    Still no inner dictionary after GetAll<T>() - template type may not be registered");
                    return false;
                }

                innerDict = parameters[1];
            }

            if (innerDict == null)
                return false;

            // Add to inner dictionary using indexer: innerDict[name] = castClone
            var innerDictType = innerDict.GetType();
            var innerIndexer = innerDictType.GetProperty("Item");
            if (innerIndexer != null)
            {
                innerIndexer.SetValue(innerDict, castClone, new object[] { name });
                return true;
            }

            // Try Add method as fallback
            var addMethod = innerDictType.GetMethod("Add");
            if (addMethod != null)
            {
                addMethod.Invoke(innerDict, new object[] { name, castClone });
                return true;
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    TryAddToOuterDictionary: {ex.Message}");
        }

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
                RegisterInLoader(gameAssembly, cloneAsset, templateType, entry.Name);
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

                    RegisterInLoader(gameAssembly, cloneAsset, templateType, cloneName);
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
