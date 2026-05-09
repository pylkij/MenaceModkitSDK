using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

using MelonLoader;
using Menace.SDK;
using Menace.SDK.Internal;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Menace.ModpackLoader;

// IL2CPP reflection fallback for template patches.
public partial class ModpackLoaderMod
{
    private static readonly MethodInfo TryCastMethod = typeof(Il2CppObjectBase).GetMethod("TryCast");

    private static readonly HashSet<string> ReadOnlyProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pointer", "ObjectClass", "WasCollected", "m_CachedPtr",
        "name", "m_ID", "hideFlags", "serializationData"
    };

    private static readonly HashSet<string> TranslatedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "DisplayTitle", "DisplayShortName", "DisplayDescription",
        "HasIcon", "IconAssetName"
    };

    private readonly Dictionary<Type, Dictionary<string, UnityEngine.Object>> _nameLookupCache = new();
    private readonly List<Sprite> _runtimeSprites = new();
    private readonly List<Texture2D> _runtimeTextures = new();

    public void InvalidateNameLookupCache()
    {
        if (_nameLookupCache.Count > 0)
        {
            SdkLogger.Msg($"  Invalidating name lookup cache ({_nameLookupCache.Count} type(s))");
            _nameLookupCache.Clear();
        }
    }

    private static int AddAssetsToLookup(Dictionary<string, UnityEngine.Object> lookup, IEnumerable<UnityEngine.Object> assets)
    {
        var addedCount = 0;
        foreach (var asset in assets)
        {
            if (asset != null && !string.IsNullOrEmpty(asset.name) && !lookup.ContainsKey(asset.name))
            {
                lookup[asset.name] = asset;
                addedCount++;
            }
        }
        return addedCount;
    }

    private enum CollectionKind { None, StructArray, ReferenceArray, Il2CppList, ManagedArray }

    private static CollectionKind ClassifyCollectionType(Type propType, out Type elementType)
    {
        elementType = null;

        if (propType.IsGenericType)
        {
            var genName = propType.GetGenericTypeDefinition().Name;
            var args = propType.GetGenericArguments();

            if (genName.StartsWith("Il2CppStructArray"))
            {
                elementType = args[0];
                return CollectionKind.StructArray;
            }

            if (genName.StartsWith("Il2CppReferenceArray"))
            {
                elementType = args[0];
                return CollectionKind.ReferenceArray;
            }

            if (genName.Contains("List"))
            {
                var isIl2Cpp = propType.FullName?.Contains("Il2Cpp") == true
                               || IsIl2CppType(propType);
                if (isIl2Cpp)
                {
                    elementType = args[0];
                    return CollectionKind.Il2CppList;
                }
            }
        }

        if (propType.Name == "Il2CppStringArray")
        {
            elementType = typeof(string);
            return CollectionKind.ReferenceArray;
        }

        if (propType.IsArray)
        {
            elementType = propType.GetElementType();
            return CollectionKind.ManagedArray;
        }

        var baseType = propType.BaseType;
        while (baseType != null && baseType != typeof(object) && baseType != typeof(Il2CppObjectBase))
        {
            if (baseType.IsGenericType)
            {
                var baseName = baseType.GetGenericTypeDefinition().Name;
                var baseArgs = baseType.GetGenericArguments();

                if (baseName.StartsWith("Il2CppStructArray"))
                {
                    elementType = baseArgs[0];
                    return CollectionKind.StructArray;
                }
                if (baseName.StartsWith("Il2CppReferenceArray"))
                {
                    elementType = baseArgs[0];
                    return CollectionKind.ReferenceArray;
                }
            }
            baseType = baseType.BaseType;
        }

        return CollectionKind.None;
    }

    private static bool IsIl2CppType(Type type)
    {
        var current = type;
        while (current != null)
        {
            if (current == typeof(Il2CppObjectBase))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static bool IsLocalizationType(Type type)
    {
        if (type == null) return false;

        var name = type.Name;
        if (name == "LocalizedLine" || name == "LocalizedMultiLine" || name == "BaseLocalizedString")
            return true;

        var current = type.BaseType;
        while (current != null && current != typeof(object) && current != typeof(Il2CppObjectBase))
        {
            if (current.Name == "BaseLocalizedString")
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static readonly HashSet<string> KnownLocalizationFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Title", "ShortName", "Description", "Text", "DisplayText",
        "TooltipText", "Label", "Message", "Hint"
    };

    private static bool IsLikelyLocalizationField(string fieldName)
    {
        return KnownLocalizationFieldNames.Contains(fieldName);
    }

    private static bool IsRuntimeLocalizationType(object value)
    {
        if (value == null) return false;

        if (value is Il2CppObjectBase il2cppObj)
        {
            try
            {
                var ptr = il2cppObj.Pointer;
                if (ptr == IntPtr.Zero) return false;

                var klassPtr = IL2CPP.il2cpp_object_get_class(ptr);
                if (klassPtr == IntPtr.Zero) return false;

                var namePtr = IL2CPP.il2cpp_class_get_name(klassPtr);
                if (namePtr == IntPtr.Zero) return false;

                var className = Marshal.PtrToStringAnsi(namePtr);
                return className == "LocalizedLine" || className == "LocalizedMultiLine" ||
                       className == "BaseLocalizedString";
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    // Memory layout offsets for BaseLocalizedString (from reverse engineering)
    private const int LOC_CATEGORY_OFFSET = 0x10;           // int LocaCategory
    private const int LOC_KEY_PART1_OFFSET = 0x18;          // string m_KeyPart1
    private const int LOC_KEY_PART2_OFFSET = 0x20;          // string m_KeyPart2
    private const int LOC_CATEGORY_NAME_OFFSET = 0x28;      // string m_CategoryName
    private const int LOC_IDENTIFIER_OFFSET = 0x30;         // string m_Identifier
    private const int LOC_DEFAULT_TRANSLATION_OFFSET = 0x38; // string m_DefaultTranslation
    private const int LOC_HAS_PLACEHOLDERS_OFFSET = 0x40;   // bool hasPlaceholders

    private static IntPtr _localizedLineClass = IntPtr.Zero;
    private static IntPtr _localizedMultiLineClass = IntPtr.Zero;

    private static IntPtr GetLocalizedLineClass()
    {
        if (_localizedLineClass != IntPtr.Zero)
            return _localizedLineClass;

        _localizedLineClass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "Menace.Tools", "LocalizedLine");
        return _localizedLineClass;
    }

    private static IntPtr GetLocalizedMultiLineClass()
    {
        if (_localizedMultiLineClass != IntPtr.Zero)
            return _localizedMultiLineClass;

        _localizedMultiLineClass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "Menace.Tools", "LocalizedMultiLine");
        return _localizedMultiLineClass;
    }

    private IntPtr CreateLocalizedObject(IntPtr existingLocPtr, string value)
    {
        try
        {
            IntPtr newClass;
            byte hasPlaceholders = 0;

            if (existingLocPtr != IntPtr.Zero)
            {
                var existingClass = IL2CPP.il2cpp_object_get_class(existingLocPtr);
                if (existingClass == IntPtr.Zero)
                {
                    SdkLogger.Warning("    CreateLocalizedObject: could not get class of existing object, defaulting to LocalizedLine");
                    newClass = GetLocalizedLineClass();
                }
                else
                {
                    var classNamePtr = IL2CPP.il2cpp_class_get_name(existingClass);
                    var className = classNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(classNamePtr) : "";

                    if (className == "LocalizedMultiLine")
                    {
                        newClass = GetLocalizedMultiLineClass();
                    }
                    else
                    {
                        newClass = GetLocalizedLineClass();
                    }

                    hasPlaceholders = Marshal.ReadByte(existingLocPtr + LOC_HAS_PLACEHOLDERS_OFFSET);
                }
            }
            else
            {
                newClass = GetLocalizedLineClass();
            }

            if (newClass == IntPtr.Zero)
            {
                SdkLogger.Warning("    CreateLocalizedObject: could not find LocalizedLine class");
                return IntPtr.Zero;
            }

            var newObj = IL2CPP.il2cpp_object_new(newClass);
            if (newObj == IntPtr.Zero)
            {
                SdkLogger.Warning("    CreateLocalizedObject: il2cpp_object_new returned null");
                return IntPtr.Zero;
            }

            // Clear lookup keys so the game uses m_DefaultTranslation instead of cached localized text.
            Marshal.WriteInt32(newObj + LOC_CATEGORY_OFFSET, 0);
            Marshal.WriteIntPtr(newObj + LOC_KEY_PART1_OFFSET, IntPtr.Zero);
            Marshal.WriteIntPtr(newObj + LOC_KEY_PART2_OFFSET, IntPtr.Zero);
            Marshal.WriteIntPtr(newObj + LOC_CATEGORY_NAME_OFFSET, IntPtr.Zero);
            Marshal.WriteIntPtr(newObj + LOC_IDENTIFIER_OFFSET, IntPtr.Zero);

            IntPtr il2cppStr = IntPtr.Zero;
            if (!string.IsNullOrEmpty(value))
            {
                il2cppStr = IL2CPP.ManagedStringToIl2Cpp(value);
            }
            Marshal.WriteIntPtr(newObj + LOC_DEFAULT_TRANSLATION_OFFSET, il2cppStr);

            Marshal.WriteByte(newObj + LOC_HAS_PLACEHOLDERS_OFFSET, hasPlaceholders);

            return newObj;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    CreateLocalizedObject failed: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    private bool WriteLocalizedFieldDirect(Il2CppObjectBase templateObj, string fieldName, string value)
    {
        try
        {
            var templatePtr = templateObj.Pointer;
            if (templatePtr == IntPtr.Zero)
                return false;

            var klassPtr = IL2CPP.il2cpp_object_get_class(templatePtr);
            if (klassPtr == IntPtr.Zero)
                return false;

            var fieldOffset = OffsetCache.GetOrResolve(klassPtr, fieldName);
            if (fieldOffset == 0)
            {
                SdkLogger.Warning($"    {fieldName}: could not find field offset");
                return false;
            }

            var existingLocPtr = Marshal.ReadIntPtr(templatePtr + (int)fieldOffset);

            if (existingLocPtr != IntPtr.Zero && existingLocPtr.ToInt64() < 0x10000)
            {
                SdkLogger.Warning($"    {fieldName}: invalid localization pointer, treating as null");
                existingLocPtr = IntPtr.Zero;
            }

            var newLocPtr = CreateLocalizedObject(existingLocPtr, value);
            if (newLocPtr == IntPtr.Zero)
            {
                SdkLogger.Warning($"    {fieldName}: failed to create new localization object");
                return false;
            }

            Marshal.WriteIntPtr(templatePtr + (int)fieldOffset, newLocPtr);

            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    WriteLocalizedFieldDirect({fieldName}) failed: {ex.Message}");
            return false;
        }
    }

    private bool WriteLocalizedFieldViaReflection(object parent, PropertyInfo prop, FieldInfo field, string fieldName, string value)
    {
        try
        {
            object existingLoc = prop != null ? prop.GetValue(parent) : field?.GetValue(parent);
            IntPtr existingPtr = IntPtr.Zero;
            Type locType = null;

            if (existingLoc is Il2CppObjectBase il2cppLoc)
            {
                existingPtr = il2cppLoc.Pointer;
                locType = existingLoc.GetType();
            }

            if (locType == null)
            {
                locType = prop?.PropertyType ?? field?.FieldType;
                if (locType == null)
                {
                    SdkLogger.Warning($"    {fieldName}: could not determine localization type");
                    return false;
                }
            }

            var newLocPtr = CreateLocalizedObject(existingPtr, value);
            if (newLocPtr == IntPtr.Zero)
            {
                SdkLogger.Warning($"    {fieldName}: failed to create new localization object");
                return false;
            }

            var wrappedNew = Activator.CreateInstance(locType, newLocPtr);
            if (wrappedNew == null)
            {
                SdkLogger.Warning($"    {fieldName}: failed to wrap new localization object");
                return false;
            }

            if (prop != null && prop.CanWrite)
                prop.SetValue(parent, wrappedNew);
            else if (field != null)
                field.SetValue(parent, wrappedNew);
            else
            {
                SdkLogger.Warning($"    {fieldName}: no writable property or field");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    WriteLocalizedFieldViaReflection({fieldName}) failed: {ex.Message}");
            return false;
        }
    }

    private Dictionary<string, UnityEngine.Object> BuildNameLookup(Type elementType)
    {
        if (_nameLookupCache.TryGetValue(elementType, out var cached))
            return cached;

        var lookup = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (elementType == typeof(Sprite))
            {
                var customSpriteNames = AssetReplacer.GetCustomSpriteNames();
                foreach (var name in customSpriteNames)
                {
                    var sprite = AssetReplacer.GetCustomSprite(name);
                    if (sprite != null)
                        lookup[name] = sprite;
                }
                if (customSpriteNames.Count > 0)
                    SdkLogger.Msg($"    Added {customSpriteNames.Count} custom sprite(s) to lookup");
            }

            try
            {
                var simpleTypeName = elementType.Name;
                string il2cppTypeName;
                try
                {
                    il2cppTypeName = Il2CppType.From(elementType)?.Name ?? simpleTypeName;
                }
                catch
                {
                    il2cppTypeName = simpleTypeName;
                }

                var typeNamesToTry = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { il2cppTypeName, simpleTypeName };

                var compiledAssetCount = 0;
                var bundleAssetCount = 0;
                foreach (var typeName in typeNamesToTry)
                {
                    compiledAssetCount += AddAssetsToLookup(lookup, CompiledAssetLoader.GetAssetsByType(typeName));
                    bundleAssetCount += AddAssetsToLookup(lookup, BundleLoader.GetAssetsByType(typeName));
                }
                if (compiledAssetCount > 0)
                    SdkLogger.Msg($"    Added {compiledAssetCount} compiled {simpleTypeName}(s) to lookup");
                if (bundleAssetCount > 0)
                    SdkLogger.Msg($"    Added {bundleAssetCount} runtime {simpleTypeName}(s) to lookup");

                if (elementType == typeof(Sprite))
                {
                    int runtimeSpriteCount = 0;

                    var modsPath = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
                    var texturesDir = Path.Combine(modsPath, "compiled", "textures");

                    if (Directory.Exists(texturesDir))
                    {
                        var pngFiles = Directory.GetFiles(texturesDir, "*.png");
                        SdkLogger.Msg($"    Found {pngFiles.Length} PNG file(s) in compiled/textures");

                        foreach (var pngPath in pngFiles)
                        {
                            var textureName = Path.GetFileNameWithoutExtension(pngPath);

                            if (lookup.ContainsKey(textureName)) continue;

                            try
                            {
                                var bytes = File.ReadAllBytes(pngPath);
                                var tex = new Texture2D(2, 2);
                                var il2cppBytes = new Il2CppStructArray<byte>(bytes);

                                if (ImageConversion.LoadImage(tex, il2cppBytes))
                                {
                                    tex.name = textureName;

                                    var rect = new Rect(0, 0, tex.width, tex.height);
                                    var pivot = new Vector2(0.5f, 0.5f);
                                    var sprite = Sprite.Create(tex, rect, pivot, 100f);

                                    if (sprite != null)
                                    {
                                        sprite.name = textureName;
                                        lookup[textureName] = sprite;

                                        _runtimeSprites.Add(sprite);
                                        _runtimeTextures.Add(tex);
                                        runtimeSpriteCount++;

                                        SdkLogger.Msg($"      Loaded sprite: '{textureName}' ({tex.width}x{tex.height})");
                                    }
                                }
                                else
                                {
                                    SdkLogger.Warning($"    Failed to load texture from '{pngPath}'");
                                }
                            }
                            catch (Exception ex)
                            {
                                SdkLogger.Warning($"    Failed to create sprite for '{textureName}': {ex.Message}");
                            }
                        }
                    }

                    if (runtimeSpriteCount > 0)
                        SdkLogger.Msg($"    Created {runtimeSpriteCount} sprite(s) from PNG files");
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"    Runtime asset lookup failed for {elementType.Name}: {ex.Message}");
            }

            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (gameAssembly != null)
                EnsureTemplatesLoaded(gameAssembly, elementType);

            var il2cppType = Il2CppType.From(elementType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects != null)
            {
                foreach (var obj in objects)
                {
                    if (obj != null && !string.IsNullOrEmpty(obj.name))
                    {
                        if (!lookup.ContainsKey(obj.name))
                            lookup[obj.name] = obj;
                    }
                }
            }

            SdkLogger.Msg($"    Built name lookup for {elementType.Name}: {lookup.Count} entries");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    Failed to build name lookup for {elementType.Name}: {ex.Message}");
        }

        _nameLookupCache[elementType] = lookup;
        return lookup;
    }

    private void ApplyTemplateModifications(UnityEngine.Object obj, Type templateType, Dictionary<string, object> modifications)
    {
        object castObj;
        try
        {
            var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
            castObj = genericTryCast.Invoke(obj, null);
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"    TryCast failed for {obj.name}: {ex.Message}");
            return;
        }

        if (castObj == null)
        {
            SdkLogger.Error($"    TryCast returned null for {obj.name}");
            return;
        }

        var propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        var currentType = templateType;
        while (currentType != null && currentType.Name != "Object" &&
               currentType != typeof(Il2CppObjectBase))
        {
            var props = currentType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var prop in props)
            {
                if (prop.CanWrite && prop.CanRead && !propertyMap.ContainsKey(prop.Name))
                    propertyMap[prop.Name] = prop;
            }
            currentType = currentType.BaseType;
        }

        int appliedCount = 0;
        foreach (var (fieldName, rawValue) in modifications)
        {
            if (ReadOnlyProperties.Contains(fieldName))
                continue;

            if (TranslatedFields.Contains(fieldName))
            {
                SdkLogger.Msg($"    {obj.name}: skipping {fieldName} (translated field - edit Title/ShortName/Description instead)");
                continue;
            }

            var dotIdx = fieldName.IndexOf('.');
            if (dotIdx > 0)
            {
                var parentFieldName = fieldName[..dotIdx];
                var childFieldName = fieldName[(dotIdx + 1)..];

                if (!propertyMap.TryGetValue(parentFieldName, out var parentProp))
                {
                    SdkLogger.Warning($"    {obj.name}: parent property '{parentFieldName}' not found on {templateType.Name}");
                    continue;
                }

                try
                {
                    var parentObj = parentProp.GetValue(castObj);
                    if (parentObj == null)
                    {
                        SdkLogger.Warning($"    {obj.name}.{parentFieldName} is null, cannot set '{childFieldName}'");
                        continue;
                    }

                    var childProp = parentObj.GetType().GetProperty(childFieldName,
                        BindingFlags.Public | BindingFlags.Instance);

                    FieldInfo childField = null;
                    if (childProp == null || !childProp.CanWrite)
                    {
                        childField = parentObj.GetType().GetField(childFieldName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (childField == null)
                        {
                            SdkLogger.Warning($"    {obj.name}: property/field '{childFieldName}' not found on {parentObj.GetType().Name}");
                            continue;
                        }
                    }

                    var childType = childProp?.PropertyType ?? childField.FieldType;

                    if (rawValue is JArray nestedJArray)
                    {
                        var kind = ClassifyCollectionType(childType, out _);
                        if (kind != CollectionKind.None)
                        {
                            if (childProp != null && TryApplyCollectionValue(parentObj, childProp, nestedJArray))
                                appliedCount++;
                            continue;
                        }
                    }

                    if (rawValue is JObject nestedJObj)
                    {
                        var nestedKind = ClassifyCollectionType(childType, out var nestedElType);
                        if (nestedKind == CollectionKind.Il2CppList && nestedElType != null)
                        {
                            if (childProp != null && TryApplyIncrementalList(parentObj, childProp, nestedJObj, nestedElType))
                                appliedCount++;
                            continue;
                        }
                    }

                    var childCurrentValue = childProp?.CanRead == true ? childProp.GetValue(parentObj) : childField?.GetValue(parentObj);
                    bool isChildLocalization = IsLocalizationType(childType) ||
                                               IsRuntimeLocalizationType(childCurrentValue) ||
                                               (IsLikelyLocalizationField(childFieldName) &&
                                                childCurrentValue is Il2CppObjectBase &&
                                                rawValue is JValue jVal && jVal.Type == JTokenType.String);

                    if (isChildLocalization)
                    {
                        var stringValue = rawValue is JToken jt ? jt.Value<string>() : rawValue?.ToString();
                        bool success = false;

                        if (parentObj is Il2CppObjectBase il2cppParent)
                        {
                            success = WriteLocalizedFieldDirect(il2cppParent, childFieldName, stringValue);
                        }
                        else
                        {
                            success = WriteLocalizedFieldViaReflection(parentObj, childProp, childField, childFieldName, stringValue);
                        }

                        if (success)
                        {
                            var detectionMethod = IsLocalizationType(childType) ? "type" :
                                                  IsRuntimeLocalizationType(childCurrentValue) ? "runtime" : "name";
                            SdkLogger.Msg($"    {obj.name}.{fieldName}: set localized text (detected by {detectionMethod})");
                            appliedCount++;
                            continue;
                        }
                        SdkLogger.Msg($"    {obj.name}.{fieldName}: localization write failed, trying normal assignment");
                    }

                    var nestedConverted = ConvertToPropertyType(rawValue, childType);

                    if (childField != null && parentProp.PropertyType.IsValueType)
                    {
                        childField.SetValue(parentObj, nestedConverted);
                        parentProp.SetValue(castObj, parentObj);
                    }
                    else if (childField != null)
                    {
                        childField.SetValue(parentObj, nestedConverted);
                    }
                    else
                    {
                        childProp.SetValue(parentObj, nestedConverted);
                    }
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    SdkLogger.Error($"    {obj.name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
                }
                continue;
            }

            if (!propertyMap.TryGetValue(fieldName, out var prop))
            {
                SdkLogger.Warning($"    {obj.name}: property '{fieldName}' not found on {templateType.Name}");
                continue;
            }

            try
            {
                if (rawValue is JArray jArray)
                {
                    var kind = ClassifyCollectionType(prop.PropertyType, out _);
                    if (kind != CollectionKind.None)
                    {
                        if (TryApplyCollectionValue(castObj, prop, jArray))
                            appliedCount++;
                        continue;
                    }
                }

                if (rawValue is JObject jObj)
                {
                    var collKind = ClassifyCollectionType(prop.PropertyType, out var elType);
                    if (elType != null)
                    {
                        if (collKind == CollectionKind.Il2CppList)
                        {
                            if (TryApplyIncrementalList(castObj, prop, jObj, elType))
                                appliedCount++;
                            continue;
                        }
                        else if (collKind == CollectionKind.StructArray || collKind == CollectionKind.ReferenceArray)
                        {
                            if (TryApplyIncrementalArray(castObj, prop, jObj, elType, collKind))
                                appliedCount++;
                            continue;
                        }
                    }
                }

                var topLevelCurrentValue = prop.CanRead ? prop.GetValue(castObj) : null;
                bool isTopLevelLocalization = IsLocalizationType(prop.PropertyType) ||
                                              IsRuntimeLocalizationType(topLevelCurrentValue) ||
                                              (IsLikelyLocalizationField(fieldName) &&
                                               topLevelCurrentValue is Il2CppObjectBase &&
                                               rawValue is JValue jVal && jVal.Type == JTokenType.String);

                if (isTopLevelLocalization)
                {
                    var stringValue = rawValue is JToken jt ? jt.Value<string>() : rawValue?.ToString();
                    if (castObj is Il2CppObjectBase il2cppCastObj &&
                        WriteLocalizedFieldDirect(il2cppCastObj, fieldName, stringValue))
                    {
                        var detectionMethod = IsLocalizationType(prop.PropertyType) ? "type" :
                                              IsRuntimeLocalizationType(topLevelCurrentValue) ? "runtime" : "name";
                        SdkLogger.Msg($"    {obj.name}.{fieldName}: set localized text (detected by {detectionMethod})");
                        appliedCount++;
                        continue;
                    }
                    SdkLogger.Msg($"    {obj.name}.{fieldName}: localization write failed, trying normal assignment");
                }

                var convertedValue = ConvertToPropertyType(rawValue, prop.PropertyType);
                prop.SetValue(castObj, convertedValue);
                appliedCount++;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                SdkLogger.Error($"    {obj.name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
            }
        }

        if (appliedCount > 0)
        {
            SdkLogger.Msg($"    {obj.name}: set {appliedCount}/{modifications.Count} fields");
        }
    }

    private bool TryApplyCollectionValue(object castObj, PropertyInfo prop, JArray jArray)
    {
        var kind = ClassifyCollectionType(prop.PropertyType, out var elementType);
        if (kind == CollectionKind.None || elementType == null)
            return false;

        switch (kind)
        {
            case CollectionKind.StructArray:
                return ApplyStructArray(castObj, prop, jArray, elementType);
            case CollectionKind.ReferenceArray:
                return ApplyReferenceArray(castObj, prop, jArray, elementType);
            case CollectionKind.Il2CppList:
                return ApplyIl2CppList(castObj, prop, jArray, elementType);
            case CollectionKind.ManagedArray:
                return ApplyManagedArray(castObj, prop, jArray, elementType);
            default:
                return false;
        }
    }

    private bool ApplyStructArray(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var arrayType = prop.PropertyType;
        var array = Activator.CreateInstance(arrayType, new object[] { jArray.Count });

        var indexer = arrayType.GetProperty("Item");
        if (indexer == null)
        {
            SdkLogger.Warning($"    {prop.Name}: no indexer found on {arrayType.Name}");
            return false;
        }

        for (int i = 0; i < jArray.Count; i++)
        {
            var converted = ConvertJTokenToType(jArray[i], elementType);
            indexer.SetValue(array, converted, new object[] { i });
        }

        prop.SetValue(castObj, array);
        SdkLogger.Msg($"    {prop.Name}: set StructArray<{elementType.Name}>[{jArray.Count}]");
        return true;
    }

    private bool ApplyReferenceArray(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var arrayType = prop.PropertyType;

        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
        {
            var lookup = BuildNameLookup(elementType);
            var array = Activator.CreateInstance(arrayType, new object[] { jArray.Count });
            var indexer = arrayType.GetProperty("Item");
            if (indexer == null) return false;

            for (int i = 0; i < jArray.Count; i++)
            {
                var name = jArray[i].Value<string>();
                if (name != null && lookup.TryGetValue(name, out var resolved))
                {
                    var castMethod = TryCastMethod.MakeGenericMethod(elementType);
                    var castElement = castMethod.Invoke(resolved, null);
                    indexer.SetValue(array, castElement, new object[] { i });
                }
                else
                {
                    SdkLogger.Warning($"    {prop.Name}[{i}]: could not resolve '{name}'");
                }
            }

            prop.SetValue(castObj, array);
            SdkLogger.Msg($"    {prop.Name}: set ReferenceArray<{elementType.Name}>[{jArray.Count}]");
            return true;
        }

        if (elementType == typeof(string) || elementType.FullName == "Il2CppSystem.String")
        {
            var array = Activator.CreateInstance(arrayType, new object[] { jArray.Count });
            var indexer = arrayType.GetProperty("Item");
            if (indexer == null) return false;

            for (int i = 0; i < jArray.Count; i++)
                indexer.SetValue(array, jArray[i].Value<string>(), new object[] { i });

            prop.SetValue(castObj, array);
            SdkLogger.Msg($"    {prop.Name}: set string array[{jArray.Count}]");
            return true;
        }

        var refArray = Activator.CreateInstance(arrayType, new object[] { jArray.Count });
        var refIndexer = arrayType.GetProperty("Item");
        if (refIndexer == null) return false;

        for (int i = 0; i < jArray.Count; i++)
        {
            var converted = ConvertJTokenToType(jArray[i], elementType);
            refIndexer.SetValue(refArray, converted, new object[] { i });
        }

        prop.SetValue(castObj, refArray);
        SdkLogger.Msg($"    {prop.Name}: set ReferenceArray<{elementType.Name}>[{jArray.Count}]");
        return true;
    }

    private bool ApplyIl2CppList(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var list = prop.GetValue(castObj);
        if (list == null)
        {
            try
            {
                list = Activator.CreateInstance(prop.PropertyType);
                prop.SetValue(castObj, list);
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"    {prop.Name}: IL2CPP List is null and construction failed: {ex.Message}");
                return false;
            }
        }

        var listType = list.GetType();

        var clearMethod = listType.GetMethod("Clear");
        if (clearMethod == null)
        {
            SdkLogger.Warning($"    {prop.Name}: List has no Clear method");
            return false;
        }

        var addMethod = listType.GetMethod("Add");
        if (addMethod == null)
        {
            SdkLogger.Warning($"    {prop.Name}: List has no Add method");
            return false;
        }

        clearMethod.Invoke(list, null);

        bool hasEmbeddedObjects = jArray.Any(item => item is JObject);

        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType) && !hasEmbeddedObjects)
        {
            var lookup = BuildNameLookup(elementType);

            foreach (var item in jArray)
            {
                var name = item.Value<string>();
                if (name != null && lookup.TryGetValue(name, out var resolved))
                {
                    var castMethod = TryCastMethod.MakeGenericMethod(elementType);
                    var castElement = castMethod.Invoke(resolved, null);
                    addMethod.Invoke(list, new[] { castElement });
                }
                else
                {
                    SdkLogger.Warning($"    {prop.Name}: could not resolve '{name}' for List<{elementType.Name}>");
                }
            }
        }
        else
        {
            int successCount = 0;
            for (int i = 0; i < jArray.Count; i++)
            {
                var item = jArray[i];
                try
                {
                    var converted = ConvertJTokenToType(item, elementType);
                    if (converted == null)
                    {
                        var typeHint = item is JObject jObj && jObj.TryGetValue("_type", out var typeToken)
                            ? typeToken.Value<string>() : "unknown";
                        SdkLogger.Warning($"    {prop.Name}[{i}]: conversion returned null (item type: {typeHint}, target: {elementType.Name})");
                        continue;
                    }
                    addMethod.Invoke(list, new[] { converted });
                    successCount++;
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    var typeHint = item is JObject jObj && jObj.TryGetValue("_type", out var typeToken)
                        ? typeToken.Value<string>() : "unknown";
                    SdkLogger.Error($"    {prop.Name}[{i}]: failed to add item (type: {typeHint}): {inner.GetType().Name}: {inner.Message}");
                }
            }
            SdkLogger.Msg($"    {prop.Name}: set List<{elementType.Name}> with {successCount}/{jArray.Count} elements");
        }

        return true;
    }

    private bool ApplyManagedArray(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var array = Array.CreateInstance(elementType, jArray.Count);

        for (int i = 0; i < jArray.Count; i++)
        {
            var converted = ConvertJTokenToType(jArray[i], elementType);
            array.SetValue(converted, i);
        }

        prop.SetValue(castObj, array);
        SdkLogger.Msg($"    {prop.Name}: set {elementType.Name}[{jArray.Count}]");
        return true;
    }

    private object ConvertToPropertyType(object value, Type targetType)
    {
        if (value == null)
            return null;

        if (value is JToken jToken)
        {
            return ConvertJTokenToType(jToken, targetType);
        }

        if (targetType.IsInstanceOfType(value))
            return value;

        if (targetType.IsEnum)
        {
            var intVal = Convert.ToInt32(value);
            return Enum.ToObject(targetType, intVal);
        }

        if (targetType == typeof(int)) return Convert.ToInt32(value);
        if (targetType == typeof(float)) return Convert.ToSingle(value);
        if (targetType == typeof(double)) return Convert.ToDouble(value);
        if (targetType == typeof(bool)) return Convert.ToBoolean(value);
        if (targetType == typeof(byte)) return Convert.ToByte(value);
        if (targetType == typeof(short)) return Convert.ToInt16(value);
        if (targetType == typeof(long)) return Convert.ToInt64(value);
        if (targetType == typeof(string)) return value.ToString();

        if (value is string strValue && IsIl2CppType(targetType))
        {
            return ResolveIl2CppReference(strValue, targetType);
        }

        if (targetType.IsValueType && !targetType.IsPrimitive && !targetType.IsEnum)
        {
            var structResult = TryCreateSimpleStruct(targetType, value);
            if (structResult != null)
                return structResult;
        }

        return Convert.ChangeType(value, targetType);
    }

    private object ConvertJTokenToType(JToken token, Type targetType)
    {
        if (token.Type == JTokenType.Null)
            return null;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, token.Value<int>());

        if (targetType == typeof(int)) return token.Value<int>();
        if (targetType == typeof(float)) return token.Value<float>();
        if (targetType == typeof(double)) return token.Value<double>();
        if (targetType == typeof(bool)) return token.Value<bool>();
        if (targetType == typeof(byte)) return token.Value<byte>();
        if (targetType == typeof(short)) return token.Value<short>();
        if (targetType == typeof(long)) return token.Value<long>();
        if (targetType == typeof(string)) return token.Value<string>();

        if (token.Type == JTokenType.String && IsIl2CppType(targetType))
        {
            var name = token.Value<string>();
            if (!string.IsNullOrEmpty(name))
                return ResolveIl2CppReference(name, targetType);
            return null;
        }

        if (token is JObject jObj && IsIl2CppType(targetType))
            return CreateIl2CppObject(targetType, jObj);

        if (targetType.IsValueType && !targetType.IsPrimitive && !targetType.IsEnum)
        {
            object primitiveValue = token.Type switch
            {
                JTokenType.Integer => token.Value<long>(),
                JTokenType.Float => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                _ => null
            };

            if (primitiveValue != null)
            {
                var structResult = TryCreateSimpleStruct(targetType, primitiveValue);
                if (structResult != null)
                    return structResult;
            }
        }

        return token.ToObject(targetType);
    }

    private object TryCreateSimpleStruct(Type structType, object primitiveValue)
    {
        try
        {
            var structInstance = Activator.CreateInstance(structType);
            if (structInstance == null)
                return null;

            var fields = structType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            string[] preferredNames = { "m_Supplies", "m_Value", "Value", "value", "_value", "m_Amount", "Amount" };

            FieldInfo targetField = null;

            foreach (var name in preferredNames)
            {
                targetField = fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (targetField != null)
                    break;
            }

            if (targetField == null && fields.Length == 1)
            {
                targetField = fields[0];
            }

            if (targetField == null)
            {
                var props = structType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && p.CanRead)
                    .ToArray();

                if (props.Length == 1)
                {
                    var prop = props[0];
                    var convertedValue = ConvertPrimitiveToType(primitiveValue, prop.PropertyType);
                    if (convertedValue != null)
                    {
                        prop.SetValue(structInstance, convertedValue);
                        return structInstance;
                    }
                }
                return null;
            }

            var fieldValue = ConvertPrimitiveToType(primitiveValue, targetField.FieldType);
            if (fieldValue == null)
                return null;

            targetField.SetValue(structInstance, fieldValue);
            return structInstance;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    TryCreateSimpleStruct({structType.Name}): {ex.Message}");
            return null;
        }
    }

    private static object ConvertPrimitiveToType(object value, Type targetType)
    {
        try
        {
            if (targetType == typeof(int)) return Convert.ToInt32(value);
            if (targetType == typeof(float)) return Convert.ToSingle(value);
            if (targetType == typeof(double)) return Convert.ToDouble(value);
            if (targetType == typeof(long)) return Convert.ToInt64(value);
            if (targetType == typeof(short)) return Convert.ToInt16(value);
            if (targetType == typeof(byte)) return Convert.ToByte(value);
            if (targetType == typeof(bool)) return Convert.ToBoolean(value);
            if (targetType == typeof(uint)) return Convert.ToUInt32(value);
            if (targetType == typeof(ulong)) return Convert.ToUInt64(value);
            if (targetType == typeof(ushort)) return Convert.ToUInt16(value);
            if (targetType == typeof(sbyte)) return Convert.ToSByte(value);

            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return null;
        }
    }

    private object ResolveIl2CppReference(string name, Type targetType)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
        {
            try
            {
                var lookup = BuildNameLookup(targetType);
                if (lookup.TryGetValue(name, out var resolved))
                {
                    SdkLogger.Msg($"    [Debug] Resolved '{name}' -> {targetType.Name} '{resolved.name}'");
                    var castMethod = TryCastMethod.MakeGenericMethod(targetType);
                    return castMethod.Invoke(resolved, null);
                }
                else
                {
                    if (targetType == typeof(Sprite))
                    {
                        SdkLogger.Warning($"    [Debug] Sprite lookup FAILED for '{name}' (lookup has {lookup.Count} entries)");
                    }
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Msg($"    [Debug] BuildNameLookup failed for {targetType.Name}: {ex.Message}");
            }
        }

        try
        {
            var obj = Activator.CreateInstance(targetType);
            if (obj != null)
            {
                var keyProp = targetType.GetProperty("Key") ??
                              targetType.GetProperty("Value") ??
                              targetType.GetProperty("key") ??
                              targetType.GetProperty("value") ??
                              targetType.GetProperty("Name") ??
                              targetType.GetProperty("Id");

                if (keyProp != null && keyProp.CanWrite)
                {
                    if (keyProp.PropertyType == typeof(string))
                    {
                        keyProp.SetValue(obj, name);
                        SdkLogger.Msg($"    Constructed {targetType.Name} with Key='{name}'");
                        return obj;
                    }
                }

                return obj;
            }
        }
        catch
        {
        }

        SdkLogger.Warning($"    Could not resolve '{name}' as {targetType.Name}");
        return null;
    }

    private static readonly Dictionary<string, Type> _polymorphicTypeCache = new(StringComparer.Ordinal);

    private object CreateIl2CppObject(Type targetType, JObject jObj, Type skipType = null)
    {
        Type actualType = targetType;
        string typeDiscriminator = null;

        if (jObj.TryGetValue("_type", out var typeToken))
        {
            typeDiscriminator = typeToken.Value<string>();
            if (!string.IsNullOrEmpty(typeDiscriminator))
            {
                actualType = ResolvePolymorphicType(targetType, typeDiscriminator);
                if (actualType == null)
                {
                    SdkLogger.Warning($"    Failed to resolve polymorphic type '{typeDiscriminator}' (base: {targetType.Name})");
                    return null;
                }
            }
        }

        object newObj;
        try
        {
            newObj = Activator.CreateInstance(actualType);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    Failed to construct {actualType.Name}: {ex.Message}");
            return null;
        }

        var propsToApply = new JObject();
        foreach (var kvp in jObj)
        {
            if (kvp.Key != "_type")
                propsToApply[kvp.Key] = kvp.Value;
        }

        ApplyFieldOverrides(newObj, propsToApply, skipType);
        return newObj;
    }

    private Type ResolvePolymorphicType(Type baseType, string typeDiscriminator)
    {
        var cacheKey = $"{baseType.FullName}:{typeDiscriminator}";
        if (_polymorphicTypeCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            SdkLogger.Warning($"    ResolvePolymorphicType: Assembly-CSharp not found");
            return null;
        }

        var candidateNames = new List<string>
        {
            $"{typeDiscriminator}Handler",
            typeDiscriminator,
            $"{typeDiscriminator}EventHandler",
            $"{typeDiscriminator}Template",
            $"Skill{typeDiscriminator}Handler",
            $"Skill{typeDiscriminator}",
        };

        var baseNamespace = baseType.Namespace;
        if (!string.IsNullOrEmpty(baseNamespace))
        {
            foreach (var name in candidateNames.ToArray())
            {
                candidateNames.Add($"{baseNamespace}.{name}");
            }
        }

        Type resolvedType = null;
        foreach (var candidate in candidateNames)
        {
            resolvedType = gameAssembly.GetType(candidate, throwOnError: false, ignoreCase: true);
            if (resolvedType != null && !resolvedType.IsAbstract)
            {
                if (baseType.IsAssignableFrom(resolvedType) || IsCompatibleHandlerType(resolvedType, baseType))
                {
                    SdkLogger.Msg($"    Resolved polymorphic type: '{typeDiscriminator}' → {resolvedType.FullName}");
                    _polymorphicTypeCache[cacheKey] = resolvedType;
                    return resolvedType;
                }
            }
        }

        try
        {
            var allTypes = gameAssembly.GetTypes();
            foreach (var type in allTypes)
            {
                if (type.IsAbstract)
                    continue;

                var typeName = type.Name;
                bool nameMatches = typeName.Equals($"{typeDiscriminator}Handler", StringComparison.OrdinalIgnoreCase) ||
                                   typeName.Equals(typeDiscriminator, StringComparison.OrdinalIgnoreCase);

                if (nameMatches && (baseType.IsAssignableFrom(type) || IsCompatibleHandlerType(type, baseType)))
                {
                    SdkLogger.Msg($"    Resolved polymorphic type (fallback): '{typeDiscriminator}' → {type.FullName}");
                    _polymorphicTypeCache[cacheKey] = type;
                    return type;
                }
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    ResolvePolymorphicType fallback failed: {ex.Message}");
        }

        SdkLogger.Warning($"    ResolvePolymorphicType: could not find type for '{typeDiscriminator}' (base: {baseType.Name})");
        _polymorphicTypeCache[cacheKey] = null;
        return null;
    }

    private static bool IsCompatibleHandlerType(Type candidateType, Type expectedBaseType)
    {
        var current = candidateType;
        while (current != null && current != typeof(object))
        {
            var name = current.Name;
            if (name == "SkillEventHandlerTemplate" ||
                name == "SkillEventHandler" ||
                name == "TileEffectHandler" ||
                name == "SerializedScriptableObject")
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private void ApplyFieldOverrides(object target, JObject overrides, Type skipType = null)
    {
        var targetType = target.GetType();

        var propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        var currentType = targetType;
        while (currentType != null && currentType.Name != "Object" &&
               currentType != typeof(Il2CppObjectBase))
        {
            var props = currentType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var prop in props)
            {
                if (prop.CanWrite && prop.CanRead && !propertyMap.ContainsKey(prop.Name))
                    propertyMap[prop.Name] = prop;
            }
            currentType = currentType.BaseType;
        }

        foreach (var kvp in overrides)
        {
            var fieldName = kvp.Key;
            var value = kvp.Value;

            if (ReadOnlyProperties.Contains(fieldName))
                continue;

            if (!propertyMap.TryGetValue(fieldName, out var prop))
            {
                SdkLogger.Warning($"    {targetType.Name}: property '{fieldName}' not found");
                continue;
            }

            if (skipType != null && prop.PropertyType.IsAssignableFrom(skipType))
                continue;

            try
            {
                var kind = ClassifyCollectionType(prop.PropertyType, out var elType);
                if (kind != CollectionKind.None && elType != null)
                {
                    if (value is JArray arr)
                    {
                        if (kind == CollectionKind.Il2CppList)
                            EnsureListExists(target, prop);
                        TryApplyCollectionValue(target, prop, arr);
                    }
                    else if (value is JObject collOps && kind == CollectionKind.Il2CppList)
                    {
                        EnsureListExists(target, prop);
                        TryApplyIncrementalList(target, prop, collOps, elType);
                    }
                    continue;
                }

                var currentValue = prop.CanRead ? prop.GetValue(target) : null;
                bool isLocalization = IsLocalizationType(prop.PropertyType) ||
                                      IsRuntimeLocalizationType(currentValue) ||
                                      (IsLikelyLocalizationField(fieldName) &&
                                       currentValue is Il2CppObjectBase &&
                                       value is JValue jv && jv.Type == JTokenType.String);

                if (isLocalization)
                {
                    var stringValue = value is JToken jt ? jt.Value<string>() : value?.ToString();
                    bool success = false;

                    if (target is Il2CppObjectBase il2cppTarget)
                    {
                        success = WriteLocalizedFieldDirect(il2cppTarget, fieldName, stringValue);
                        if (success)
                            SdkLogger.Msg($"    {targetType.Name}.{fieldName}: set localized text (detected by {(IsLocalizationType(prop.PropertyType) ? "type" : IsRuntimeLocalizationType(currentValue) ? "runtime" : "name")})");
                    }
                    else
                    {
                        success = WriteLocalizedFieldViaReflection(target, prop, null, fieldName, stringValue);
                    }

                    if (success)
                    {
                        continue;
                    }
                    SdkLogger.Msg($"    {targetType.Name}.{fieldName}: localization write failed, trying normal assignment");
                }

                var converted = ConvertJTokenToType(value, prop.PropertyType);
                prop.SetValue(target, converted);
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                SdkLogger.Warning($"    {targetType.Name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
            }
        }
    }

    private void EnsureListExists(object owner, PropertyInfo prop)
    {
        var existing = prop.GetValue(owner);
        if (existing != null) return;

        try
        {
            var newList = Activator.CreateInstance(prop.PropertyType);
            prop.SetValue(owner, newList);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    {prop.Name}: failed to construct list: {ex.Message}");
        }
    }

    private bool TryApplyIncrementalList(object castObj, PropertyInfo prop, JObject ops, Type elementType)
    {
        var list = prop.GetValue(castObj);
        if (list == null)
        {
            try
            {
                list = Activator.CreateInstance(prop.PropertyType);
                prop.SetValue(castObj, list);
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"    {prop.Name}: IL2CPP List is null and construction failed: {ex.Message}");
                return false;
            }
        }

        var listType = list.GetType();
        var countProp = listType.GetProperty("Count");
        var getItem = listType.GetMethod("get_Item");
        var removeAt = listType.GetMethod("RemoveAt");
        var addMethod = listType.GetMethod("Add");

        if (countProp == null || getItem == null)
        {
            SdkLogger.Warning($"    {prop.Name}: List missing Count or get_Item");
            return false;
        }

        int opCount = 0;

        // IMPORTANT: Operation order matters for index semantics!
        // UI sends all indices as ORIGINAL indices (before any modifications).
        // We apply in this order:
        // 1. $update — uses original indices on original array
        // 2. $remove — uses original indices, applied highest-first
        // 3. $append — adds to end (indices don't matter)

        if (ops.TryGetValue("$update", out var updateToken) && updateToken is JObject updates)
        {
            var count = (int)countProp.GetValue(list);
            foreach (var kvp in updates)
            {
                if (!int.TryParse(kvp.Key, out var idx))
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: invalid index '{kvp.Key}'");
                    continue;
                }
                if (idx < 0 || idx >= count)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: index {idx} out of range (count={count})");
                    continue;
                }
                if (kvp.Value is not JObject fieldOverrides)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update[{idx}]: expected object");
                    continue;
                }

                var element = getItem.Invoke(list, new object[] { idx });
                if (element != null)
                {
                    ApplyFieldOverrides(element, fieldOverrides);
                    opCount++;
                }
            }
        }

        if (ops.TryGetValue("$remove", out var removeToken) && removeToken is JArray removeIndices)
        {
            if (removeAt == null)
            {
                SdkLogger.Warning($"    {prop.Name}: List has no RemoveAt method");
            }
            else
            {
                var indices = removeIndices.Select(t => t.Value<int>()).OrderByDescending(i => i).ToList();
                var count = (int)countProp.GetValue(list);
                foreach (var idx in indices)
                {
                    if (idx >= 0 && idx < count)
                    {
                        removeAt.Invoke(list, new object[] { idx });
                        count--;
                        opCount++;
                    }
                    else
                    {
                        SdkLogger.Warning($"    {prop.Name}.$remove: index {idx} out of range (count={count})");
                    }
                }
            }
        }

        if (ops.TryGetValue("$append", out var appendToken) && appendToken is JArray appendItems)
        {
            if (addMethod == null)
            {
                SdkLogger.Warning($"    {prop.Name}: List has no Add method");
            }
            else
            {
                foreach (var item in appendItems)
                {
                    var converted = ConvertJTokenToType(item, elementType);
                    if (converted != null)
                    {
                        addMethod.Invoke(list, new[] { converted });
                        opCount++;

                        if (elementType.Name == "ArmyEntry")
                        {
                            LogArmyEntryAppend(converted, item);
                        }
                    }
                    else
                    {
                        SdkLogger.Warning($"    {prop.Name}.$append: failed to convert item: {item}");
                    }
                }
            }
        }

        SdkLogger.Msg($"    {prop.Name}: applied {opCount} incremental ops on List<{elementType.Name}>");
        return opCount > 0;
    }

    private bool TryApplyIncrementalArray(object castObj, PropertyInfo prop, JObject ops, Type elementType, CollectionKind arrayKind)
    {
        var currentArray = prop.GetValue(castObj);
        if (currentArray == null)
        {
            SdkLogger.Warning($"    {prop.Name}: array is null, cannot apply incremental operations");
            return false;
        }

        var arrayType = currentArray.GetType();
        var lengthProp = arrayType.GetProperty("Length");
        var indexer = arrayType.GetProperty("Item");

        if (lengthProp == null || indexer == null)
        {
            SdkLogger.Warning($"    {prop.Name}: array missing Length or Item property");
            return false;
        }

        int currentLength = (int)lengthProp.GetValue(currentArray);
        var elements = new List<object>();

        for (int i = 0; i < currentLength; i++)
        {
            elements.Add(indexer.GetValue(currentArray, new object[] { i }));
        }

        int opCount = 0;

        if (ops.TryGetValue("$update", out var updateToken) && updateToken is JObject updates)
        {
            foreach (var kvp in updates)
            {
                if (!int.TryParse(kvp.Key, out var idx))
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: invalid index '{kvp.Key}'");
                    continue;
                }
                if (idx < 0 || idx >= elements.Count)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: index {idx} out of range (count={elements.Count})");
                    continue;
                }
                if (kvp.Value is not JObject fieldOverrides)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update[{idx}]: expected object");
                    continue;
                }

                var element = elements[idx];
                if (element != null)
                {
                    ApplyFieldOverrides(element, fieldOverrides);
                    opCount++;
                }
            }
        }

        if (ops.TryGetValue("$remove", out var removeToken) && removeToken is JArray removeIndices)
        {
            var indices = removeIndices.Select(t => t.Value<int>()).OrderByDescending(i => i).ToList();
            foreach (var idx in indices)
            {
                if (idx >= 0 && idx < elements.Count)
                {
                    elements.RemoveAt(idx);
                    opCount++;
                }
                else
                {
                    SdkLogger.Warning($"    {prop.Name}.$remove: index {idx} out of range (count={elements.Count})");
                }
            }
        }

        if (ops.TryGetValue("$append", out var appendToken) && appendToken is JArray appendItems)
        {
            foreach (var item in appendItems)
            {
                var converted = ConvertJTokenToType(item, elementType);
                if (converted != null)
                {
                    elements.Add(converted);
                    opCount++;

                    if (elementType.Name == "ArmyEntry")
                    {
                        LogArmyEntryAppend(converted, item);
                    }
                }
                else
                {
                    SdkLogger.Warning($"    {prop.Name}.$append: failed to convert item: {item}");
                }
            }
        }

        if (opCount == 0)
            return false;

        try
        {
            var newArray = Activator.CreateInstance(arrayType, new object[] { elements.Count });
            var newIndexer = arrayType.GetProperty("Item");

            for (int i = 0; i < elements.Count; i++)
            {
                newIndexer.SetValue(newArray, elements[i], new object[] { i });
            }

            prop.SetValue(castObj, newArray);

            var arrayTypeName = arrayKind == CollectionKind.StructArray ? "StructArray" : "ReferenceArray";
            SdkLogger.Msg($"    {prop.Name}: applied {opCount} incremental ops on {arrayTypeName}<{elementType.Name}> ({currentLength} → {elements.Count} elements)");
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"    {prop.Name}: failed to create modified array: {ex.Message}");
            return false;
        }
    }

    private void LogArmyEntryAppend(object armyEntry, JToken sourceItem)
    {
        try
        {
            var entryType = armyEntry.GetType();

            var templateProp = entryType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(armyEntry);
            string templateName = "(null)";
            if (template != null)
            {
                if (template is Il2CppObjectBase il2cppTemplate)
                {
                    var nameField = template.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                    templateName = nameField?.GetValue(template)?.ToString() ?? "(unnamed)";
                }
            }

            var amountProp = entryType.GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance)
                          ?? entryType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            int amount = 1;
            if (amountProp != null)
            {
                amount = (int)amountProp.GetValue(armyEntry);
            }

            var sourceJson = sourceItem?.ToString();
            if (sourceJson?.Length > 100) sourceJson = sourceJson.Substring(0, 100) + "...";
            SdkLogger.Msg($"      ArmyEntry appended: Template='{templateName}', Amount={amount}");

            if (template == null && sourceItem is JObject jObj && jObj.TryGetValue("Template", out var templateToken))
            {
                var requestedTemplate = templateToken.Value<string>();
                SdkLogger.Warning($"      WARNING: Template reference '{requestedTemplate}' resolved to null!");
                SdkLogger.Warning($"      This may indicate the clone was not registered before patching.");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"      LogArmyEntryAppend failed: {ex.Message}");
        }
    }
}
