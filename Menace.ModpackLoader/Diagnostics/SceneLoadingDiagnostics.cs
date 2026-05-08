#nullable disable
using System;
using System.Linq;
using System.Text;
using HarmonyLib;
using Menace.SDK;
using Menace.SDK.Repl;
using UnityEngine.SceneManagement;

namespace Menace.ModpackLoader.Diagnostics;

/// <summary>
/// Diagnostics for scene loading/navigation.
/// Patches SceneManager to log scene load attempts and results.
/// Provides commands to inspect current scene and list available scenes.
/// </summary>
public static class SceneLoadingDiagnostics
{
    private static bool _patchesApplied = false;
    private static string _lastSceneLoadAttempt = null;
    private static DateTime _lastSceneLoadTime = DateTime.MinValue;

    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_patchesApplied)
            return;

        try
        {
            // Patch SceneManager.LoadScene(string)
            var loadSceneMethod = typeof(SceneManager).GetMethod("LoadScene",
                new[] { typeof(string) });

            if (loadSceneMethod != null)
            {
                harmony.Patch(loadSceneMethod,
                    prefix: new HarmonyMethod(typeof(SceneLoadingDiagnostics),
                        nameof(LoadScenePrefix)),
                    postfix: new HarmonyMethod(typeof(SceneLoadingDiagnostics),
                        nameof(LoadScenePostfix)));
                SdkLogger.Msg("[SceneDiagnostics] Patched SceneManager.LoadScene");
            }

            // Patch SceneManager.LoadScene(int)
            var loadSceneByIndexMethod = typeof(SceneManager).GetMethod("LoadScene",
                new[] { typeof(int) });

            if (loadSceneByIndexMethod != null)
            {
                harmony.Patch(loadSceneByIndexMethod,
                    prefix: new HarmonyMethod(typeof(SceneLoadingDiagnostics),
                        nameof(LoadSceneByIndexPrefix)));
                SdkLogger.Msg("[SceneDiagnostics] Patched SceneManager.LoadScene(int)");
            }

            _patchesApplied = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[SceneDiagnostics] Failed to apply patches: {ex.Message}");
        }
    }

    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("debug.scene_info", "",
            "Show detailed information about current scene", _ =>
        {
            return GetSceneInfo();
        });

        DevConsole.RegisterCommand("debug.list_scenes", "",
            "List all scenes in build settings", _ =>
        {
            return ListAllScenes();
        });

        DevConsole.RegisterCommand("debug.test_scene_load", "<scene_name>",
            "Test loading a specific scene and report results", args =>
        {
            if (args.Length == 0)
                return "Usage: debug.test_scene_load <scene_name>";

            return TestSceneLoad(args[0]);
        });

        DevConsole.RegisterCommand("debug.last_scene_attempt", "",
            "Show last scene load attempt", _ =>
        {
            if (string.IsNullOrEmpty(_lastSceneLoadAttempt))
                return "No scene load attempts recorded";

            return $"Last attempt: {_lastSceneLoadAttempt} at {_lastSceneLoadTime:HH:mm:ss}";
        });
    }

    private static void LoadScenePrefix(string sceneName)
    {
        try
        {
            _lastSceneLoadAttempt = sceneName;
            _lastSceneLoadTime = DateTime.Now;

            var currentScene = SceneManager.GetActiveScene();
            SdkLogger.Msg($"[SceneDiagnostics] LoadScene('{sceneName}') called");
            SdkLogger.Msg($"[SceneDiagnostics]   Current scene: '{currentScene.name}' (index: {currentScene.buildIndex})");
            SdkLogger.Msg($"[SceneDiagnostics]   Scene count in build: {SceneManager.sceneCountInBuildSettings}");

            // Check if scene exists in build settings
            var sceneExists = false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                if (scenePath.Contains(sceneName))
                {
                    sceneExists = true;
                    SdkLogger.Msg($"[SceneDiagnostics]   Found scene at index {i}: {scenePath}");
                    break;
                }
            }

            if (!sceneExists)
            {
                SdkLogger.Warning($"[SceneDiagnostics]   Scene '{sceneName}' NOT found in build settings!");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[SceneDiagnostics] LoadScenePrefix error: {ex.Message}");
        }
    }

    private static void LoadScenePostfix(string sceneName)
    {
        try
        {
            var activeScene = SceneManager.GetActiveScene();
            SdkLogger.Msg($"[SceneDiagnostics] LoadScene('{sceneName}') completed");
            SdkLogger.Msg($"[SceneDiagnostics]   Active scene now: '{activeScene.name}' (index: {activeScene.buildIndex})");

            if (activeScene.name != sceneName)
            {
                SdkLogger.Warning($"[SceneDiagnostics]   WARNING: Scene did not change! Still on '{activeScene.name}'");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[SceneDiagnostics] LoadScenePostfix error: {ex.Message}");
        }
    }

    private static void LoadSceneByIndexPrefix(int sceneBuildIndex)
    {
        try
        {
            var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
            SdkLogger.Msg($"[SceneDiagnostics] LoadScene({sceneBuildIndex}) called");
            SdkLogger.Msg($"[SceneDiagnostics]   Scene path: {scenePath}");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[SceneDiagnostics] LoadSceneByIndexPrefix error: {ex.Message}");
        }
    }

    private static string GetSceneInfo()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== CURRENT SCENE INFO ===");

            var activeScene = SceneManager.GetActiveScene();
            sb.AppendLine($"Name: {activeScene.name}");
            sb.AppendLine($"Build Index: {activeScene.buildIndex}");
            sb.AppendLine($"Path: {activeScene.path}");
            sb.AppendLine($"Is Loaded: {activeScene.isLoaded}");
            sb.AppendLine($"Root Count: {activeScene.rootCount}");
            sb.AppendLine($"Is Valid: {activeScene.IsValid()}");
            sb.AppendLine();

            sb.AppendLine("GameState Info:");
            sb.AppendLine($"  CurrentScene: {GameState.CurrentScene}");
            sb.AppendLine($"  IsTactical: {GameState.IsTactical}");
            sb.AppendLine();

            sb.AppendLine($"Loaded Scene Count: {SceneManager.loadedSceneCount}");
            sb.AppendLine($"Total Scenes in Build: {SceneManager.sceneCountInBuildSettings}");

            if (!string.IsNullOrEmpty(_lastSceneLoadAttempt))
            {
                sb.AppendLine();
                sb.AppendLine($"Last Load Attempt: {_lastSceneLoadAttempt} at {_lastSceneLoadTime:HH:mm:ss}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting scene info: {ex.Message}";
        }
    }

    private static string ListAllScenes()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ALL SCENES IN BUILD SETTINGS ===");
            sb.AppendLine($"Total: {SceneManager.sceneCountInBuildSettings}");
            sb.AppendLine();

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                var isActive = SceneManager.GetActiveScene().buildIndex == i;

                sb.AppendLine($"{(isActive ? "→" : " ")} [{i}] {sceneName}");
                sb.AppendLine($"      {scenePath}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing scenes: {ex.Message}";
        }
    }

    private static string TestSceneLoad(string sceneName)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== TESTING SCENE LOAD: {sceneName} ===");

            var currentScene = SceneManager.GetActiveScene();
            sb.AppendLine($"Current scene: {currentScene.name}");
            sb.AppendLine();

            sb.AppendLine("Attempting to load scene...");
            SceneManager.LoadScene(sceneName);

            sb.AppendLine("LoadScene() call completed");
            sb.AppendLine("Check debug.scene_info in a moment to see if scene changed");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error during test: {ex.Message}\n{ex.StackTrace}";
        }
    }
}
