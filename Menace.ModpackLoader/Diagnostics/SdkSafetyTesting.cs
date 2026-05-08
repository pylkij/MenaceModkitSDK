#nullable disable
using System;
using System.Text;
using Menace.SDK;
using Menace.SDK.Repl;

namespace Menace.ModpackLoader.Diagnostics;

/// <summary>
/// Safety testing for SDK methods that may crash in certain game states.
/// Tests which methods work in which modes (main menu, strategy, tactical).
/// </summary>
public static class SdkSafetyTesting
{
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("debug.test_tilemap", "",
            "Test which TileMap SDK methods work in current mode", _ =>
        {
            return TestTileMapMethods();
        });

        DevConsole.RegisterCommand("debug.test_sdk_methods", "",
            "Test all SDK methods for safety in current mode", _ =>
        {
            return TestAllSdkMethods();
        });

        DevConsole.RegisterCommand("debug.test_templates", "",
            "Test Templates SDK methods", _ =>
        {
            return TestTemplatesMethods();
        });
    }

    private static string TestTileMapMethods()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== TILEMAP SDK SAFETY TEST ===");
        sb.AppendLine($"Current Scene: {GameState.CurrentScene}");
        sb.AppendLine($"Is Tactical: {GameState.IsTactical}");
        sb.AppendLine();

        // Test GetMapInfo()
        sb.AppendLine("Testing TileMap.GetMapInfo()...");
        try
        {
            var mapInfo = TileMap.GetMapInfo();
            if (mapInfo != null)
            {
                sb.AppendLine($"  ✓ SUCCESS - Width: {mapInfo.Width}, Height: {mapInfo.Height}");
            }
            else
            {
                sb.AppendLine("  ✗ FAILED - Returned null");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  ✗ CRASHED - {ex.GetType().Name}: {ex.Message}");
        }

        // Test TileToWorld()
        sb.AppendLine("Testing TileMap.TileToWorld(0, 0)...");
        try
        {
            var worldPos = TileMap.TileToWorld(0, 0);
            sb.AppendLine($"  ✓ SUCCESS - Position: ({worldPos.x}, {worldPos.y}, {worldPos.z})");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  ✗ CRASHED - {ex.GetType().Name}: {ex.Message}");
        }

        // Test WorldToTile()
        sb.AppendLine("Testing TileMap.WorldToTile(Vector3.zero)...");
        try
        {
            var worldPos = UnityEngine.Vector3.zero;
            var tilePos = TileMap.WorldToTile(worldPos);
            sb.AppendLine($"  ✓ SUCCESS - Tile: ({tilePos.x}, {tilePos.z})");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  ✗ CRASHED - {ex.GetType().Name}: {ex.Message}");
        }

        // Test GetTile()
        sb.AppendLine("Testing TileMap.GetTile(0, 0)...");
        try
        {
            var tile = TileMap.GetTile(0, 0);
            if (!tile.IsNull)
            {
                sb.AppendLine($"  ✓ SUCCESS - Tile exists");
            }
            else
            {
                sb.AppendLine("  ○ SUCCESS - No tile at (0,0) or not in tactical");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  ✗ CRASHED - {ex.GetType().Name}: {ex.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("RECOMMENDATION:");
        if (GameState.IsTactical)
        {
            sb.AppendLine("  In tactical mode - all methods should work if map is loaded");
        }
        else
        {
            sb.AppendLine("  NOT in tactical mode - TileMap methods expected to fail");
            sb.AppendLine("  Use GameState.IsTactical check before calling these methods");
        }

        return sb.ToString();
    }

    private static string TestTemplatesMethods()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== TEMPLATES SDK SAFETY TEST ===");
        sb.AppendLine($"Current Scene: {GameState.CurrentScene}");
        sb.AppendLine();

        // Test FindAll with a known template type
        sb.AppendLine("Testing Templates.FindAll('WeaponTemplate')...");
        try
        {
            var weapons = Templates.FindAll("WeaponTemplate");
            sb.AppendLine($"  ✓ SUCCESS - Found {weapons.Length} weapons");

            if (weapons.Length > 0)
            {
                var sample = weapons[0];
                var sampleName = sample.ReadString("m_Name") ?? "unknown";
                sb.AppendLine($"  Sample: {sampleName}");

                // Test ReadField
                sb.AppendLine("  Testing Templates.ReadField(weapon, 'DisplayName')...");
                try
                {
                    var displayName = Templates.ReadField(sample, "DisplayName");
                    sb.AppendLine($"    ✓ SUCCESS - DisplayName: {displayName}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    ✗ FAILED - {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  ✗ CRASHED - {ex.GetType().Name}: {ex.Message}");
        }

        // Test Find
        sb.AppendLine();
        sb.AppendLine("Testing Templates.Find('ArmorTemplate', 'known_item')...");
        try
        {
            var equipment = Templates.FindAll("ArmorTemplate");
            if (equipment.Length > 0)
            {
                var firstItem = equipment[0];
                var firstItemName = firstItem.ReadString("m_Name");
                var found = Templates.Find("ArmorTemplate", firstItemName);
                if (!found.IsNull)
                {
                    var foundName = found.ReadString("m_Name");
                    sb.AppendLine($"  ✓ SUCCESS - Found: {foundName}");
                }
                else
                {
                    sb.AppendLine("  ✗ FAILED - Could not find item by name");
                }
            }
            else
            {
                sb.AppendLine("  ○ SKIPPED - No equipment templates loaded");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  ✗ CRASHED - {ex.GetType().Name}: {ex.Message}");
        }

        return sb.ToString();
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
        sb.AppendLine(TestTileMapMethods());
        sb.AppendLine();

        // Templates tests
        sb.AppendLine("[Templates SDK]");
        sb.AppendLine(TestTemplatesMethods());
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
