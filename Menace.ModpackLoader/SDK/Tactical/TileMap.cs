using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

using Menace.SDK.Internal;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for tile and map operations in tactical combat.
/// Provides safe access to tile queries, cover checks, visibility, and map traversal.
///
/// Based on reverse engineering findings:
/// - Map : BaseMap&lt;Tile&gt; with max 42x42 tiles
/// - Tile.GetCover(Direction, Entity, EntityProperties, bool realCover) @ 0x180680b20
/// - Tile.HasActor() @ 0x180681cd0
/// - Tile.HasLineOfSightTo(other, flags) @ 0x180681d70
/// - Tile.IsVisibleToFaction(factionId) @ 0x180682140
/// - Tile.IsVisibleToPlayer() for player visibility check
/// - Map.GetTile(x, z) via Tiles array
/// - Map.GetTileAtPos(Vector3) for world position lookup
/// - TacticalManager.Instance.Map @ +0x28
///
/// COORDINATE SYSTEM NOTE:
/// The game uses X/Z coordinates for tiles (Y is elevation/height).
/// TileInfo.X = game's X coordinate
/// TileInfo.Z = game's Z coordinate (formerly named Y in SDK)
/// </summary>
public static partial class TileMap
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // BaseTile fields
    private static FieldHandle<Il2CppMenace.Tactical.BaseTile, int> _hTileX;
    private static FieldHandle<Il2CppMenace.Tactical.BaseTile, int> _hTileZ;
    private static FieldHandle<Il2CppMenace.Tactical.BaseTile, float> _hTileElevation;
    private static FieldHandle<Il2CppMenace.Tactical.BaseTile, uint> _hTileFlags;
    private static FieldHandle<Il2CppMenace.Tactical.BaseTile, Il2CppMenace.Tactical.CoverType> _hTileInherentCover;

    // Tile fields
    private static FieldHandle<Il2CppMenace.Tactical.Tile, ulong> _hTileVisibleMask;
    private static FieldHandle<Il2CppMenace.Tactical.Tile, byte> _hTileBlockingLOSStack;
    private static ObjFieldHandle<Il2CppMenace.Tactical.Tile, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Tactical.TileEffects.TileEffectHandler>> _hTileEffects;

    // BaseMap fields
    private static FieldHandle<Il2CppMenace.Tactical.BaseMap, int> _hMapSizeX;
    private static FieldHandle<Il2CppMenace.Tactical.BaseMap, int> _hMapSizeZ;

    // Map fields
    private static FieldHandle<Il2CppMenace.Tactical.Map, bool> _hMapIsUsingFogOfWar;
    private static FieldHandle<Il2CppMenace.Tactical.Map, bool> _hMapIsReady;

    // TacticalManager fields
    private static ObjFieldHandle<Il2CppMenace.Tactical.TacticalManager, Il2CppMenace.Tactical.Map> _hTacticalManagerMap;

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
            // BaseTile fields
            _hTileX = GameObj<Il2CppMenace.Tactical.BaseTile>.ResolveField(x => x.m_X);
            _hTileZ = GameObj<Il2CppMenace.Tactical.BaseTile>.ResolveField(x => x.m_Z);
            _hTileElevation = GameObj<Il2CppMenace.Tactical.BaseTile>.ResolveField(x => x.m_Elevation);
            _hTileFlags = GameObj<Il2CppMenace.Tactical.BaseTile>.ResolveField(x => x.m_Flags);
            _hTileInherentCover = GameObj<Il2CppMenace.Tactical.BaseTile>.ResolveField(x => x.m_InherentCover);

            // Tile fields
            _hTileVisibleMask = GameObj<Il2CppMenace.Tactical.Tile>.ResolveField(x => x.m_VisibleMask);
            _hTileBlockingLOSStack = GameObj<Il2CppMenace.Tactical.Tile>.ResolveField(x => x.m_BlockingLineOfSightStack);
            _hTileEffects = GameObj<Il2CppMenace.Tactical.Tile>.ResolveObjField(x => x.m_Effects);

            // BaseMap fields
            _hMapSizeX = GameObj<Il2CppMenace.Tactical.BaseMap>.ResolveField(x => x.m_MapSizeX);
            _hMapSizeZ = GameObj<Il2CppMenace.Tactical.BaseMap>.ResolveField(x => x.m_MapSizeZ);

            // Map fields
            _hMapIsUsingFogOfWar = GameObj<Il2CppMenace.Tactical.Map>.ResolveField(x => x.m_IsUsingFogOfWar);
            _hMapIsReady = GameObj<Il2CppMenace.Tactical.Map>.ResolveField(x => x.m_IsReady);

            // TacticalManager fields
            _hTacticalManagerMap = GameObj<Il2CppMenace.Tactical.TacticalManager>.ResolveObjField(x => x.m_Map);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.ResolveHandles: Field handle resolution failed", ex);
        }
    }

    private static class TileFlags
    {
        public const int Blocked = (int)Il2CppMenace.Tactical.TileFlag.Blocked;
        public const int TemporarilyOccupied = (int)Il2CppMenace.Tactical.TileFlag.TemporarilyOccupied;
        public const int Isolated = (int)Il2CppMenace.Tactical.TileFlag.Isolated;
        public const int FogOfWar = (int)Il2CppMenace.Tactical.TileFlag.FogOfWar;
        public const int LOSBlockedByHalfcover = (int)Il2CppMenace.Tactical.TileFlag.LOSBlockedByHalfcover;
    }

    //  Map constants
    public const int MAX_MAP_SIZE = 42;
    public const float TILE_SIZE = 8.0f;

    /// <summary>
    /// Get a tile at specific coordinates.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static GameObj GetTile(int x, int z)
    {
        try
        {
            var mapObj = GetMap();
            if (mapObj.IsNull) return GameObj.Null;

            if (!GameObj<Il2CppMenace.Tactical.Map>.TryWrap(mapObj, out var typedMap))
                return GameObj.Null;

            var tileRaw = GameMethod.Call<Il2CppMenace.Tactical.Map>(
                typedMap.AsManaged(), m => m.GetTile(x, z));
            if (tileRaw is not Il2CppMenace.Tactical.Tile tile) return GameObj.Null;

            return GameObj<Il2CppMenace.Tactical.Tile>.Wrap(tile.Pointer).Untyped;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TileMap.GetTile] Failed for ({x}, {z})", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get a tile at the given grid coordinates. Alias for GetTile.
    /// </summary>
    public static GameObj GetTileAt(int x, int z) => GetTile(x, z);

    /// <summary>
    /// Get tile at a world position.
    /// Uses native Map.GetTileAtPos when available for accurate results.
    /// </summary>
    public static GameObj GetTileAtWorldPos(Vector3 worldPos)
    {
        try
        {
            var mapObj = GetMap();
            if (mapObj.IsNull) return GameObj.Null;

            if (!GameObj<Il2CppMenace.Tactical.Map>.TryWrap(mapObj, out var typedMap))
                return GameObj.Null;

            var tileRaw = GameMethod.Call<Il2CppMenace.Tactical.Map>(
                typedMap.AsManaged(), m => m.GetTileAtPos(worldPos));
            if (tileRaw is not Il2CppMenace.Tactical.Tile tile) return GameObj.Null;

            return GameObj<Il2CppMenace.Tactical.Tile>.Wrap(tile.Pointer).Untyped;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetTileAtWorldPos: Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get detailed information about a tile.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static TileInfo GetTileInfo(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetTileInfo(tile);
    }

    /// <summary>
    /// Get detailed information about a tile.
    /// </summary>
    public static TileInfo GetTileInfo(GameObj tile)
    {
        if (tile.IsNull) return null;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return null;

            var managed = typedTile.AsManaged();

            var info = new TileInfo
            {
                Pointer = tile.Pointer,
                X = tile.ReadInt(0x10),
                Z = tile.ReadInt(0x14),
                Elevation = tile.ReadFloat(0x18),
            };

            var flags = tile.ReadInt(0x1C);
            info.IsBlocked = (flags & TileFlags.Blocked) != 0;
            info.IsIsolated = (flags & TileFlags.Isolated) != 0;
            info.IsTemporarilyOccupied = (flags & TileFlags.TemporarilyOccupied) != 0;
            info.IsLOSBlockedByHalfcover = (flags & TileFlags.LOSBlockedByHalfcover) != 0;
            info.IsInFogOfWar = (flags & TileFlags.FogOfWar) != 0;

            info.InherentCover = tile.ReadInt(0x20);

            if (_hTileVisibleMask.TryRead(typedTile, out var mask))
                info.VisibleMask = mask;

            info.IsVisibleToPlayer = GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(managed, t => t.IsVisibleToPlayer());
            info.HasActor = GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(managed, t => t.HasActor());
            info.HasStructure = GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(managed, t => t.HasStructure());
            info.HasEffects = GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(managed, t => t.HasEffect());
            info.HasCover = GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(managed, t => t.HasCover());
            info.HasHalfCover = GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(managed, t => t.HasHalfCover());
            info.Concealment = GameMethod.CallInt<Il2CppMenace.Tactical.Tile>(managed, t => t.GetConcealment());

            if (info.HasActor)
            {
                var actorRaw = GameMethod.Call<Il2CppMenace.Tactical.Tile>(managed, t => t.GetActor());
                if (actorRaw is Il2CppMenace.Tactical.Actor actor)
                    info.ActorName = GameObj<Il2CppMenace.Tactical.Actor>.Wrap(actor.Pointer).Untyped.GetName();
            }

            info.WorldPos = GameMethod.Call<Il2CppMenace.Tactical.BaseTile>(
                managed, t => t.GetPos()) is Vector3 pos ? pos : default;

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetTileInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Convert tile coordinates to world position.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate</param>
    /// <param name="elevation">Elevation (game's Y axis)</param>
    public static Vector3 TileToWorld(int x, int z, float elevation = 0f)
    {
        return new Vector3(
            x * TILE_SIZE + TILE_SIZE / 2f,
            elevation,
            z * TILE_SIZE + TILE_SIZE / 2f
        );
    }

    /// <summary>
    /// Convert world position to tile coordinates.
    /// </summary>
    /// <returns>Tuple of (x, z) tile coordinates</returns>
    public static (int x, int z) WorldToTile(Vector3 worldPos)
    {
        int x = (int)(worldPos.x / TILE_SIZE);
        int z = (int)(worldPos.z / TILE_SIZE);
        return (x, z);
    }

    /// <summary>
    /// Get direction name from direction index.
    /// </summary>
    public static string GetDirectionName(int direction)
    {
        if (direction < 0 || direction >= Dir.Count) return "Unknown";
        return ((Il2CppMenace.Tactical.Direction)direction).ToString();
    }

    /// <summary>
    /// Get cover type name.
    /// </summary>
    public static string GetCoverName(int coverType)
    {
        if (coverType < Cover.None || coverType > Cover.Heavy) return "Unknown";
        return ((Il2CppMenace.Tactical.CoverType)coverType).ToString();
    }

    /// <summary>
    /// Register console commands for TileMap SDK.
    /// Called by DevConsole during initialization.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // tile <x> <z> - Get tile info
        DevConsole.RegisterCommand("tile", "<x> <z>", "Get tile information", args =>
        {
            if (args.Length < 2)
                return "Usage: tile <x> <z>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int z))
                return "Invalid coordinates";

            var info = GetTileInfo(x, z);
            if (info == null)
                return $"Tile at ({x}, {z}) not found";

            var lines = new List<string>
        {
            $"Tile ({info.X}, {info.Z}) - Elevation: {info.Elevation:F1}",
            $"Blocked: {info.IsBlocked}, Isolated: {info.IsIsolated}, FogOfWar: {info.IsInFogOfWar}",
            $"Visible: {info.IsVisibleToPlayer}, LOS Blocked: {info.IsLOSBlockedByHalfcover}",
            $"HasActor: {info.HasActor}" + (info.HasActor ? $" ({info.ActorName})" : ""),
            $"HasStructure: {info.HasStructure}, Effects: {info.HasEffects}",
            $"Cover: {GetCoverName(info.InherentCover)}, Concealment: {info.Concealment}",
            $"WorldPos: {info.WorldPos}"
        };
            return string.Join("\n", lines);
        });

        // cover <x> <z> - Get cover values
        DevConsole.RegisterCommand("cover", "<x> <z>", "Get cover values for a tile", args =>
        {
            if (args.Length < 2)
                return "Usage: cover <x> <z>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int z))
                return "Invalid coordinates";

            var cover = GetAllCover(x, z);
            var lines = new List<string> { $"Cover at ({x}, {z}):" };
            for (int dir = 0; dir < Dir.Count; dir++)
                lines.Add($"  {GetDirectionName(dir)}: {GetCoverName(cover[dir])}");

            return string.Join("\n", lines);
        });

        // mapinfo - Get map information
        DevConsole.RegisterCommand("mapinfo", "", "Get current map information", args =>
        {
            var info = GetMapInfo();
            if (info == null)
                return "No map available";

            return $"Map: {info.SizeX}x{info.SizeZ} tiles\n" +
                   $"Fog of War: {info.IsUsingFogOfWar}\n" +
                   $"Ready: {info.IsReady}\n" +
                   $"Center: {info.CenterWorldPos}";
        });

        // blocked <x> <z> - Check if tile is blocked
        DevConsole.RegisterCommand("blocked", "<x> <z>", "Check if tile is blocked", args =>
        {
            if (args.Length < 2)
                return "Usage: blocked <x> <z>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int z))
                return "Invalid coordinates";

            return $"Tile ({x}, {z}) blocked: {IsBlocked(x, z)}";
        });

        // visible <x> <z> - Check tile visibility
        DevConsole.RegisterCommand("visible", "<x> <z>", "Check if tile is visible to player", args =>
        {
            if (args.Length < 2)
                return "Usage: visible <x> <z>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int z))
                return "Invalid coordinates";

            return $"Tile ({x}, {z}) visible: {IsVisibleToPlayer(x, z)}";
        });

        // dist <x1> <z1> <x2> <z2> - Get distance between tiles
        DevConsole.RegisterCommand("dist", "<x1> <z1> <x2> <z2>", "Get distance between tiles", args =>
        {
            if (args.Length < 4)
                return "Usage: dist <x1> <z1> <x2> <z2>";
            if (!int.TryParse(args[0], out int x1) || !int.TryParse(args[1], out int z1) ||
                !int.TryParse(args[2], out int x2) || !int.TryParse(args[3], out int z2))
                return "Invalid coordinates";

            var distance = GetDistance(x1, z1, x2, z2);
            var manhattan = GetManhattanDistance(x1, z1, x2, z2);
            var direction = GetDirectionTo(x1, z1, x2, z2);

            return $"Distance from ({x1},{z1}) to ({x2},{z2}):\n" +
                   $"  Distance: {distance}\n" +
                   $"  Manhattan: {manhattan}\n" +
                   $"  Direction: {GetDirectionName(direction)}";
        });

        // whostile <x> <z> - Show who is on a tile
        DevConsole.RegisterCommand("whostile", "<x> <z>", "Show who occupies a tile", args =>
        {
            if (args.Length < 2)
                return "Usage: whostile <x> <z>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int z))
                return "Invalid coordinates";

            if (!HasActor(x, z))
                return $"Tile ({x}, {z}) is empty";

            var actor = GetActorOnTile(x, z);
            if (actor.IsNull)
                return $"Tile ({x}, {z}) has no actor";

            var name = actor.GetName() ?? "<unnamed>";
            return $"Tile ({x}, {z}) occupied by: {name}";
        });
    }
}
