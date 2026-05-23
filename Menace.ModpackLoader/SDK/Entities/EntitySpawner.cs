using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// SDK extension for spawning and destroying entities in tactical combat.
/// Uses IL2CPP interop to call game spawning methods.
///
/// Based on reverse engineering findings:
/// - TacticalManager.TrySpawnUnit(FactionType, EntityTemplate, Tile, out Actor) @ TacticalManager.s_Singleton
/// - Entity.Die(bool destroyImmediately) @ 0x180610aa0
/// </summary>
public static class EntitySpawner
{
    // Cached types
    private static readonly GameType _actorType = GameType.Of<Il2CppMenace.Tactical.Actor>();
    private static readonly GameType _entityTemplateType = GameType.Of<Il2CppMenace.Tactical.EntityTemplate>();
    private static readonly GameType _tileType = GameType.Of<Il2CppMenace.Tactical.Tile>();
    private static readonly GameType _tacticalManagerType = GameType.Of<Il2CppMenace.Tactical.TacticalManager>();
    private static readonly GameType _baseFactionType = GameType.Of<Il2CppMenace.Tactical.AI.BaseFaction>();

    // Entity field offsets — confirmed from IL2CPP dump (Entity.cs)
    private const uint OFFSET_ENTITY_ID = 0x10;                 // <ID>k__BackingField        int
    private const uint OFFSET_ENTITY_IS_ALIVE = 0x48;           // m_IsAlive                  bool
    private const uint OFFSET_ENTITY_FACTION_ID = 0x4C;         // m_FactionID                int
    private const uint OFFSET_ENTITY_DEBUG_NAME = 0x88;         // <DebugName>k__BackingField string

