using HarmonyLib;
using System;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// Harmony patching helpers with hierarchy-aware method discovery.
/// Pass target types using typeof() for compile-time verification.
/// Failed patches log to ModError and return false.
/// </summary>
public static class GamePatch
{
    /// <summary>
    /// Apply a Harmony Postfix patch to a method on the given type.
    /// Pass the target type using typeof() to ensure compile-time verification.
    /// Use the overload with parameterTypes when the method has multiple overloads.
    /// Failed patches log to ModError and return false.
    /// </summary>
    public static bool Postfix(HarmonyLib.Harmony harmony, Type targetType, string methodName, MethodInfo patchMethod)
    {
        return PatchInternal(harmony, targetType, methodName, null, patchMethod);
    }

    /// <summary>
    /// Apply a Harmony Prefix patch to a method on the given type.
    /// Pass the target type using typeof() to ensure compile-time verification.
    /// Use the overload with parameterTypes when the method has multiple overloads.
    /// Failed patches log to ModError and return false.
    /// </summary>
    public static bool Prefix(HarmonyLib.Harmony harmony, Type targetType, string methodName, MethodInfo patchMethod)
    {
        return PatchInternal(harmony, targetType, methodName, patchMethod, null);
    }

    private static bool PatchInternal(HarmonyLib.Harmony harmony, Type targetType,
        string methodName, MethodInfo prefix, MethodInfo postfix)
    {
        if (harmony == null)
        {
            ModError.ReportInternal("GamePatch", "Harmony instance is null");
            return false;
        }

        try
        {
            var method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);

            if (method == null)
            {
                // Try by declared-only in the type hierarchy
                var current = targetType;
                while (current != null && method == null)
                {
                    method = current.GetMethod(methodName,
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.DeclaredOnly);
                    current = current.BaseType;
                }
            }

            if (method == null)
            {
                ModError.ReportInternal("GamePatch",
                    $"Method '{methodName}' not found on {targetType.Name}");
                return false;
            }

            var prefixHm = prefix != null ? new HarmonyMethod(prefix) : null;
            var postfixHm = postfix != null ? new HarmonyMethod(postfix) : null;

            harmony.Patch(method, prefix: prefixHm, postfix: postfixHm);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GamePatch",
                $"Failed to patch {targetType.Name}.{methodName}", ex);
            return false;
        }
    }
}