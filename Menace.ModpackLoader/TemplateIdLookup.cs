using Il2CppInterop.Runtime;
using Il2CppMenace.Tools;
using Menace.SDK;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Scans the game assembly for DataTemplate-derived types and reads their m_IDs,
/// providing a name-based lookup that doesn't depend on resource paths.
/// </summary>
public static class TemplateIdLookup
{
    private static readonly string CachePath = Path.Combine(
        Directory.GetCurrentDirectory(), "Mods", "compiled", "template_id_cache.json");

    private static string _gameVersion;
    private static Assembly _gameAssembly;
    private static MethodInfo _getAllMethod;
    private static uint? _templateIdOffset;
    private static bool _initialized = false;

    // Type -> set of known m_IDs
    private static readonly Dictionary<Type, HashSet<string>> _knownIds = new();

    private class TemplateIdCache
    {
        public string GameVersion { get; set; }
        public Dictionary<string, List<string>> Types { get; set; } = new();
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (_gameAssembly == null)
        {
            SdkLogger.Warning("[TemplateIdLookup] Assembly-CSharp not found");
            return;
        }

        var loaderType = _gameAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "DataTemplateLoader");

        _getAllMethod = loaderType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

        if (_getAllMethod == null)
            SdkLogger.Warning("[TemplateIdLookup] DataTemplateLoader.GetAll not found");

        _gameVersion = GetGameVersion();
        LoadCacheFromDisk();

        var dataTemplateBase = GameType.Of<DataTemplate>();

        var templateTypes = _gameAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(DataTemplate).IsAssignableFrom(t))
            .ToList();

        SdkLogger.Msg($"[TemplateIdLookup] Initializing: {templateTypes.Count} DataTemplate type(s) found");

        foreach (var type in templateTypes)
        {
            GetKnownIds(type);
        }

        SdkLogger.Msg("[TemplateIdLookup] Initialization complete");
    }

    /// <summary>
    /// Scan all known m_IDs for a given template type.
    /// Results are cached for subsequent calls.
    /// </summary>
    public static HashSet<string> GetKnownIds(Type templateType)
    {
        if (_knownIds.TryGetValue(templateType, out var cached))
            return cached;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (templateType.IsAbstract || !typeof(DataTemplate).IsAssignableFrom(templateType))
        {
            SdkLogger.Warning($"[TemplateIdLookup] Type '{templateType.Name}' is not a valid DataTemplate subtype");
            _knownIds[templateType] = ids;
            return ids;
        }

        try
        {
            // Prime DataTemplateLoader for this specific type
            _getAllMethod?.MakeGenericMethod(templateType).Invoke(null, null);

            var il2cppType = Il2CppType.From(templateType);
            var all = Resources.FindObjectsOfTypeAll(il2cppType);

            if (all == null || all.Length == 0)
            {
                SdkLogger.Warning($"[TemplateIdLookup] No objects found for {templateType.Name}");
                _knownIds[templateType] = ids;
                return ids;
            }

            foreach (var obj in all)
            {
                if (obj == null) continue;
                var id = ReadTemplateId(obj.Pointer);
                if (!string.IsNullOrEmpty(id))
                    ids.Add(id);
            }

            SdkLogger.Msg($"[TemplateIdLookup] {templateType.Name}: {ids.Count} m_ID(s) found");
            SdkLogger.Msg($"  IDs: {string.Join(", ", ids)}");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[TemplateIdLookup] GetKnownIds({templateType.Name}): {ex.Message}");
        }

        _knownIds[templateType] = ids;
        FlushCacheToDisk();
        return ids;
    }

    public static bool HasId(Type templateType, string id) =>
        GetKnownIds(templateType).Contains(id);

    private static string ReadTemplateId(IntPtr objectPtr)
    {
        if (objectPtr == IntPtr.Zero) return null;

        if (!_templateIdOffset.HasValue)
        {
            var klass = IL2CPP.il2cpp_object_get_class(objectPtr);
            var field = FindField(klass, "m_ID");
            if (field == IntPtr.Zero)
            {
                SdkLogger.Warning("[TemplateIdLookup] m_ID field not found");
                return null;
            }

            var offset = IL2CPP.il2cpp_field_get_offset(field);
            if (offset != 0x68)
                SdkLogger.Warning($"[TemplateIdLookup] m_ID offset 0x{offset:X}, expected 0x68 — game may have updated");
            else
                SdkLogger.Msg($"[TemplateIdLookup] m_ID offset confirmed: 0x{offset:X}");

            _templateIdOffset = offset;
        }

        if (_templateIdOffset == 0) return null;

        var strPtr = Marshal.ReadIntPtr(objectPtr + (int)_templateIdOffset.Value);
        return strPtr != IntPtr.Zero ? IL2CPP.Il2CppStringToManaged(strPtr) : null;
    }

    private static IntPtr FindField(IntPtr klass, string fieldName)
    {
        var search = klass;
        while (search != IntPtr.Zero)
        {
            var field = IL2CPP.il2cpp_class_get_field_from_name(search, fieldName);
            if (field != IntPtr.Zero) return field;
            search = IL2CPP.il2cpp_class_get_parent(search);
        }
        return IntPtr.Zero;
    }

    private static string GetGameVersion()
    {
        try { return Application.version; }
        catch { }

        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm != null)
                return File.GetLastWriteTimeUtc(csharpAsm.Location).ToString("yyyyMMddHHmmss");
        }
        catch { }

        return "unknown";
    }

    private static void LoadCacheFromDisk()
    {
        if (!File.Exists(CachePath))
        {
            SdkLogger.Msg("[TemplateIdLookup] No cache file found, will build on first request");
            return;
        }

        try
        {
            var cached = JsonConvert.DeserializeObject<TemplateIdCache>(File.ReadAllText(CachePath));

            if (cached?.GameVersion != _gameVersion)
            {
                SdkLogger.Msg($"[TemplateIdLookup] Cache version mismatch ({cached?.GameVersion} != {_gameVersion}), rebuilding");
                return;
            }

            foreach (var kvp in cached.Types)
            {
                var type = _gameAssembly?.GetType(kvp.Key)
                    ?? AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.FullName == kvp.Key || t.Name == kvp.Key);

                if (type != null)
                    _knownIds[type] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
            }

            SdkLogger.Msg($"[TemplateIdLookup] Loaded cache: {cached.Types.Count} type(s), version {_gameVersion}");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[TemplateIdLookup] Failed to load cache: {ex.Message}");
        }
    }

    private static void FlushCacheToDisk()
    {
        SdkLogger.Msg($"[TemplateIdLookup] Flushing cache to: {CachePath}");
        try
        {
            var toWrite = new TemplateIdCache
            {
                GameVersion = _gameVersion,
                Types = _knownIds.ToDictionary(
                    kvp => kvp.Key.FullName ?? kvp.Key.Name,
                    kvp => kvp.Value.ToList())
            };

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonConvert.SerializeObject(toWrite, Formatting.Indented));
            SdkLogger.Msg($"[TemplateIdLookup] Cache written: {_knownIds.Count} type(s)");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[TemplateIdLookup] Failed to write cache: {ex.Message}");
        }
    }
}