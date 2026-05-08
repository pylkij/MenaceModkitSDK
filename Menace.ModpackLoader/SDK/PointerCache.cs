using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// A dictionary that maps IL2CPP object pointers (IntPtr) to values.
/// Simplifies the common pattern of tracking relationships between IL2CPP objects.
///
/// Usage:
/// <code>
/// private static readonly PointerCache&lt;float&gt; s_BaseSupprMult = new();
/// s_BaseSupprMult.Set(props.Pointer, props.SuppressionImpactMult);
/// if (s_BaseSupprMult.TryGet(props.Pointer, out float value)) { }
/// </code>
/// </summary>
/// <typeparam name="TValue">The type of values to store.</typeparam>
public class PointerCache<TValue> : IEnumerable<KeyValuePair<IntPtr, TValue>>
{
    private readonly IDictionary<IntPtr, TValue> _dict;
    private readonly bool _concurrent;

    /// <summary>
    /// Create a new PointerCache.
    /// </summary>
    /// <param name="concurrent">If true, uses ConcurrentDictionary for thread safety.</param>
    public PointerCache(bool concurrent = false)
    {
        _concurrent = concurrent;
        _dict = concurrent
            ? new ConcurrentDictionary<IntPtr, TValue>()
            : new Dictionary<IntPtr, TValue>();
    }

    /// <summary>
    /// Number of entries in the cache.
    /// </summary>
    public int Count => _dict.Count;

    /// <summary>
    /// Check if the cache contains a pointer.
    /// </summary>
    public bool Contains(IntPtr pointer) => pointer != IntPtr.Zero && _dict.ContainsKey(pointer);

    /// <summary>
    /// Set a value for a pointer. Does nothing if pointer is zero.
    /// </summary>
    public void Set(IntPtr pointer, TValue value)
    {
        if (pointer == IntPtr.Zero) return;
        _dict[pointer] = value;
    }

    /// <summary>
    /// Try to get a value for a pointer.
    /// </summary>
    /// <returns>True if found, false otherwise.</returns>
    public bool TryGet(IntPtr pointer, out TValue value)
    {
        if (pointer == IntPtr.Zero)
        {
            value = default;
            return false;
        }
        return _dict.TryGetValue(pointer, out value);
    }

