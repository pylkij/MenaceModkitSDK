using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// Invoke methods on IL2CPP game objects by name via reflection,
/// with cached MethodInfo lookups to avoid repeated scanning.
/// Complements GameObj field reads with method call capability.
/// </summary>
public static class GameMethod
{
    private static Assembly _gameAssembly;
    private static readonly Dictionary<string, Type> _typeCache = new();

    // Cache keyed by "typeName::methodName" — MethodInfo on a stable game assembly
    // does not change for the lifetime of the process.
    private static readonly Dictionary<string, MethodInfo> _methodCache = new();

    // ═══════════════════════════════════════════════════════════════════
    //  Type resolution
    // ═══════════════════════════════════════════════════════════════════

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            ModError.ReportInternal($"GameMethod: type name is null or empty");
            return null;
        }

        if (_typeCache.TryGetValue(typeName, out var cached))
            return cached;

        try
        {
            _gameAssembly ??= AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (_gameAssembly == null)
            {
                ModError.ReportInternal($"GameMethod: Assembly-CSharp not loaded");
                return null;
            }

            var type = _gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

            if (type == null)
            {
                ModError.ReportInternal($"GameMethod: type not found — {typeName}");
                return null;
            }

            _typeCache[typeName] = type;
            return type;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal($"GameMethod: failed to resolve type '{typeName}' — {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Method resolution
    // ═══════════════════════════════════════════════════════════════════

    private static MethodInfo ResolveMethod(string typeName, string methodName, Type[] paramTypes = null)
    {
        var cacheKey = paramTypes == null
            ? $"{typeName}::{methodName}"
            : $"{typeName}::{methodName}({string.Join(",", Array.ConvertAll(paramTypes, t => t.FullName))})";

        if (_methodCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var type = ResolveType(typeName);
            if (type == null) return null;

            MethodInfo method;
            if (paramTypes != null)
            {
                method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                    null, paramTypes, null);
            }
            else
            {
                method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }

            if (method == null)
            {
                ModError.ReportInternal($"GameMethod: method not found — {typeName}.{methodName}");
                return null;
            }

            _methodCache[cacheKey] = method;
            return method;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal($"GameMethod: failed to resolve {typeName}.{methodName} — {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Static calls (singletons, factory methods)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invoke a static method by type and method name. Returns null on failure.
    /// </summary>
    public static object CallStatic(string typeName, string methodName, Type[] paramTypes = null, object[] args = null)
    {
        try
        {
            var method = ResolveMethod(typeName, methodName, paramTypes);
            if (method == null) return null;

            return method.Invoke(null, args);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal($"GameMethod: CallStatic failed — {typeName}.{methodName} — {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Instance calls — returns object
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invoke an instance method on an IL2CPP object. Returns null on failure.
    /// </summary>
    public static object Call(object instance, string typeName, string methodName, Type[] paramTypes = null, object[] args = null)
    {
        if (instance == null)
        {
            ModError.ReportInternal($"GameMethod: Call — null instance for {typeName}.{methodName}");
            return null;
        }

        try
        {
            var method = ResolveMethod(typeName, methodName, paramTypes);
            if (method == null) return null;

            return method.Invoke(instance, args);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal($"GameMethod: Call failed — {typeName}.{methodName} — {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Instance calls — typed convenience wrappers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invoke an instance method and return the result as int. Returns 0 on failure.
    /// </summary>
    public static int CallInt(object instance, string typeName, string methodName, Type[] paramTypes = null, object[] args = null)
    {
        var result = Call(instance, typeName, methodName, paramTypes, args);
        if (result is int i) return i;
        if (result != null)
            ModError.ReportInternal($"GameMethod: CallInt — unexpected return type {result.GetType()} for {typeName}.{methodName}");
        return 0;
    }

    /// <summary>
    /// Invoke an instance method and return the result as bool. Returns false on failure.
    /// </summary>
    public static bool CallBool(object instance, string typeName, string methodName, Type[] paramTypes = null, object[] args = null)
    {
        var result = Call(instance, typeName, methodName, paramTypes, args);
        if (result is bool b) return b;
        if (result != null)
            ModError.ReportInternal($"GameMethod: CallBool — unexpected return type {result.GetType()} for {typeName}.{methodName}");
        return false;
    }

    /// <summary>
    /// Invoke an instance method and return the result as IntPtr. Returns IntPtr.Zero on failure.
    /// </summary>
    public static IntPtr CallPtr(object instance, string typeName, string methodName, Type[] paramTypes = null, object[] args = null)
    {
        var result = Call(instance, typeName, methodName, paramTypes, args);
        if (result is IntPtr ptr) return ptr;
        if (result is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj) return il2cppObj.Pointer;
        if (result != null)
            ModError.ReportInternal($"GameMethod: CallPtr — unexpected return type {result.GetType()} for {typeName}.{methodName}");
        return IntPtr.Zero;
    }
}
