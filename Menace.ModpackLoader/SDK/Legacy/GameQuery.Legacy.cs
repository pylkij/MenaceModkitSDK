using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

public static partial class GameQuery
{
    private static readonly Dictionary<IntPtr, GameObj[]> _cache = new();

    /// LEGACY USES
    /// Some methods couldn't be ported to the new style, so here be dragons and other bullshit
    /// from the before times.

    /// [Obsolete("Use FindAll<T>() for compile-time safety")]
    internal static GameObj[] FindAll(string typeName, string assembly = "Assembly-CSharp")
    {
        var type = GameType.Find(typeName, assembly);
        return FindAll(type);
    }

    /// [Obsolete("Use FindAll<T>() for compile-time safety")]
    internal static GameObj[] FindAll(GameType type)
    {
        if (type == null || !type.IsValid)
            return Array.Empty<GameObj>();

        try
        {
            var managedType = type.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("GameQuery.FindAll", $"No managed proxy for {type.FullName}");
                return Array.Empty<GameObj>();
            }

            var il2cppType = Il2CppType.From(managedType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects == null || objects.Length == 0)
                return Array.Empty<GameObj>();

            var results = new GameObj[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                results[i] = obj != null ? new GameObj(obj.Pointer) : GameObj.Null;
            }

            return results;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameQuery.FindAll", $"Failed for {type.FullName}", ex);
            return Array.Empty<GameObj>();
        }
    }

    /// [Obsolete("Use FindAll<T>() for compile-time safety")]
    internal static object[] FindAllManaged(string typeName, string assembly = "Assembly-CSharp")
    {
        var type = GameType.Find(typeName, assembly);
        return FindAllManaged(type);
    }

    /// [Obsolete("Use FindAll<T>() for compile-time safety")]
    internal static object[] FindAllManaged(GameType type)
    {
        if (type == null || !type.IsValid)
            return Array.Empty<object>();

        try
        {
            var managedType = type.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("GameQuery.FindAllManaged", $"No managed proxy for {type.FullName}");
                return Array.Empty<object>();
            }

            var il2cppType = Il2CppType.From(managedType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects == null || objects.Length == 0)
                return Array.Empty<object>();

            // Get the IntPtr constructor for the target type to create properly-typed instances
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null)
            {
                ModError.WarnInternal("GameQuery.FindAllManaged", $"No IntPtr constructor on {managedType.Name}");
                return Array.Empty<object>();
            }

            // Create properly-typed managed proxy instances
            var results = new List<object>(objects.Length);
            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj == null) continue;

                try
                {
                    // Get the pointer and construct the correct type
                    var ptr = obj.Pointer;
                    if (ptr != IntPtr.Zero)
                    {
                        var typedObj = ptrCtor.Invoke(new object[] { ptr });
                        results.Add(typedObj);
                    }
                }
                catch
                {
                    // Skip objects that fail to convert
                }
            }

            return results.ToArray();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameQuery.FindAllManaged", $"Failed for {type.FullName}", ex);
            return Array.Empty<object>();
        }
    }

    /// [Obsolete("Use FindAll<T>() for compile-time safety")]
    internal static GameObj FindByName(string typeName, string name)
    {
        var type = GameType.Find(typeName);
        return FindByName(type, name);
    }

    internal static GameObj FindByName(GameType type, string name)
    {
        if (string.IsNullOrEmpty(name))
            return GameObj.Null;

        var all = FindAll(type);
        foreach (var obj in all)
        {
            var objName = obj.GetName();
            if (objName == name)
                return obj;
        }
        return GameObj.Null;
    }
}