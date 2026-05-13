using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Safe wrappers around FindObjectsOfTypeAll for discovering game objects
/// by IL2CPP type. Includes a per-scene cache that is cleared on scene load.
/// </summary>
public static partial class GameQuery
{
    /// <summary>
    /// Find all objects of a given IL2CppInterop proxy type.
    /// </summary>
    public static T[] FindAll<T>() where T : Il2CppObjectBase
    {
        try
        {
            var il2cppType = Il2CppType.From(typeof(T));
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects == null || objects.Length == 0)
                return Array.Empty<T>();

            var results = new List<T>(objects.Length);
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                var ptr = obj.Pointer;
                if (ptr != IntPtr.Zero)
                    results.Add(IL2CPP.PointerToValueGeneric<T>(ptr, false, false));
            }
            return results.ToArray();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameQuery.FindAll<T>", $"Failed for {typeof(T).Name}", ex);
            return Array.Empty<T>();
        }
    }

    /// <summary>
    /// Find an object by type name and Unity object name.
    /// </summary>
    public static T FindByName<T>(string name) where T : Il2CppObjectBase
    {
        if (string.IsNullOrEmpty(name))
            return null;

        foreach (var obj in FindAll<T>())
        {
            if (obj is UnityEngine.Object uo && uo.name == name)
                return obj;
        }
        return null;
    }

    /// <summary>
    /// Cached variant of FindAll — results are cached until ClearCache is called
    /// (typically on scene load).
    /// </summary>
    public static T[] FindAllCached<T>() where T : Il2CppObjectBase
    {
        if (Cache<T>.Value != null)
            return Cache<T>.Value;

        return Cache<T>.Value = FindAll<T>();
    }

    private static class Cache<T> where T : Il2CppObjectBase
    {
        internal static T[] Value = null;
    }

    /// <summary>
    /// Clear the per-scene query cache. Called from ModpackLoaderMod.OnSceneWasLoaded.
    /// </summary>
    internal static void ClearCache()
    {
        _cache.Clear();
    }


}