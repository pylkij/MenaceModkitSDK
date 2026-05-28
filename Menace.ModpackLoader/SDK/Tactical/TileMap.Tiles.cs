using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

using Menace.SDK.Internal;

namespace Menace.SDK;

public static partial class TileMap
{
    public static class Dir
    {
        public const int North = (int)Il2CppMenace.Tactical.Direction.N;
        public const int Northeast = (int)Il2CppMenace.Tactical.Direction.NE;
        public const int East = (int)Il2CppMenace.Tactical.Direction.E;
        public const int Southeast = (int)Il2CppMenace.Tactical.Direction.SE;
        public const int South = (int)Il2CppMenace.Tactical.Direction.S;
        public const int Southwest = (int)Il2CppMenace.Tactical.Direction.SW;
        public const int West = (int)Il2CppMenace.Tactical.Direction.W;
        public const int Northwest = (int)Il2CppMenace.Tactical.Direction.NW;
        public const int Count = (int)Il2CppMenace.Tactical.Direction.COUNT;
    }

    public static class Cover
    {
        public const int None = (int)Il2CppMenace.Tactical.CoverType.None;
        public const int Light = (int)Il2CppMenace.Tactical.CoverType.Light;
        public const int Medium = (int)Il2CppMenace.Tactical.CoverType.Medium;
        public const int Heavy = (int)Il2CppMenace.Tactical.CoverType.Heavy;
    }

    /// <summary>
    /// Tile information structure.
    /// Note: X and Z map to game's X and Z coordinates respectively.
    /// The game uses Y for elevation/height, not horizontal position.
    /// </summary>
    /// <summary>
    /// Snapshot of a tile's state at the time of the query.
    /// </summary>
    public class TileInfo
    {
        /// <summary>Game's X coordinate (horizontal).</summary>
        public int X { get; set; }
        /// <summary>Game's Z coordinate (horizontal depth).</summary>
        public int Z { get; set; }
        /// <summary>Tile elevation (game's Y axis).</summary>
        public float Elevation { get; set; }

        /// <summary>Tile is impassable.</summary>
        public bool IsBlocked { get; set; }
        /// <summary>Tile is isolated (unreachable from the main connected area).</summary>
        public bool IsIsolated { get; set; }
        /// <summary>Tile is temporarily occupied (reserved by a moving actor).</summary>
        public bool IsTemporarilyOccupied { get; set; }
        /// <summary>Tile's LOS is blocked by a half-cover object.</summary>
        public bool IsLOSBlockedByHalfcover { get; set; }
        /// <summary>Tile is inside fog of war.</summary>
        public bool IsInFogOfWar { get; set; }

        /// <summary>Tile has a living actor on it.</summary>
        public bool HasActor { get; set; }
        /// <summary>Tile has a structure on it.</summary>
        public bool HasStructure { get; set; }
        /// <summary>Name of the actor on this tile, or null if none.</summary>
        public string ActorName { get; set; }

        /// <summary>Cover value per direction (0–7). Use Cover.None/Light/Medium/Heavy constants.</summary>
        public int[] CoverValues { get; set; }
        /// <summary>Inherent cover of the tile regardless of direction.</summary>
        public int InherentCover { get; set; }
        /// <summary>Tile has any cover in any direction.</summary>
        public bool HasCover { get; set; }
        /// <summary>Tile has any half-cover.</summary>
        public bool HasHalfCover { get; set; }

        /// <summary>Tile is visible to the player faction.</summary>
        public bool IsVisibleToPlayer { get; set; }
        /// <summary>Raw visibility bitmask — one bit per faction ID.</summary>
        public ulong VisibleMask { get; set; }

        /// <summary>Tile has one or more active tile effects.</summary>
        public bool HasEffects { get; set; }
        /// <summary>Concealment value of the tile.</summary>
        public int Concealment { get; set; }

        /// <summary>World-space position of the tile center.</summary>
        public Vector3 WorldPos { get; set; }

        /// <summary>Raw native pointer to the Tile object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get cover value in a specific direction (0-7).
    /// Returns: None=0, Light=1, Medium=2, Heavy=3
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    /// <param name="direction">Direction index (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW)</param>
    public static int GetCover(int x, int z, int direction)
    {
        var tile = GetTile(x, z);
        return GetCover(tile, direction);
    }