    public class EntityInfo
    {
        public int EntityId { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int FactionId { get; set; }
        public bool IsAlive { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Spawn result containing the spawned entity or error info.
    /// </summary>
    public class SpawnResult
    {
        public bool Success { get; set; }
        public GameObj Entity { get; set; }
        public string Error { get; set; }

        public static SpawnResult Failed(string error) => new() { Success = false, Error = error };
        public static SpawnResult Ok(GameObj entity) => new() { Success = true, Entity = entity };
    }

    public static SpawnResult SpawnUnit(string templateId, int tileX, int tileZ, Il2CppMenace.Tactical.FactionType faction = Il2CppMenace.Tactical.FactionType.Neutral)
    {
        try
        {
            if (!Templates.TryGet<Il2CppMenace.Tactical.EntityTemplate>(templateId, out var template))
                return SpawnResult.Failed($"Template '{templateId}' not found");

            var tile = GetTileAt(tileX, tileZ);
            if (tile.IsNull)
                return SpawnResult.Failed($"Tile at ({tileX}, {tileZ}) not found");

            if (GameMethod.CallBool<Il2CppMenace.Tactical.Tile>(tile.As<Il2CppMenace.Tactical.Tile>(), x => x.HasActor()))
                return SpawnResult.Failed($"Tile at ({tileX}, {tileZ}) is occupied");

            var tm = Il2CppMenace.Tactical.TacticalManager.Get();
            if (tm == null)
                return SpawnResult.Failed("TacticalManager not available");

            // TrySpawnUnit uses an out parameter — AsManaged() is required here;
            // expression-tree method invocation cannot represent out arguments.
            var success = tm.TrySpawnUnit(faction, template, tile.As<Il2CppMenace.Tactical.Tile>(), out var actor);

            if (!success)
                return SpawnResult.Failed($"TrySpawnUnit returned false for '{templateId}'");

            var actorObj = new GameObj(actor.Pointer);
            ModError.Info("EntitySpawner", $"Spawned '{templateId}' at ({tileX}, {tileZ}) faction {faction}");
            return SpawnResult.Ok(actorObj);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.SpawnUnit", $"Failed to spawn '{templateId}'", ex);
            return SpawnResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Spawn multiple units at once.
    /// </summary>
    /// <param name="templateId">EntityTemplate m_ID</param>
    /// <param name="positions">List of (x, z) tile coordinates</param>
    /// <param name="faction">Faction for all spawned units (default: Neutral)</param>
    /// <returns>List of spawn results</returns>
    public static List<SpawnResult> SpawnGroup(string templateId, List<(int x, int z)> positions, Il2CppMenace.Tactical.FactionType faction = Il2CppMenace.Tactical.FactionType.Neutral)
    {
        var results = new List<SpawnResult>();
        foreach (var (x, z) in positions)
            results.Add(SpawnUnit(templateId, x, z, faction));
        return results;
    }

    /// <summary>
    /// Get all actors currently on the tactical map.
    /// </summary>
    /// <param name="factionFilter">Optional faction to filter by, or null for all factions</param>
    /// <returns>Array of actor GameObjs</returns>
    public static GameObj[] ListEntities(Il2CppMenace.Tactical.FactionType? factionFilter = null)
    {
        try
        {
            var tm = Il2CppMenace.Tactical.TacticalManager.Get();
            if (tm == null) return Array.Empty<GameObj>();

            var factions = tm.GetFactions();
            if (factions == null) return Array.Empty<GameObj>();

            var result = new List<GameObj>();
            foreach (var faction in factions)
            {
                if (faction == null) continue;
                if (factionFilter.HasValue && faction.GetFactionType() != factionFilter.Value) continue;

                var actors = faction.GetActors();
                if (actors == null) continue;

                foreach (var actor in actors)
                {
                    if (actor == null) continue;
                    var ptr = actor.Pointer;
                    if (ptr != IntPtr.Zero)
                        result.Add(new GameObj(ptr));
                }
            }

            return result.ToArray();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.ListEntities", "Failed to list entities", ex);
            return Array.Empty<GameObj>();
        }
    }

    /// <summary>
    /// Destroy/kill an entity.
    /// </summary>
    /// <param name="entity">The entity to destroy</param>
    /// <param name="immediate">If true, skip death animation</param>
    /// <returns>True if successful</returns>
    public static bool DestroyEntity(GameObj entity, bool immediate = false)
    {
        if (entity.IsNull || entity.CheckAlive() != AliveStatus.Alive)
            return false;

        try
        {
            GameMethod.Call<Il2CppMenace.Tactical.Actor>(entity.As<Il2CppMenace.Tactical.Actor>(), x => x.Die(immediate));
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.DestroyEntity", "Failed to destroy entity", ex);
            return false;
        }
    }

    /// <summary>
    /// Clear all actors of a given faction from the map.
    /// </summary>
    /// <param name="immediate">If true, skip death animations</param>
    /// <param name="faction">Faction to clear (default: EnemyLocalForces)</param>
    /// <returns>Number of actors cleared</returns>
    public static int ClearEnemies(bool immediate = true, Il2CppMenace.Tactical.FactionType faction = Il2CppMenace.Tactical.FactionType.EnemyLocalForces)
    {
        var enemies = ListEntities(faction);
        int count = 0;

        foreach (var enemy in enemies)
        {
            if (DestroyEntity(enemy, immediate))
                count++;
        }

        return count;
    }

    public static EntityInfo GetEntityInfo(GameObj entity)
    {
        if (entity.IsNull)
            return null;

        try
        {
            return new EntityInfo
            {
                EntityId = entity.ReadInt(OFFSET_ENTITY_ID),
                Name = entity.GetName() ?? entity.ReadString(OFFSET_ENTITY_DEBUG_NAME),
                TypeName = entity.GetTypeName(),
                FactionId = entity.ReadInt(OFFSET_ENTITY_FACTION_ID),
                IsAlive = entity.ReadBool(OFFSET_ENTITY_IS_ALIVE),
                Pointer = entity.Pointer
            };
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.GetEntityInfo", "Failed", ex);
            return null;
        }
    }

    // --- Internal helpers ---

    private static GameObj GetTileAt(int x, int z)
    {
        try
        {
            var tm = Il2CppMenace.Tactical.TacticalManager.Get();
            if (tm == null) return GameObj.Null;

            var map = tm.GetMap();
            if (map == null) return GameObj.Null;

            var tile = map.GetBaseTile(x, z);
            if (tile == null) return GameObj.Null;

            return new GameObj(tile.Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.GetTileAt", $"Failed for ({x}, {z})", ex);
            return GameObj.Null;
        }
    }
}
