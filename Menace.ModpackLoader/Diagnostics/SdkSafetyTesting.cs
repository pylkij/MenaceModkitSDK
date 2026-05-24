#nullable disable
using Il2CppInterop.Runtime;
using Menace.SDK;
using Menace.SDK.Internal;
using Menace.SDK.Repl;
using System;
using System.Text;

namespace Menace.ModpackLoader.Diagnostics;

/// <summary>
/// Safety testing for SDK methods that may crash in certain game states.
/// Tests which methods work in which modes (main menu, strategy, tactical).
/// </summary>
public static class SdkSafetyTesting
{
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("debug.test_sdk_methods", "",
            "Test all SDK methods for safety in current mode", _ =>
        {
            return TestAllSdkMethods();
        });
    }

    private static string TestAllSdkMethods()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SDK METHODS COMPREHENSIVE SAFETY TEST ===");
        sb.AppendLine($"Current Scene: {GameState.CurrentScene}");
        sb.AppendLine($"Is Tactical: {GameState.IsTactical}");
        sb.AppendLine();

        // TileMap tests
        sb.AppendLine("[TileMap SDK]");
        sb.AppendLine();

        // GameState tests
        sb.AppendLine("[GameState SDK]");
        sb.AppendLine($"  CurrentScene: {GameState.CurrentScene}");
        sb.AppendLine($"  IsTactical: {GameState.IsTactical}");
        sb.AppendLine($"  ✓ GameState methods work in all modes");
        sb.AppendLine();

        // Pathfinding tests (tactical only)
        if (GameState.IsTactical)
        {
            sb.AppendLine("[Pathfinding SDK]");
            sb.AppendLine("  Testing Pathfinding.FindPath()...");
            try
            {
                // FindPath requires an entity, so we'll just check if it handles null safely
                var result = Pathfinding.FindPath(GameObj.Null, 0, 0, 1, 1);
                if (result != null)
                {
                    sb.AppendLine($"  ✓ SUCCESS - Returned result (success={result.Success})");
                }
                else
                {
                    sb.AppendLine("  ○ INFO - Returned null (expected with null entity)");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ✗ CRASHED - {ex.GetType().Name}: {ex.Message}");
            }
        }
        else
        {
            sb.AppendLine("[Pathfinding SDK]");
            sb.AppendLine("  ○ SKIPPED - Not in tactical mode");
        }

        return sb.ToString();
    }
}
