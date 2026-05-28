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
    /// <summary>
    /// Map information structure.
    /// </summary>
    /// <summary>
    /// Snapshot of the current map's state at the time of the query.
    /// </summary>
    public class MapInfo
    {
        /// <summary>Map width in tiles (X axis).</summary>
        public int SizeX { get; set; }
        /// <summary>Map depth in tiles (Z axis).</summary>
        public int SizeZ { get; set; }
        /// <summary>Map is using fog of war.</summary>
        public bool IsUsingFogOfWar { get; set; }
        /// <summary>Map has finished generating and is ready for queries.</summary>
        public bool IsReady { get; set; }
        /// <summary>World-space position of the map center.</summary>
        public Vector3 CenterWorldPos { get; set; }
        /// <summary>Raw native pointer to the Map object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the current tactical map.
    /// </summary>
    public static GameObj GetMap()
    {
        try
        {
            var tmRaw = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
                x => Il2CppMenace.Tactical.TacticalManager.Get());
            if (tmRaw is not Il2CppMenace.Tactical.TacticalManager tm) return GameObj.Null;

            var tmObj = GameObj<Il2CppMenace.Tactical.TacticalManager>.Wrap(tm.Pointer);
            if (tmObj.Untyped.IsNull) return GameObj.Null;

            if (!_hTacticalManagerMap.TryRead(tmObj, out var mapObj))
                return GameObj.Null;

            return mapObj.Untyped.IsNull ? GameObj.Null : mapObj.Untyped;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetMap: Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get map dimensions and info.
    /// </summary>
    public static MapInfo GetMapInfo()
    {
        try
        {
            var mapObj = GetMap();
            if (mapObj.IsNull) return null;

            if (!GameObj<Il2CppMenace.Tactical.Map>.TryWrap(mapObj, out var typedMap))
                return null;

            var info = new MapInfo { Pointer = mapObj.Pointer };

            info.SizeX = mapObj.ReadInt(0x10);
            info.SizeZ = mapObj.ReadInt(0x14);

            if (_hMapIsUsingFogOfWar.TryRead(typedMap, out var fog)) info.IsUsingFogOfWar = fog;
            if (_hMapIsReady.TryRead(typedMap, out var ready)) info.IsReady = ready;

            var centerRaw = GameMethod.Call<Il2CppMenace.Tactical.Map>(
                typedMap.AsManaged(), x => x.GetCenterWorldPos());
            if (centerRaw is Vector3 center) info.CenterWorldPos = center;

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.GetMapInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if the given coordinates are within the map bounds.
    /// </summary>
    public static bool IsValidTile(int x, int z)
    {
        try
        {
            var mapObj = GetMap();
            if (mapObj.IsNull) return false;

            if (!GameObj<Il2CppMenace.Tactical.Map>.TryWrap(mapObj, out var typedMap))
                return false;

            return GameMethod.CallBool<Il2CppMenace.Tactical.Map>(
                typedMap.AsManaged(), m => m.IsValidTile(x, z));
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsValidTile: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if the given coordinates are within the map bounds.
    /// </summary>
    public static bool IsInBounds(int x, int z)
    {
        try
        {
            return GameMethod.CallStatic<Il2CppMenace.Tactical.Map>(
                m => Il2CppMenace.Tactical.Map.IsInBounds(x, z)) is bool result && result;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.IsInBounds: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Execute an action on every tile in the current map.
    /// </summary>
    public static void ForEachTile(Action<GameObj> action)
    {
        if (action == null) return;

        try
        {
            var mapObj = GetMap();
            if (mapObj.IsNull) return;

            if (!GameObj<Il2CppMenace.Tactical.Map>.TryWrap(mapObj, out var typedMap))
                return;

            typedMap.AsManaged().ForEachTile(new Action<Il2CppMenace.Tactical.Tile>(tile =>
            {
                if (tile == null) return;
                action(GameObj<Il2CppMenace.Tactical.Tile>.Wrap(tile.Pointer).Untyped);
            }));
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.ForEachTile: Failed", ex);
        }
    }

    /// <summary>
    /// Get all tiles inside a rectangular area.
    /// </summary>
    public static List<GameObj> QueryTilesInside(RectInt area)
        => QueryTilesInside(area, emptyOnly: false, nonBlockedOnly: true, nonIsolatedOnly: true);

    /// <summary>
    /// Get all tiles inside a rectangular area with filtering options.
    /// </summary>
    public static List<GameObj> QueryTilesInside(RectInt area, bool emptyOnly, bool nonBlockedOnly, bool nonIsolatedOnly)
    {
        var result = new List<GameObj>();

        try
        {
            var mapObj = GetMap();
            if (mapObj.IsNull) return result;

            if (!GameObj<Il2CppMenace.Tactical.Map>.TryWrap(mapObj, out var typedMap))
                return result;

            var nativeList = new Il2CppSystem.Collections.Generic.List<Il2CppMenace.Tactical.Tile>();
            GameMethod.Call<Il2CppMenace.Tactical.Map>(
                typedMap.AsManaged(), m => m.QueryTilesInside(area, nativeList, emptyOnly, nonBlockedOnly, nonIsolatedOnly));

            foreach (var tile in nativeList)
            {
                if (tile == null) continue;
                result.Add(GameObj<Il2CppMenace.Tactical.Tile>.Wrap(tile.Pointer).Untyped);
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TileMap.QueryTilesInside: Failed", ex);
        }

        return result;
    }
}