using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

using Menace.SDK.Internal;

namespace Menace.SDK;

/// <summary>
/// Code-level API for reading, writing, and cloning game templates
/// (ScriptableObjects managed by DataTemplateLoader).
/// </summary>
public static class Templates
{
    private static readonly MethodInfo TryCastMethod =
        typeof(Il2CppObjectBase).GetMethod("TryCast");

    // Cache of template types we've already ensured are loaded
    private static readonly HashSet<string> _loadedTypes = new();

    /// <summary>
    /// Find a specific template instance by type name and instance name.
    /// </summary>
    public static GameObj Find(string templateTypeName, string instanceName)
    {
        if (string.IsNullOrEmpty(templateTypeName) || string.IsNullOrEmpty(instanceName))
            return GameObj.Null;

        // Ensure templates are loaded into memory via DataTemplateLoader
        EnsureTemplatesLoaded(templateTypeName);

        return GameQuery.FindByName(templateTypeName, instanceName);
    }

    /// <summary>
    /// Find a specific template instance and return it as the managed IL2CPP type.
    /// Returns null if not found or conversion fails.
    /// </summary>
    public static T Get<T>(string templateTypeName, string instanceName) where T : class
    {
        var obj = Find(templateTypeName, instanceName);
        return obj.IsNull ? null : obj.As<T>();
    }

    /// <summary>
    /// Find all template instances of a given type.
    /// </summary>
    public static GameObj[] FindAll(string templateTypeName)
    {
        if (string.IsNullOrEmpty(templateTypeName))
            return Array.Empty<GameObj>();

        // Ensure templates are loaded into memory via DataTemplateLoader
        EnsureTemplatesLoaded(templateTypeName);

        return GameQuery.FindAll(templateTypeName);
    }

    /// <summary>
    /// Find all template instances of a given type and return them as managed IL2CPP types.
    /// Items that fail conversion are skipped.
    /// </summary>
    public static List<T> GetAll<T>(string templateTypeName) where T : class
    {
        var objects = FindAll(templateTypeName);
        var result = new List<T>(objects.Length);
        foreach (var obj in objects)
        {
            var managed = obj.As<T>();
            if (managed != null)
                result.Add(managed);
        }
        return result;
    }

    /// <summary>
    /// Find all template instances and return them as managed IL2CPP proxy objects.
    /// Use this when you need to pass objects to reflection or IL2CPP APIs but don't have
    /// compile-time access to the specific type.
    /// </summary>
    public static object[] FindAllManaged(string templateTypeName)
    {
        if (string.IsNullOrEmpty(templateTypeName))
            return Array.Empty<object>();

        return GameQuery.FindAllManaged(templateTypeName);
    }

    /// <summary>
    /// Find a specific template and return it as a managed IL2CPP proxy object.
    /// Use this when you need to pass the object to reflection but don't have
    /// compile-time access to the specific type.
    /// </summary>
    public static object GetManaged(string templateTypeName, string instanceName)
    {
        var obj = Find(templateTypeName, instanceName);
        return obj.IsNull ? null : GameObj<Il2CppObjectBase>.Wrap(obj).AsManaged();
    }

    /// <summary>
    /// Check if a template with the given type and name exists.
    /// </summary>
    public static bool Exists(string templateTypeName, string instanceName)
    {
        return !Find(templateTypeName, instanceName).IsNull;
    }

    /// <summary>
    /// Read a field value from a template object using managed reflection.
    /// Returns null on failure.
    /// </summary>
    public static object ReadField(GameObj template, string fieldName)
    {
        if (template.IsNull || string.IsNullOrEmpty(fieldName))
            return null;

        try
        {
            var gameType = template.GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("Templates.ReadField",
                    $"No managed type for {gameType?.FullName}");
                return null;
            }

            // Get managed proxy wrapper
            var obj = GetManagedProxy(template, managedType);
            if (obj == null) return null;

