using System;
using System.Collections.Generic;
using Il2CppMenace.Tools;
using Il2CppMenace.Strategy.Missions;
using Il2CppMenace.Tactical;

namespace Menace.SDK;

/// <summary>
/// Runtime template lifecycle API.
///
/// Provides guaranteed loading of DataTemplate types before they are queried.
/// Use <see cref="EnsureLoaded{T}"/> before any <c>DataTemplateLoader.Get&lt;T&gt;</c>
/// or <c>GameQuery.FindByName&lt;T&gt;</c> call to ensure the type has been
/// materialised into memory.
///
/// Clone support is not yet part of this API. The complete clone pipeline —
/// including m_TemplateMaps registration, m_TemplateArrays extension, and
/// ancestor mirroring — is implemented in ModpackLoader (kind of) and will be 
/// promoted here once that research is verified and stable.
/// </summary>
public static class Templates
{
    internal static StringFieldHandle<Il2CppMenace.Tools.DataTemplate> _hDataTemplateId;
    private static readonly HashSet<Type> _loadedTypes = new();

    internal static void Initialize()
    {
        GameState.SceneLoaded += _ => ResolveHandles();
    }

    private static void ResolveHandles()
    {
        _hDataTemplateId = GameObj<Il2CppMenace.Tools.DataTemplate>.ResolveStringField(x => x.m_ID);
    }

    private static void EnsureLoaded<T>() where T : DataTemplate
    {
        if (!_loadedTypes.Add(typeof(T))) return;
        var result = DataTemplateLoader.GetAll<T>();
        if (result == null || result.Count == 0)
            ModError.WarnInternal("Templates.EnsureLoaded",
                $"No templates loaded for {typeof(T).Name}");
    }
    /// <summary>
    /// Returns the template with the given ID.
    /// Throws <see cref="TemplateNotFoundException"/> if not found.
    /// Use <see cref="TryGet{T}"/> when absence is a legitimate runtime condition.
    /// </summary>
    public static T FindByID<T>(string id) where T : DataTemplate
    {
        EnsureLoaded<T>();
        var result = DataTemplateLoader.Get<T>(id, false);
        if (result == null)
            ModError.WarnInternal("Templates.FindByID",
                $"{typeof(T).Name} '{id}' not found");
        return result;
    }

    /// <summary>
    /// Returns true and populates <paramref name="template"/> if found.
    /// Logs a warning on miss — use this when absence is a legitimate runtime condition
    /// that requires branch logic. If absence is always a bug, use <see cref="FindByID{T}"/> instead.
    /// </summary>
    public static bool TryGet<T>(string id, out T template) where T : DataTemplate
    {
        EnsureLoaded<T>();
        if (DataTemplateLoader.TryGet<T>(id, out template))
            return true;

        ModError.WarnInternal("Templates.TryGet",
            $"{typeof(T).Name} '{id}' not found");
        return false;
    }

    /// <summary>
    /// Returns all loaded templates of type <typeparamref name="T"/>.
    /// </summary>
    public static IReadOnlyCollection<T> FindAll<T>() where T : DataTemplate
    {
        EnsureLoaded<T>();
        return (IReadOnlyCollection<T>)DataTemplateLoader.GetAll<T>();
    }
}