    /// <summary>
    /// Get cover value in a specific direction (0-7).
    /// Returns: None=0, Light=1, Medium=2, Heavy=3
    /// </summary>
    /// <param name="tile">The tile to check</param>
    /// <param name="direction">Direction index (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW)</param>
    public static int GetCover(GameObj tile, int direction)
    {
        if (tile.IsNull || direction < 0 || direction >= Dir.Count) return 0;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return 0;

            var managed = typedTile.AsManaged();
            var dir = (Il2CppMenace.Tactical.Direction)direction;

            var result = GameMethod.Call<Il2CppMenace.Tactical.Tile>(
                managed, t => t.GetCover(dir, null, null, true));

            return result is Il2CppMenace.Tactical.CoverType coverType
                ? (int)coverType
                : 0;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetCover: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get cover in all 8 directions.
    /// Returns array of cover values: None=0, Light=1, Medium=2, Heavy=3
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static int[] GetAllCover(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetAllCover(tile);
    }

    /// <summary>
    /// Get cover in all 8 directions.
    /// </summary>
    public static int[] GetAllCover(GameObj tile)
    {
        var result = new int[Dir.Count];
        if (tile.IsNull) return result;

        for (int dir = 0; dir < Dir.Count; dir++)
            result[dir] = GetCover(tile, dir);

        return result;
    }

    /// <summary>
    /// Check if a tile is visible to a specific faction.
    /// Uses native Tile.IsVisibleToFaction when available.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    /// <param name="factionId">Faction ID to check visibility for</param>
    public static bool IsVisibleToFaction(int x, int z, int factionId)
    {
        var tile = GetTile(x, z);
        return IsVisibleToFaction(tile, factionId);
    }

    /// <summary>
    /// Check if a tile is visible to a specific faction.
    /// Uses native Tile.IsVisibleToFaction when available.
    /// </summary>
    public static bool IsVisibleToFaction(GameObj tile, int factionId)
    {
        if (tile.IsNull) return false;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.IsVisibleToFaction(factionId));
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsVisibleToFaction: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a tile is visible to the player.
    /// Uses native Tile.IsVisibleToPlayer when available.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static bool IsVisibleToPlayer(int x, int z)
    {
        var tile = GetTile(x, z);
        return IsVisibleToPlayer(tile);
    }

    /// <summary>
    /// Check if a tile is visible to the player.
    /// Uses native Tile.IsVisibleToPlayer when available.
    /// </summary>
    public static bool IsVisibleToPlayer(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.IsVisibleToPlayer());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsVisibleToPlayer: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a tile is blocked (impassable).
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static bool IsBlocked(int x, int z)
    {
        var tile = GetTile(x, z);
        return IsBlocked(tile);
    }

    /// <summary>
    /// Check if a tile is blocked (impassable).
    /// </summary>
    public static bool IsBlocked(GameObj tile)
    {
        if (tile.IsNull) return true;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return true;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.IsBlocked());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsBlocked: Failed", ex);
            return true;
        }
    }

    /// <summary>
    /// Check if a tile is a valid movement destination.
    /// </summary>
    public static bool IsValidMovementDestination(int x, int z) => IsValidMovementDestination(GetTile(x, z));

    /// <summary>
    /// Check if a tile is a valid movement destination.
    /// </summary>
    public static bool IsValidMovementDestination(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.IsValidMovementDestination());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsValidMovementDestination: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a tile can be entered by any actor.
    /// </summary>
    public static bool CanBeEntered(int x, int z) => CanBeEntered(GetTile(x, z));

    /// <summary>
    /// Check if a tile can be entered by any actor.
    /// </summary>
    public static bool CanBeEntered(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.CanBeEntered());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.CanBeEntered: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a tile has an actor on it.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static bool HasActor(int x, int z)
    {
        var tile = GetTile(x, z);
        return HasActor(tile);
    }

    /// <summary>
    /// Check if a tile has an actor on it.
    /// </summary>
    public static bool HasActor(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.HasActor());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.HasActor: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a tile has no actor and no structure on it.
    /// </summary>
    public static bool IsEmpty(int x, int z) => IsEmpty(GetTile(x, z));

    /// <summary>
    /// Check if a tile has no actor and no structure on it.
    /// </summary>
    public static bool IsEmpty(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.IsEmpty());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsEmpty: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the actor on a tile.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static GameObj GetActorOnTile(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetActorOnTile(tile);
    }

    /// <summary>
    /// Get the actor on a tile.
    /// </summary>
    public static GameObj GetActorOnTile(GameObj tile)
    {
        if (tile.IsNull) return GameObj.Null;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return GameObj.Null;

            var actorRaw = GameMethod.Call<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.GetActor());
            if (actorRaw is not Il2CppMenace.Tactical.Actor actor) return GameObj.Null;

            return GameObj<Il2CppMenace.Tactical.Actor>.Wrap(actor.Pointer).Untyped;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetActorOnTile: Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the neighbor tile in a direction.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    /// <param name="direction">Direction index (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW)</param>
    public static GameObj GetNeighbor(int x, int z, int direction)
    {
        var tile = GetTile(x, z);
        return GetNeighbor(tile, direction);
    }

    /// <summary>
    /// Get the neighbor tile in a direction.
    /// </summary>
    public static GameObj GetNeighbor(GameObj tile, int direction)
    {
        if (tile.IsNull || direction < 0 || direction >= Dir.Count) return GameObj.Null;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return GameObj.Null;

            var dir = (Il2CppMenace.Tactical.Direction)direction;
            var neighborRaw = GameMethod.Call<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.GetNextTile(dir));
            if (neighborRaw is not Il2CppMenace.Tactical.Tile neighbor) return GameObj.Null;

            return GameObj<Il2CppMenace.Tactical.Tile>.Wrap(neighbor.Pointer).Untyped;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetNeighbor: Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all 8 neighbors of a tile.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static GameObj[] GetAllNeighbors(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetAllNeighbors(tile);
    }

