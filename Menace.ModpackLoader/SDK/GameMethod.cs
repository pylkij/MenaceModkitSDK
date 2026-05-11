using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// Invoke methods on IL2CPP game objects via expression trees.
/// Type and method resolution happens at compile time; failures surface
/// as compiler errors rather than runtime exceptions.
/// Complements GameObj field reads with method call capability.
/// </summary>
public static class GameMethod
{
    // Keyed by MethodInfo directly — stable reference on a loaded assembly.
    // Acts as an identity cache: confirms a method has been validated and is
    // ready to invoke without re-evaluating the expression tree on every call.
    private static readonly HashSet<MethodInfo> _methodCache = new();

    // ═══════════════════════════════════════════════════════════════════
    //  Method resolution
    // ═══════════════════════════════════════════════════════════════════

    private static MethodInfo ResolveMethod<TType>(Expression<Action<TType>> methodExpr)
    {
        if (methodExpr.Body is not MethodCallExpression callExpr)
        {
            ModError.ReportInternal("...", "GameMethod.ResolveMethod", null);
            return null;
        }
        return ResolveFromExpression(callExpr);
    }

    private static MethodInfo ResolveMethod<TType, TReturn>(Expression<Func<TType, TReturn>> methodExpr)
    {
        if (methodExpr.Body is not MethodCallExpression callExpr)
        {
            ModError.ReportInternal("...", "GameMethod.ResolveMethod", null);
            return null;
        }
        return ResolveFromExpression(callExpr);
    }

    private static MethodInfo ResolveFromExpression(MethodCallExpression callExpr)
    {
        var method = callExpr.Method;
        if (_methodCache.Contains(method))
            return method;
        _methodCache.Add(method);
        return method;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Static calls (singletons, factory methods)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invoke a static method resolved via expression tree. Returns null on failure.
    /// </summary>
    public static object CallStatic<TType>(
        Expression<Action<TType>> methodExpr,
        object[] args = null)
    {
        try
        {
            var method = ResolveMethod(methodExpr);
            if (method == null) return null;

            return method.Invoke(null, args);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal($"CallStatic failed — {typeof(TType).Name}", "GameMethod.CallStatic", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Instance calls — returns object
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invoke an instance method on an IL2CPP object. Returns null on failure.
    /// </summary>
    public static object Call<TType>(
        object instance,
        Expression<Action<TType>> methodExpr,
        object[] args = null)
    {
        if (instance == null)
        {
            ModError.ReportInternal($"Null instance for {typeof(TType).Name}", "GameMethod.Call", null);
            return null;
        }

        try
        {
            var method = ResolveMethod(methodExpr);
            if (method == null) return null;

            return method.Invoke(instance, args);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal($"Call failed — {typeof(TType).Name}", "GameMethod.Call", ex);
            return null;
        }
    }

    private static object Call<TType, TReturn>(
        object instance,
        Expression<Func<TType, TReturn>> methodExpr,
        object[] args = null)
    {
        if (instance == null)
        {
            ModError.ReportInternal($"Null instance for {typeof(TType).Name}", "GameMethod.Call", null);
            return null;
        }

        try
        {
            var method = ResolveMethod(methodExpr);
            if (method == null) return null;

            return method.Invoke(instance, args);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal($"Call failed — {typeof(TType).Name}", "GameMethod.Call", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Instance calls — typed convenience wrappers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invoke an instance method and return the result as int. Returns 0 on failure.
    /// </summary>
    public static int CallInt<TType>(
        object instance,
        Expression<Func<TType, int>> methodExpr,
        object[] args = null)
    {
        var result = Call<TType, int>(instance, methodExpr, args);
        if (result is int i) return i;
        if (result != null)
            ModError.ReportInternal($"Unexpected return type {result.GetType()} for {typeof(TType).Name}", "GameMethod.CallInt", null);
        return 0;
    }

    /// <summary>
    /// Invoke an instance method and return the result as bool. Returns false on failure.
    /// </summary>
    public static bool CallBool<TType>(
        object instance,
        Expression<Func<TType, bool>> methodExpr,
        object[] args = null)
    {
        var result = Call<TType, bool>(instance, methodExpr, args);
        if (result is bool b) return b;
        if (result != null)
            ModError.ReportInternal($"Unexpected return type {result.GetType()} for {typeof(TType).Name}", "GameMethod.CallBool", null);
        return false;
    }

    /// <summary>
    /// Invoke an instance method and return the result as IntPtr. Returns IntPtr.Zero on failure.
    /// </summary>
    public static IntPtr CallPtr<TType>(
        object instance,
        Expression<Func<TType, IntPtr>> methodExpr,
        object[] args = null)
    {
        var result = Call<TType, IntPtr>(instance, methodExpr, args);
        if (result is IntPtr ptr) return ptr;
        if (result is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj) return il2cppObj.Pointer;
        if (result != null)
            ModError.ReportInternal($"Unexpected return type {result.GetType()} for {typeof(TType).Name}", "GameMethod.CallPtr", null);
        return IntPtr.Zero;
    }
}
