using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppTactical;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for Line of Sight and visibility operations.
/// Provides safe access to LOS checks, detection, and visibility management.
///
/// Based on reverse engineering findings:
/// - LineOfSight.HasLineOfSight(from, to, flags) @ 0x18051df40
/// - Tile.HasLineOfSightTo(target, flags) @ 0x180681d70
/// - Actor.HasLineOfSightTo(entity, wasDetected, fromTile, toTile) @ 0x1805dfa10
/// - EntityProperties.GetVision() @ 0x18060c7b0
/// - EntityProperties.GetDetection() @ 0x18060bd90
/// </summary>
public static class LineOfSight
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // Entity fields
    private static FieldHandle<Il2CppMenace.Tactical.Entity, Il2CppMenace.Tactical.Visibility> _hVisibilityToPlayer;

    // Actor fields
    private static FieldHandle<Il2CppMenace.Tactical.Actor, bool> _hRevealed;

    // ═══════════════════════════════════════════════════════════════════
    //  Initialisation — wire up to GameState.SceneLoaded
    // ═══════════════════════════════════════════════════════════════════

    private static bool _handlesResolved = false;

    internal static void Initialize()
    {
        GameState.SceneLoaded += _ => ResolveHandles();
    }

    private static void ResolveHandles()
    {
        if (_handlesResolved) return;

        try
        {
            _hVisibilityToPlayer = GameObj<Il2CppMenace.Tactical.Entity>.ResolveField(x => x.VisibilityToPlayer);
            _hRevealed = GameObj<Il2CppMenace.Tactical.Actor>.ResolveField(x => x.Revealed);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.ResolveHandles: Field handle resolution failed", ex);
        }
    }

    public class VisibilityInfo
    {
        public Visibility State { get; set; }
        public bool IsVisible { get; set; }
        public bool IsRevealed { get; set; }
        public int Vision { get; set; }
        public int Detection { get; set; }
        public int Concealment { get; set; }
    }

    /// <summary>
    /// Check if there is clear line of sight between two tiles.
    /// </summary>
    public static bool HasLineOfSight(int fromX, int fromY, int toX, int toY)
    {
        var fromTile = TileMap.GetTile(fromX, fromY);
        var toTile = TileMap.GetTile(toX, toY);
        return HasLineOfSight(fromTile, toTile);
    }

    /// <summary>
    /// Check if there is clear line of sight between two tiles.
    /// </summary>
    public static bool HasLineOfSight(GameObj fromTile, GameObj toTile, LineOfSightFlags flags = LineOfSightFlags.Default)
    {
        if (fromTile.IsNull || toTile.IsNull) return false;
        if (fromTile.Pointer == toTile.Pointer) return true;

        try
        {
            return (bool)GameMethod.Call<Tile>(fromTile, x => x.HasLineOfSightTo(default, default), new object[] { toTile.As<Tile>(), flags });
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.HasLineOfSight: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if an actor can see a target entity (includes detection vs concealment).
    /// </summary>
    public static bool CanActorSee(GameObj actor, GameObj target)
    {
        if (actor.IsNull || target.IsNull) return false;

        try
        {
            return (bool)GameMethod.Call<Actor>(actor, x => x.HasLineOfSightTo(default, default, default, default), new object[] { target.As<Entity>(), false, null, null });
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.CanActorSee: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the visibility state of an entity.
    /// </summary>
    public static Visibility GetVisibilityState(GameObj entity)
    {
        if (entity.IsNull) return Visibility.Unset;

        _hVisibilityToPlayer.TryRead(GameObj<Entity>.Wrap(entity), out var state);
        return state;
    }

    /// <summary>
    /// Check if an entity is currently visible to the player.
    /// </summary>
    public static bool IsVisibleToPlayer(GameObj entity)
    {
        return GetVisibilityState(entity) == Visibility.Visible;
    }

    /// <summary>
    /// Check if an actor is revealed (always visible when in range).
    /// </summary>
    public static bool IsRevealed(GameObj actor)
    {
        if (actor.IsNull) return false;

        _hRevealed.TryRead(GameObj<Actor>.Wrap(actor), out var revealed);
        return revealed;
    }

    /// <summary>
    /// Get vision range for an entity.
    /// </summary>
    /// <summary>
    /// Get vision range for an entity.
    /// </summary>
    public static int GetVision(GameObj entity)
    {
        if (entity.IsNull) return 0;

        try
        {
            var props = GameMethod.Call<Entity>(entity, x => x.GetCurrentProperties());
            if (props == null) return 0;

            var propsObj = GameObj.FromPointer(((Il2CppObjectBase)props).Pointer);
            return GameMethod.CallInt<EntityProperties>(propsObj, x => x.GetVision());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.GetVision: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get detection stat for an entity.
    /// </summary>
    public static int GetDetection(GameObj entity)
    {
        if (entity.IsNull) return 0;

        try
        {
            var props = GameMethod.Call<Entity>(entity, x => x.GetCurrentProperties());
            if (props == null) return 0;

            var propsObj = GameObj.FromPointer(((Il2CppObjectBase)props).Pointer);
            return GameMethod.CallInt<EntityProperties>(propsObj, x => x.GetDetection());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.GetDetection: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get concealment stat for an entity.
    /// </summary>
    public static int GetConcealment(GameObj entity)
    {
        if (entity.IsNull) return 0;

        try
        {
            var props = GameMethod.Call<Entity>(entity, x => x.GetCurrentProperties());
            if (props == null) return 0;

            var propsObj = GameObj.FromPointer(((Il2CppObjectBase)props).Pointer);
            return GameMethod.CallInt<EntityProperties>(propsObj, x => x.GetConcealment());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.GetConcealment: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get visibility info for an entity.
    /// </summary>
    public static VisibilityInfo GetVisibilityInfo(GameObj entity)
    {
        if (entity.IsNull) return null;

        try
        {
            return new VisibilityInfo
            {
                State = GetVisibilityState(entity),
                IsVisible = IsVisibleToPlayer(entity),
                IsRevealed = IsRevealed(entity),
                Vision = GetVision(entity),
                Detection = GetDetection(entity),
                Concealment = GetConcealment(entity)
            };
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.GetVisibilityInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get all tiles visible from a position within a given range.
    /// </summary>
    public static List<(int x, int z)> GetVisibleTiles(int centerX, int centerZ, int range)
    {
        var result = new List<(int x, int z)>();

        try
        {
            var centerTile = TileMap.GetTile(centerX, centerZ);
            if (centerTile.IsNull) return result;

            var mapInfo = TileMap.GetMapInfo();
            if (mapInfo == null) return result;

            for (int x = Math.Max(0, centerX - range); x <= Math.Min(mapInfo.SizeX - 1, centerX + range); x++)
            {
                for (int z = Math.Max(0, centerZ - range); z <= Math.Min(mapInfo.SizeZ - 1, centerZ + range); z++)
                {
                    int dx = x - centerX;
                    int dz = z - centerZ;
                    if (dx * dx + dz * dz > range * range) continue;

                    if (HasLineOfSight(centerX, centerZ, x, z))
                        result.Add((x, z));
                }
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Error("LineOfSight.GetVisibleTiles: Failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Register console commands for LineOfSight SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("los", "<x1> <y1> <x2> <y2>", "Check line of sight between tiles", args =>
        {
            if (args.Length < 4)
                return "Usage: los <x1> <y1> <x2> <y2>";
            if (!int.TryParse(args[0], out int x1) || !int.TryParse(args[1], out int y1) ||
                !int.TryParse(args[2], out int x2) || !int.TryParse(args[3], out int y2))
                return "Invalid coordinates";

            var hasLos = HasLineOfSight(x1, y1, x2, y2);
            var dist = TileMap.GetDistance(x1, y1, x2, y2);

            return $"LOS from ({x1},{y1}) to ({x2},{y2}): {(hasLos ? "Clear" : "Blocked")}\n" +
                   $"Distance: {dist:F1}";
        });

        DevConsole.RegisterCommand("visibility", "", "Show visibility info for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var info = GetVisibilityInfo(actor);
            if (info == null) return "Could not get visibility info";

            return $"Visibility State: {info.State}\n" +
                   $"Is Visible: {info.IsVisible}, Revealed: {info.IsRevealed}\n" +
                   $"Vision: {info.Vision}, Detection: {info.Detection}\n" +
                   $"Concealment: {info.Concealment}";
        });

        DevConsole.RegisterCommand("vision", "", "Get vision range for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            return $"Vision: {GetVision(actor)}, Detection: {GetDetection(actor)}, Concealment: {GetConcealment(actor)}";
        });

        DevConsole.RegisterCommand("cansee", "<target_name>", "Check if selected actor can see target", args =>
        {
            if (args.Length == 0)
                return "Usage: cansee <target_name>";

            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var targetName = string.Join(" ", args);
            var target = GameQuery.FindByName<Actor>(targetName);
            if (target == null)
                return $"Target '{targetName}' not found";

            var canSee = CanActorSee(actor, new GameObj(target.Pointer));
            return $"Can see '{targetName}': {canSee}";
        });

        DevConsole.RegisterCommand("visibletiles", "<range>", "Count visible tiles from selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            int range = 10;
            if (args.Length > 0 && int.TryParse(args[0], out int r))
                range = r;

            var pos = EntityMovement.GetPosition(actor);
            if (!pos.HasValue)
                return "Could not get actor position";

            var visibleTiles = GetVisibleTiles(pos.Value.x, pos.Value.y, range);
            return $"Visible tiles within range {range}: {visibleTiles.Count}";
        });
    }
}