    /// <summary>
    /// Get all 8 neighbors of a tile.
    /// </summary>
    public static GameObj[] GetAllNeighbors(GameObj tile)
    {
        var result = new GameObj[Dir.Count];
        for (int dir = 0; dir < Dir.Count; dir++)
            result[dir] = GetNeighbor(tile, dir);

        return result;
    }

    /// <summary>
    /// Get all valid neighbors of a tile, excluding empty slots.
    /// </summary>
    public static List<GameObj> GetValidNeighbors(GameObj tile)
    {
        var result = new List<GameObj>(Dir.Count);
        for (int dir = 0; dir < Dir.Count; dir++)
        {
            var neighbor = GetNeighbor(tile, dir);
            if (!neighbor.IsNull)
                result.Add(neighbor);
        }
        return result;
    }

    /// <summary>
    /// Check if two tiles are direct neighbors (share an edge or corner).
    /// </summary>
    public static bool IsDirectNeighbor(int x1, int z1, int x2, int z2)
        => IsDirectNeighbor(GetTile(x1, z1), GetTile(x2, z2));

    /// <summary>
    /// Check if two tiles are direct neighbors (share an edge or corner).
    /// </summary>
    public static bool IsDirectNeighbor(GameObj tile, GameObj other)
    {
        if (tile.IsNull || other.IsNull) return false;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile, out var typedTile))
                return false;
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(other, out var typedOther))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(
                typedTile.AsManaged(), t => t.IsDirectNeighbor(typedOther.AsManaged()));
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsDirectNeighbor: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the direction from one tile to another.
    /// </summary>
    /// <param name="fromX">Source tile X coordinate</param>
    /// <param name="fromZ">Source tile Z coordinate</param>
    /// <param name="toX">Target tile X coordinate</param>
    /// <param name="toZ">Target tile Z coordinate</param>
    public static int GetDirectionTo(int fromX, int fromZ, int toX, int toZ)
    {
        var fromTile = GetTile(fromX, fromZ);
        var toTile = GetTile(toX, toZ);
        return GetDirectionTo(fromTile, toTile);
    }

    /// <summary>
    /// Get the direction from one tile to another.
    /// </summary>
    public static int GetDirectionTo(GameObj fromTile, GameObj toTile)
    {
        if (fromTile.IsNull || toTile.IsNull) return -1;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(fromTile, out var typedFrom))
                return -1;
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(toTile, out var typedTo))
                return -1;

            var result = GameMethod.Call<Il2CppMenace.Tactical.Tile>(
                typedFrom.AsManaged(), t => t.GetDirectionTo(typedTo.AsManaged()));

            return result is Il2CppMenace.Tactical.Direction dir ? (int)dir : -1;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetDirectionTo: Failed", ex);
            return -1;
        }
    }

    /// <summary>
    /// Get the distance between two tiles (in tile units).
    /// Note: Game's GetDistanceTo returns Int32, not float.
    /// </summary>
    /// <param name="x1">First tile X coordinate</param>
    /// <param name="z1">First tile Z coordinate</param>
    /// <param name="x2">Second tile X coordinate</param>
    /// <param name="z2">Second tile Z coordinate</param>
    public static int GetDistance(int x1, int z1, int x2, int z2)
    {
        var tile1 = GetTile(x1, z1);
        var tile2 = GetTile(x2, z2);
        return GetDistance(tile1, tile2);
    }

    /// <summary>
    /// Get the distance between two tiles (in tile units).
    /// Note: Game's GetDistanceTo returns Int32, not float.
    /// </summary>
    public static int GetDistance(GameObj tile1, GameObj tile2)
    {
        if (tile1.IsNull || tile2.IsNull) return -1;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile1, out var typedTile1))
                return -1;
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile2, out var typedTile2))
                return -1;

            return GameMethod.CallInt<Il2CppMenace.Tactical.Tile>(
                typedTile1.AsManaged(), t => t.GetDistanceTo(typedTile2.AsManaged()));
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetDistance: Failed", ex);
            return -1;
        }
    }

    /// <summary>
    /// Get the Manhattan distance between two tiles.
    /// </summary>
    public static int GetManhattanDistance(int x1, int z1, int x2, int z2)
        => GetManhattanDistance(GetTile(x1, z1), GetTile(x2, z2));

    /// <summary>
    /// Get the Manhattan distance between two tiles.
    /// </summary>
    public static int GetManhattanDistance(GameObj tile1, GameObj tile2)
    {
        if (tile1.IsNull || tile2.IsNull) return -1;

        try
        {
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile1, out var typedTile1))
                return -1;
            if (!GameObj<Il2CppMenace.Tactical.Tile>.TryWrap(tile2, out var typedTile2))
                return -1;

            return GameMethod.CallInt<Il2CppMenace.Tactical.Tile>(
                typedTile1.AsManaged(), t => t.GetManhattanDistanceTo(typedTile2.AsManaged()));
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetManhattanDistance: Failed", ex);
            return -1;
        }
    }
}