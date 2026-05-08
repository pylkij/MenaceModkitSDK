#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Menace.SDK;

namespace Menace.ModpackLoader.TemplateLoading;

/// <summary>
/// Patches to fix broken template loading.
/// Based on diagnostic findings, this patches GetBaseFolder() to return correct paths
/// for template types that have null/empty/incorrect resource paths.
/// </summary>
public static class TemplateLoadingFixes
{
    private static bool _patchesApplied = false;

    // Known path fixes discovered through diagnostics
    // Format: TypeName -> ResourcePath
    // Populate this based on debug.test_all_templates results
    private static readonly Dictionary<string, string> KnownPathFixes = new()
    {
        // Example entries (to be filled in after running diagnostics):
        // { "PerkTree", "Data/Perks/Trees" },
        // { "PerkNode", "Data/Perks/Nodes" },
        // { "OperationTemplate", "Data/Operations" },
    };

    // Guessed paths for types with no path
    // NOTE: As of 2026-03-04 validation, all 77 template types load correctly with their proper names.
    // The previous list used incorrect type names (PerkTree→PerkTreeTemplate, CharacterTemplate→UnitLeaderTemplate, etc.)
    // Keeping this dictionary for potential future fixes, but currently no types need path guessing.
    private static readonly Dictionary<string, string> PathGuesses = new()
    {
        // Add entries here if you discover types with null/empty paths via debug.test_all_templates
    };

    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_patchesApplied)
            return;

        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                SdkLogger.Warning("[TemplateLoadingFixes] Assembly-CSharp not found");
                return;
            }

            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                SdkLogger.Warning("[TemplateLoadingFixes] DataTemplateLoader not found");
                return;
            }

            // Patch GetBaseFolder to apply fixes
            var getBaseFolderMethod = loaderType.GetMethod("GetBaseFolder",
                BindingFlags.Public | BindingFlags.Static);

            if (getBaseFolderMethod != null)
            {
                harmony.Patch(getBaseFolderMethod,
                    postfix: new HarmonyMethod(typeof(TemplateLoadingFixes),
                        nameof(GetBaseFolderPostfix)));
                SdkLogger.Msg("[TemplateLoadingFixes] Patched GetBaseFolder");
            }

            _patchesApplied = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TemplateLoadingFixes] Failed to apply patches: {ex.Message}");
        }
    }

    private static void GetBaseFolderPostfix(Type _type, ref string __result)
    {
        try
        {
            var typeName = _type?.Name;
            if (string.IsNullOrEmpty(typeName))
                return;

            // If result is already valid, don't override
            if (!string.IsNullOrEmpty(__result))
                return;

            // Try known fixes first
            if (KnownPathFixes.TryGetValue(typeName, out var fixedPath))
            {
                __result = fixedPath;
                SdkLogger.Msg($"[TemplateLoadingFixes] Applied known fix: {typeName} -> {fixedPath}");
                return;
            }

            // Fall back to guessed paths
            if (PathGuesses.TryGetValue(typeName, out var guessedPath))
            {
                __result = guessedPath;
                SdkLogger.Msg($"[TemplateLoadingFixes] Applied guessed path: {typeName} -> {guessedPath}");
                return;
            }

            // Log types we don't have fixes for yet
            SdkLogger.Warning($"[TemplateLoadingFixes] No fix for type '{typeName}' with null/empty path");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TemplateLoadingFixes] GetBaseFolderPostfix error: {ex.Message}");
        }
    }

    /// <summary>
    /// Add a known path fix for a template type.
    /// Use this after running diagnostics to register correct paths.
    /// </summary>
    public static void AddPathFix(string typeName, string resourcePath)
    {
        if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(resourcePath))
            return;

        KnownPathFixes[typeName] = resourcePath;
        SdkLogger.Msg($"[TemplateLoadingFixes] Registered path fix: {typeName} -> {resourcePath}");
    }

    /// <summary>
    /// Get current known fixes (for diagnostic reporting).
    /// </summary>
    public static Dictionary<string, string> GetKnownFixes()
    {
        return new Dictionary<string, string>(KnownPathFixes);
    }

    /// <summary>
    /// Get guessed paths (for diagnostic reporting).
    /// </summary>
    public static Dictionary<string, string> GetGuessedPaths()
    {
        return new Dictionary<string, string>(PathGuesses);
    }
}