    /// <summary>
    /// Get a value for a pointer, or return the default value if not found.
    /// </summary>
    public TValue Get(IntPtr pointer, TValue defaultValue = default)
    {
        if (pointer == IntPtr.Zero) return defaultValue;
        return _dict.TryGetValue(pointer, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Get an existing value or set and return a new value atomically.
    /// </summary>
    /// <param name="pointer">The pointer key.</param>
    /// <param name="factory">Factory function to create the value if not found.</param>
    /// <returns>The existing or newly created value.</returns>
    public TValue GetOrSet(IntPtr pointer, Func<TValue> factory)
    {
        if (pointer == IntPtr.Zero) return default;
        if (factory == null) return default;

        if (_dict.TryGetValue(pointer, out var existing))
            return existing;

        var value = factory();

        if (_concurrent)
        {
            // ConcurrentDictionary.GetOrAdd handles race conditions
            return ((ConcurrentDictionary<IntPtr, TValue>)_dict).GetOrAdd(pointer, _ => value);
        }

        _dict[pointer] = value;
        return value;
    }

    /// <summary>
    /// Get an existing value or set and return a new value.
    /// </summary>
    /// <param name="pointer">The pointer key.</param>
    /// <param name="value">Value to store if not found.</param>
    /// <returns>The existing or newly stored value.</returns>
    public TValue GetOrSet(IntPtr pointer, TValue value)
    {
        if (pointer == IntPtr.Zero) return default;

        if (_dict.TryGetValue(pointer, out var existing))
            return existing;

        if (_concurrent)
        {
            return ((ConcurrentDictionary<IntPtr, TValue>)_dict).GetOrAdd(pointer, value);
        }

        _dict[pointer] = value;
        return value;
    }

    /// <summary>
    /// Remove an entry from the cache.
    /// </summary>
    /// <returns>True if removed, false if not found.</returns>
    public bool Remove(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return false;
        return _dict.Remove(pointer);
    }

    /// <summary>
    /// Remove entries where the predicate returns false.
    /// Use this to clean up dead IL2CPP object references.
    ///
    /// Example:
    /// <code>
    /// // Remove entries where the Unity object is no longer alive
    /// cache.Prune(ptr => new GameObj(ptr).IsAlive);
    /// </code>
    /// </summary>
    /// <param name="keepPredicate">Return true to keep the entry, false to remove.</param>
    /// <returns>Number of entries removed.</returns>
    public int Prune(Func<IntPtr, bool> keepPredicate)
    {
        if (keepPredicate == null) return 0;

        var toRemove = new List<IntPtr>();
        foreach (var kvp in _dict)
        {
            try
            {
                if (!keepPredicate(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            catch
            {
                // Predicate threw - remove the entry as a safety measure
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var ptr in toRemove)
            _dict.Remove(ptr);

        return toRemove.Count;
    }

    /// <summary>
    /// Remove entries where the value predicate returns false.
    /// </summary>
    /// <param name="keepPredicate">Return true to keep the entry, false to remove.</param>
    /// <returns>Number of entries removed.</returns>
    public int PruneByValue(Func<TValue, bool> keepPredicate)
    {
        if (keepPredicate == null) return 0;

        var toRemove = new List<IntPtr>();
        foreach (var kvp in _dict)
        {
            try
            {
                if (!keepPredicate(kvp.Value))
                    toRemove.Add(kvp.Key);
            }
            catch
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var ptr in toRemove)
            _dict.Remove(ptr);

        return toRemove.Count;
    }

    /// <summary>
    /// Clear all entries from the cache.
    /// </summary>
    public void Clear() => _dict.Clear();

    /// <summary>
    /// Get all stored pointers.
    /// </summary>
    public IEnumerable<IntPtr> Keys => _dict.Keys;

    /// <summary>
    /// Get all stored values.
    /// </summary>
    public IEnumerable<TValue> Values => _dict.Values;

    public IEnumerator<KeyValuePair<IntPtr, TValue>> GetEnumerator() => _dict.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Indexer for direct access. Returns default for null/missing keys.
    /// </summary>
    public TValue this[IntPtr pointer]
    {
        get => Get(pointer);
        set => Set(pointer, value);
    }
}

/// <summary>
/// A typed dictionary that maps IL2CPP objects to values, automatically extracting
/// the .Pointer property for storage.
///
/// Usage:
/// <code>
/// private static readonly PointerMap&lt;EntityProperties, Entity&gt; s_PropsToEntity = new();
/// s_PropsToEntity.Set(props, entity);  // Uses props.Pointer internally
/// if (s_PropsToEntity.TryGet(props, out Entity entity)) { }
/// </code>
/// </summary>
/// <typeparam name="TKey">An IL2CPP proxy type with a .Pointer property (Il2CppObjectBase).</typeparam>
/// <typeparam name="TValue">The type of values to store.</typeparam>
public class PointerMap<TKey, TValue> : IEnumerable<KeyValuePair<IntPtr, TValue>>
    where TKey : Il2CppObjectBase
{
    private readonly PointerCache<TValue> _cache;

    /// <summary>
    /// Create a new PointerMap.
    /// </summary>
    /// <param name="concurrent">If true, uses ConcurrentDictionary for thread safety.</param>
    public PointerMap(bool concurrent = false)
    {
        _cache = new PointerCache<TValue>(concurrent);
    }

    /// <summary>
    /// Number of entries in the map.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Check if the map contains a key.
    /// </summary>
    public bool Contains(TKey key) => key != null && _cache.Contains(key.Pointer);

    /// <summary>
    /// Set a value for a key. Does nothing if key is null.
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        if (key == null) return;
        _cache.Set(key.Pointer, value);
    }

    /// <summary>
    /// Try to get a value for a key.
    /// </summary>
    /// <returns>True if found, false otherwise.</returns>
    public bool TryGet(TKey key, out TValue value)
    {
        if (key == null)
        {
            value = default;
            return false;
        }
        return _cache.TryGet(key.Pointer, out value);
    }

    /// <summary>
    /// Get a value for a key, or return the default value if not found.
    /// </summary>
    public TValue Get(TKey key, TValue defaultValue = default)
    {
        if (key == null) return defaultValue;
        return _cache.Get(key.Pointer, defaultValue);
    }

    /// <summary>
    /// Get an existing value or set and return a new value.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="factory">Factory function to create the value if not found.</param>
    /// <returns>The existing or newly created value.</returns>
    public TValue GetOrSet(TKey key, Func<TValue> factory)
    {
        if (key == null) return default;
        return _cache.GetOrSet(key.Pointer, factory);
    }

    /// <summary>
    /// Get an existing value or set and return a new value.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">Value to store if not found.</param>
    /// <returns>The existing or newly stored value.</returns>
    public TValue GetOrSet(TKey key, TValue value)
    {
        if (key == null) return default;
        return _cache.GetOrSet(key.Pointer, value);
    }

    /// <summary>
    /// Remove an entry from the map.
    /// </summary>
    /// <returns>True if removed, false if not found.</returns>
    public bool Remove(TKey key)
    {
        if (key == null) return false;
        return _cache.Remove(key.Pointer);
    }

    /// <summary>
    /// Remove entries where the predicate returns false.
    /// Use this to clean up dead IL2CPP object references.
    ///
    /// Example:
    /// <code>
    /// map.Prune(ptr => new GameObj(ptr).IsAlive);
    /// </code>
    /// </summary>
    /// <param name="keepPredicate">Return true to keep the entry, false to remove.</param>
    /// <returns>Number of entries removed.</returns>
    public int Prune(Func<IntPtr, bool> keepPredicate) => _cache.Prune(keepPredicate);

    /// <summary>
    /// Remove entries where the value predicate returns false.
    /// </summary>
    /// <param name="keepPredicate">Return true to keep the entry, false to remove.</param>
    /// <returns>Number of entries removed.</returns>
    public int PruneByValue(Func<TValue, bool> keepPredicate) => _cache.PruneByValue(keepPredicate);

    /// <summary>
    /// Clear all entries from the map.
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Get all stored pointers.
    /// </summary>
    public IEnumerable<IntPtr> Keys => _cache.Keys;

    /// <summary>
    /// Get all stored values.
    /// </summary>
    public IEnumerable<TValue> Values => _cache.Values;

    /// <summary>
    /// Get the underlying PointerCache for direct IntPtr access.
    /// </summary>
    public PointerCache<TValue> Inner => _cache;

    public IEnumerator<KeyValuePair<IntPtr, TValue>> GetEnumerator() => _cache.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Indexer for direct access. Returns default for null/missing keys.
    /// </summary>
    public TValue this[TKey key]
    {
        get => Get(key);
        set => Set(key, value);
    }
}

/// <summary>
/// A PointerCache variant that also supports GameObj keys.
/// </summary>
/// <typeparam name="TValue">The type of values to store.</typeparam>
public class GameObjCache<TValue> : PointerCache<TValue>
{
    /// <summary>
    /// Create a new GameObjCache.
    /// </summary>
    /// <param name="concurrent">If true, uses ConcurrentDictionary for thread safety.</param>
    public GameObjCache(bool concurrent = false) : base(concurrent) { }

    /// <summary>
    /// Check if the cache contains a GameObj.
    /// </summary>
    public bool Contains(GameObj key) => Contains(key.Pointer);

    /// <summary>
    /// Set a value for a GameObj key.
    /// </summary>
    public void Set(GameObj key, TValue value) => Set(key.Pointer, value);

    /// <summary>
    /// Try to get a value for a GameObj key.
    /// </summary>
    public bool TryGet(GameObj key, out TValue value) => TryGet(key.Pointer, out value);

    /// <summary>
    /// Get a value for a GameObj key, or return the default value if not found.
    /// </summary>
    public TValue Get(GameObj key, TValue defaultValue = default) => Get(key.Pointer, defaultValue);

    /// <summary>
    /// Get an existing value or set and return a new value.
    /// </summary>
    public TValue GetOrSet(GameObj key, Func<TValue> factory) => GetOrSet(key.Pointer, factory);

    /// <summary>
    /// Get an existing value or set and return a new value.
    /// </summary>
    public TValue GetOrSet(GameObj key, TValue value) => GetOrSet(key.Pointer, value);

    /// <summary>
    /// Remove an entry by GameObj key.
    /// </summary>
    public bool Remove(GameObj key) => Remove(key.Pointer);

    /// <summary>
    /// Indexer for direct access via GameObj.
    /// </summary>
    public TValue this[GameObj key]
    {
        get => Get(key.Pointer);
        set => Set(key.Pointer, value);
    }
}
