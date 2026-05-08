#nullable disable
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Menace.SDK;
using UnityEngine.SceneManagement;

namespace Menace.ModpackLoader.TemplateLoading;

/// <summary>
/// Patches to fix scene loading issues discovered through diagnostics.
/// Populated based on findings from debug.scene_info and debug.test_scene_load.
/// </summary>
public static class SceneLoadingFixes
{
    private static bool _patchesApplied = false;

    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_patchesApplied)
            return;

        try
        {
            // Placeholder for scene loading fixes
            // Based on diagnostic results, we may need to patch:
            // - Scene validation
            // - Scene transition blocking
            // - Scene load completion callbacks

            // Currently no fixes needed - diagnostics will reveal if any are required

            _patchesApplied = true;
            SdkLogger.Msg("[SceneLoadingFixes] Initialized (no fixes currently applied)");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[SceneLoadingFixes] Failed to initialize: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if a scene exists in build settings before attempting to load.
    /// Helper for safe scene navigation.
    /// </summary>
    public static bool SceneExists(string sceneName)
    {
        try
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                if (scenePath.Contains(sceneName))
                    return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[SceneLoadingFixes] SceneExists error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Safely load a scene with validation.
    /// Returns success message or error.
    /// </summary>
    public static string SafeLoadScene(string sceneName)
    {
        try
        {
            if (string.IsNullOrEmpty(sceneName))
                return "ERROR: Scene name cannot be null or empty";

            if (!SceneExists(sceneName))
                return $"ERROR: Scene '{sceneName}' not found in build settings";

            var currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == sceneName)
                return $"Already in scene '{sceneName}'";

            SceneManager.LoadScene(sceneName);
            return $"Loading scene '{sceneName}'...";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to load scene '{sceneName}': {ex.Message}";
        }
    }
}