            // Handle dotted path
            var parts = fieldName.Split('.');
            object current = obj;
            foreach (var part in parts)
            {
                if (current == null) return null;
                var prop = current.GetType().GetProperty(part,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanRead)
                {
                    ModError.WarnInternal("Templates.ReadField",
                        $"Property '{part}' not found on {current.GetType().Name}");
                    return null;
                }
                current = prop.GetValue(current);
            }

            return current;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Templates.ReadField", $"Failed '{fieldName}'", ex);
            return null;
        }
    }

    /// <summary>
    /// Write a field value on a template object using managed reflection.
    /// Returns false on failure.
    /// </summary>
    public static bool WriteField(GameObj template, string fieldName, object value)
    {
        if (template.IsNull || string.IsNullOrEmpty(fieldName))
            return false;

        try
        {
            var gameType = template.GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("Templates.WriteField",
                    $"No managed type for {gameType?.FullName}");
                return false;
            }

            var obj = GetManagedProxy(template, managedType);
            if (obj == null) return false;

            // Handle dotted path — navigate to parent, then set leaf
            var parts = fieldName.Split('.');
            object current = obj;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current == null) return false;
                var prop = current.GetType().GetProperty(parts[i],
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanRead) return false;
                current = prop.GetValue(current);
            }

            if (current == null) return false;

            var leafProp = current.GetType().GetProperty(parts[^1],
                BindingFlags.Public | BindingFlags.Instance);
            if (leafProp == null || !leafProp.CanWrite)
            {
                ModError.WarnInternal("Templates.WriteField",
                    $"Property '{parts[^1]}' not writable on {current.GetType().Name}");
                return false;
            }

            var converted = ConvertValue(value, leafProp.PropertyType);
            leafProp.SetValue(current, converted);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Templates.WriteField", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Write multiple fields on a template object. Returns the number successfully written.
    /// </summary>
    public static int WriteFields(GameObj template, Dictionary<string, object> fields)
    {
        if (template.IsNull || fields == null) return 0;

        int count = 0;
        foreach (var (name, value) in fields)
        {
            if (WriteField(template, name, value))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Clone an existing template via UnityEngine.Object.Instantiate and return
    /// the new instance. Does NOT register in DataTemplateLoader (the main
    /// ModpackLoaderMod cloning pipeline handles that).
    /// </summary>
    public static GameObj Clone(string templateTypeName, string sourceName, string newName)
    {
        if (string.IsNullOrEmpty(templateTypeName) || string.IsNullOrEmpty(sourceName)
            || string.IsNullOrEmpty(newName))
            return GameObj.Null;

        try
        {
            // Ensure templates are loaded into memory via DataTemplateLoader
            EnsureTemplatesLoaded(templateTypeName);

            var gameType = GameType.Find(templateTypeName);
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("Templates.Clone",
                    $"No managed type for '{templateTypeName}'");
                return GameObj.Null;
            }

            var il2cppType = Il2CppType.From(managedType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);
            if (objects == null || objects.Length == 0)
            {
                ModError.WarnInternal("Templates.Clone",
                    $"No instances of '{templateTypeName}' found");
                return GameObj.Null;
            }

            UnityEngine.Object source = null;
            foreach (var obj in objects)
            {
                if (obj != null && obj.name == sourceName)
                {
                    source = obj;
                    break;
                }
            }

            if (source == null)
            {
                ModError.WarnInternal("Templates.Clone",
                    $"Source '{sourceName}' not found");
                return GameObj.Null;
            }

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = newName;
            clone.hideFlags = HideFlags.DontUnloadUnusedAsset;

            return new GameObj(clone.Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Templates.Clone",
                $"Failed to clone {sourceName} -> {newName}", ex);
            return GameObj.Null;
        }
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);

    private static object ConvertValue(object value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, Convert.ToInt32(value));
        if (targetType == typeof(int)) return Convert.ToInt32(value);
        if (targetType == typeof(float)) return Convert.ToSingle(value);
        if (targetType == typeof(double)) return Convert.ToDouble(value);
        if (targetType == typeof(bool)) return Convert.ToBoolean(value);
        if (targetType == typeof(string)) return value.ToString();

        return Convert.ChangeType(value, targetType);
    }

    /// <summary>
    /// Get a property value from a template by type name, instance name, and property path.
    /// Returns null if template or property not found.
    /// </summary>
    /// <example>
    /// var damage = Templates.GetProperty("WeaponTemplate", "weapon.sword", "Damage");
    /// var displayName = Templates.GetProperty("ArmorTemplate", "armor.shield", "DisplayName");
    /// </example>
    public static object GetProperty(string templateTypeName, string instanceName, string propertyPath)
    {
        var template = Find(templateTypeName, instanceName);
        if (template.IsNull)
            return null;

        return ReadField(template, propertyPath);
    }

    /// <summary>
    /// Get a property value from a template with automatic type conversion.
    /// Returns default(T) if template or property not found.
    /// </summary>
    /// <example>
    /// var damage = Templates.GetProperty&lt;int&gt;("WeaponTemplate", "Sword", "Damage");
    /// var displayName = Templates.GetProperty&lt;string&gt;("WeaponTemplate", "Sword", "DisplayName");
    /// </example>
    public static T GetProperty<T>(string templateTypeName, string instanceName, string propertyPath)
    {
        var value = GetProperty(templateTypeName, instanceName, propertyPath);
        if (value == null)
            return default;

        try
        {
            if (value is T typed)
                return typed;

            // Try conversion
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Get multiple property values from a template at once.
    /// Returns a dictionary of property name to value.
    /// Properties that fail to read are omitted from the result.
    /// </summary>
    /// <example>
    /// var props = Templates.GetProperties("WeaponTemplate", "Sword", "Damage", "Range", "DisplayName");
    /// int damage = (int)props["Damage"];
    /// </example>
    public static Dictionary<string, object> GetProperties(string templateTypeName, string instanceName, params string[] propertyPaths)
    {
        var result = new Dictionary<string, object>();
        var template = Find(templateTypeName, instanceName);

        if (template.IsNull)
            return result;

        foreach (var propertyPath in propertyPaths)
        {
            var value = ReadField(template, propertyPath);
            if (value != null)
                result[propertyPath] = value;
        }

        return result;
    }

    /// <summary>
    /// Ensures templates of the given type are loaded into memory by calling
    /// DataTemplateLoader.GetAll&lt;T&gt;(). Templates loaded this way become
    /// findable via Resources.FindObjectsOfTypeAll().
    /// </summary>
    private static void EnsureTemplatesLoaded(string templateTypeName)
    {
        // Only try once per type to avoid repeated reflection overhead
        if (_loadedTypes.Contains(templateTypeName))
            return;

        try
        {
            var gameType = GameType.Find(templateTypeName);
            var managedType = gameType?.ManagedType;
            if (managedType == null)
                return;

            // Find DataTemplateLoader in Assembly-CSharp
            var gameAssembly = GameState.GameAssembly;
            if (gameAssembly == null)
                return;

            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
                return;

            // Get the GetAll<T>() method
            var getAllMethod = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

            if (getAllMethod == null)
                return;

            // Call GetAll<T>() to force templates into memory
            var genericMethod = getAllMethod.MakeGenericMethod(managedType);
            genericMethod.Invoke(null, null);

            _loadedTypes.Add(templateTypeName);
        }
        catch (Exception ex)
        {
            // Don't fail hard - just log and continue, FindAll will return empty
            ModError.WarnInternal("Templates.EnsureTemplatesLoaded",
                $"Failed for {templateTypeName}: {ex.Message}");
        }
    }

    // ==================== Localization Field Helpers ====================

    /// <summary>
    /// Get the localization key from a localization field (m_LocaState, LocalizedLine, etc.)
    /// Returns the key string (e.g., "weapons.assault_rifle.name") or null if not a localization field.
    /// </summary>
    public static string GetLocalizationKey(string templateTypeName, string instanceName, string fieldName)
    {
        try
        {
            var fieldValue = GetProperty(templateTypeName, instanceName, fieldName);
            if (fieldValue == null) return null;

            // Try to get m_Key from the field value
            var keyField = fieldValue.GetType().GetField("m_Key");
            if (keyField != null)
            {
                var keyValue = keyField.GetValue(fieldValue);
                return keyValue?.ToString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the localization table ID from a localization field.
    /// Returns the table ID string (e.g., "weapons", "skills") or null if not a localization field.
    /// </summary>
    public static string GetLocalizationTable(string templateTypeName, string instanceName, string fieldName)
    {
        try
        {
            var fieldValue = GetProperty(templateTypeName, instanceName, fieldName);
            if (fieldValue == null) return null;

            // Try to get m_TableID from the field value
            var tableField = fieldValue.GetType().GetField("m_TableID");
            if (tableField != null)
            {
                var tableValue = tableField.GetValue(fieldValue);
                return tableValue?.ToString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the translated text for a localization field in a specific language.
    /// Automatically extracts the key and table from the field and looks up the translation.
    /// </summary>
    public static string GetLocalizedText(string templateTypeName, string instanceName, string fieldName, string language = "English")
    {
        var key = GetLocalizationKey(templateTypeName, instanceName, fieldName);
        var table = GetLocalizationTable(templateTypeName, instanceName, fieldName);

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(table))
            return null;

        // Extract category from table (usually matches table ID)
        return MultiLingualLocalization.GetTranslation(language, table, key);
    }

    /// <summary>
    /// Get the translated text for a localization field in ALL languages.
    /// Returns a dictionary of language -> translated text.
    /// </summary>
    public static Dictionary<string, string> GetAllLocalizedTexts(string templateTypeName, string instanceName, string fieldName)
    {
        var key = GetLocalizationKey(templateTypeName, instanceName, fieldName);
        var table = GetLocalizationTable(templateTypeName, instanceName, fieldName);

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(table))
            return new Dictionary<string, string>();

        return MultiLingualLocalization.GetAllTranslations(table, key);
    }

    /// <summary>
    /// Helper to get localization info for a template field in a structured format.
    /// Returns an object with: key, tableID, and translations for all languages.
    /// </summary>
    public static object GetLocalizationInfo(string templateTypeName, string instanceName, string fieldName)
    {
        var key = GetLocalizationKey(templateTypeName, instanceName, fieldName);
        var table = GetLocalizationTable(templateTypeName, instanceName, fieldName);

        if (string.IsNullOrEmpty(key))
            return null;

        var translations = string.IsNullOrEmpty(table)
            ? new Dictionary<string, string>()
            : MultiLingualLocalization.GetAllTranslations(table, key);

        return new
        {
            key,
            tableID = table,
            languages = translations.Keys.ToArray(),
            languageCount = translations.Count,
            translations
        };
    }

    /// <summary>
    /// Set a template's localization field to use a different key.
    /// This changes which translation the template will display.
    /// </summary>
    public static void SetLocalizationKey(string templateTypeName, string instanceName, string fieldName, string newKey, string tableID)
    {
        var template = Find(templateTypeName, instanceName);
        if (template.IsNull)
        {
            ModError.WarnInternal("Templates.SetLocalizationKey", $"Template not found: {templateTypeName}/{instanceName}");
            return;
        }

        // Create new LocaState object
        var locaState = new
        {
            m_Key = newKey,
            m_TableID = tableID
        };

        WriteField(template, fieldName, locaState);
    }

    /// <summary>
    /// Helper to check if a field is a localization field (has m_Key and m_TableID).
    /// </summary>
    public static bool IsLocalizationField(string templateTypeName, string instanceName, string fieldName)
    {
        var fieldValue = GetProperty(templateTypeName, instanceName, fieldName);
        if (fieldValue == null) return false;

        var type = fieldValue.GetType();
        return type.GetField("m_Key") != null && type.GetField("m_TableID") != null;
    }
}
