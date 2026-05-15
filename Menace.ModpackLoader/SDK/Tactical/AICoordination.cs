using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Data;
using System;
using System.Collections.Generic;

using AISkillBehavior = Il2CppMenace.Tactical.AI.SkillBehavior;

namespace Menace.SDK;

/// <summary>
/// SDK for coordinated AI behavior modifications (Combined Arms-style mods).
///
/// Provides safe hooks for:
/// - Agent selection scoring (which unit acts next)
/// - Tile score modification (where units want to move)
/// - Turn state tracking (suppressor actions, targeted tiles, positions)
///
/// THREADING MODEL:
/// - OnTurnStart callbacks run single-threaded (SAFE for writes)
/// - Agent scoring callbacks run in parallel (READ-ONLY shared state)
/// - Tile score callbacks run in parallel per-agent (SAFE to write per-agent tiles)
/// - OnExecute callbacks run sequentially (SAFE for writes)
/// </summary>
public static class AICoordination
{
    // ═══════════════════════════════════════════════════════════════════
    //  Per-Faction Turn State
    // ═══════════════════════════════════════════════════════════════════

    private static readonly Dictionary<int, FactionTurnState> _factionStates = new();
    private static readonly object _stateLock = new();

    private static class Offsets
    {
        // Agent fields
        internal static readonly Lazy<ObjFieldHandle<Agent, Actor>> Agent_Actor
            = new(() => GameObj<Agent>.ResolveObjField(x => x.m_Actor));

        internal static readonly Lazy<ObjFieldHandle<Agent, AIFaction>> Agent_Faction
            = new(() => GameObj<Agent>.ResolveObjField(x => x.m_Faction));

        internal static readonly Lazy<ObjFieldHandle<Agent, Behavior>> Agent_ActiveBehavior
            = new(() => GameObj<Agent>.ResolveObjField(x => x.m_ActiveBehavior));

        internal static readonly Lazy<FieldHandle<Agent, IntPtr>> Agent_Tiles
            = new(() => GameObj<Agent>.FieldAt<IntPtr>(0x58, "m_Tiles"));

        // BaseTile coordinate fields (inherited by Menace.Tactical.Tile)
        internal static readonly Lazy<FieldHandle<Tile, int>> Tile_X
            = new(() => GameObj<Tile>.ResolveField(x => x.m_X));

        internal static readonly Lazy<FieldHandle<Tile, int>> Tile_Z
            = new(() => GameObj<Tile>.ResolveField(x => x.m_Z));

        // TileScore fields
        internal static readonly Lazy<FieldHandle<TileScore, float>> TileScore_UtilityScore
            = new(() => GameObj<TileScore>.ResolveField(x => x.UtilityScore));

        internal static readonly Lazy<ObjFieldHandle<Il2CppMenace.Tactical.AI.SkillBehavior, Tile>> SkillBehavior_TargetTile
            = new(() => GameObj<Il2CppMenace.Tactical.AI.SkillBehavior>.ResolveObjField(x => x.m_TargetTile));

        internal static readonly Lazy<FieldHandle<BaseFaction, int>> BaseFaction_FactionIndex
            = new(() => GameObj<BaseFaction>.ResolveField(x => x.m_FactionIndex));
    }

    /// <summary>
    /// State tracked per faction per turn.
    /// Reset at the start of each faction's turn.
    /// </summary>
    public class FactionTurnState
    {
        /// <summary>Faction index this state belongs to.</summary>
        public int FactionIndex { get; internal set; }

        /// <summary>Whether a suppression-focused unit has acted this turn.</summary>
        public bool HasSuppressorActed { get; set; }

        /// <summary>Whether a damage-focused unit has acted this turn.</summary>
        public bool HasDamageDealerActed { get; set; }

        /// <summary>Number of units that have acted this turn.</summary>
        public int ActedCount { get; set; }

        /// <summary>Tiles that have been targeted by attacks this turn. Key is tile pointer.</summary>
        public HashSet<IntPtr> TargetedTiles { get; } = new();

        /// <summary>Count of attacks per targeted tile.</summary>
        public Dictionary<IntPtr, int> TargetedTileCounts { get; } = new();

        /// <summary>Actor positions at start of turn (for centroid calculation).</summary>
        public List<(int x, int y)> AllyPositions { get; } = new();

        /// <summary>Pre-computed centroid of ally positions.</summary>
        public (float x, float y) AllyCentroid { get; set; }

        /// <summary>Enemy positions at start of turn.</summary>
        public List<(int x, int y)> EnemyPositions { get; } = new();

        /// <summary>Pre-computed centroid of enemy positions.</summary>
        public (float x, float y) EnemyCentroid { get; set; }

        /// <summary>Custom data storage for mod-specific state.</summary>
        public Dictionary<string, object> CustomData { get; } = new();

        /// <summary>Reset state for a new turn.</summary>
        public void Reset()
        {
            HasSuppressorActed = false;
            HasDamageDealerActed = false;
            ActedCount = 0;
            TargetedTiles.Clear();
            TargetedTileCounts.Clear();
            AllyPositions.Clear();
            EnemyPositions.Clear();
            AllyCentroid = (0, 0);
            EnemyCentroid = (0, 0);
            CustomData.Clear();
        }

        /// <summary>Compute centroids from position lists.</summary>
        public void ComputeCentroids()
        {
            if (AllyPositions.Count > 0)
            {
                float sumX = 0, sumY = 0;
                foreach (var pos in AllyPositions)
                {
                    sumX += pos.x;
                    sumY += pos.y;
                }
                AllyCentroid = (sumX / AllyPositions.Count, sumY / AllyPositions.Count);
            }

            if (EnemyPositions.Count > 0)
            {
                float sumX = 0, sumY = 0;
                foreach (var pos in EnemyPositions)
                {
                    sumX += pos.x;
                    sumY += pos.y;
                }
                EnemyCentroid = (sumX / EnemyPositions.Count, sumY / EnemyPositions.Count);
            }
        }
    }

    /// <summary>
    /// Get or create faction state for a faction index.
    /// Thread-safe.
    /// </summary>
    public static FactionTurnState GetFactionState(int factionIndex)
    {
        lock (_stateLock)
        {
            if (!_factionStates.TryGetValue(factionIndex, out var state))
            {
                state = new FactionTurnState { FactionIndex = factionIndex };
                _factionStates[factionIndex] = state;
            }
            return state;
        }
    }

    /// <summary>
    /// Reset faction state for a new turn.
    /// Call this from OnTurnStart hook.
    /// </summary>
    public static void ResetFactionState(int factionIndex)
    {
        var state = GetFactionState(factionIndex);
        state.Reset();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Agent Classification Helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Unit role classification based on RoleData weights.
    /// </summary>
    public enum UnitRole
    {
        Unknown,
        Suppressor,     // Prefers suppression over damage
        DamageDealer,   // Prefers damage over suppression
        Balanced,       // Similar suppression and damage weights
        Support,        // Low combat weights, high move weight
        Defensive       // High safety scale
    }

    /// <summary>
    /// Classify a unit's role based on its RoleData.
    /// </summary>
    public static UnitRole ClassifyUnit(GameObj<Actor> actor)
    {
        var role = AI.GetRoleData(actor);

        float damage = role.InflictDamageWeight;
        float suppression = role.InflictSuppressionWeight;
        float safety = role.SafetyScale;
        float move = role.MoveWeight;

        // High safety = defensive
        if (safety > 30f)
            return UnitRole.Defensive;

        // High move, low combat = support
        if (move > 5f && damage < 5f && suppression < 5f)
            return UnitRole.Support;

        // Compare damage vs suppression
        float combatDiff = Math.Abs(damage - suppression);
        if (combatDiff < 2f)
            return UnitRole.Balanced;

        return suppression > damage ? UnitRole.Suppressor : UnitRole.DamageDealer;
    }

    /// <summary>
    /// Check if a unit is primarily a suppressor.
    /// </summary>
    public static bool IsSuppressor(GameObj<Actor> actor)
    {
        var role = AI.GetRoleData(actor);
        return role.InflictSuppressionWeight > role.InflictDamageWeight &&
               role.InflictSuppressionWeight > 0;
    }

    /// <summary>
    /// Check if a unit is primarily a damage dealer.
    /// </summary>
    public static bool IsDamageDealer(GameObj<Actor> actor)
    {
        var role = AI.GetRoleData(actor);
        return role.InflictDamageWeight > role.InflictSuppressionWeight &&
               role.InflictDamageWeight > 0;
    }

    /// <summary>
    /// Classify unit into formation band (Frontline/Midline/Backline).
    /// </summary>
    public enum FormationBand { Frontline, Midline, Backline }

    public static FormationBand ClassifyFormationBand(GameObj<Actor> actor)
    {
        var role = AI.GetRoleData(actor);

        float frontScore = role.MoveWeight * 0.4f + role.InflictDamageWeight * 0.6f;
        float midScore = role.InflictSuppressionWeight;
        float backScore = role.SafetyScale;

        if (frontScore >= midScore && frontScore >= backScore)
            return FormationBand.Frontline;
        if (midScore >= backScore)
            return FormationBand.Midline;
        return FormationBand.Backline;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Agent Scoring Helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate a score multiplier for agent selection based on coordination rules.
    /// Call this from a GetScoreMultForPickingThisAgent postfix.
    ///
    /// Returns a multiplier (1.0 = no change, >1.0 = prioritize, <1.0 = deprioritize).
    /// </summary>
    public static float CalculateAgentScoreMultiplier(
        GameObj<Actor> actor,
        FactionTurnState state,
        CoordinationConfig config)
    {
        float multiplier = 1.0f;

        // === Agent Sequencing ===
        if (config.EnableSequencing)
        {
            var role = ClassifyUnit(actor);

            if (!state.HasSuppressorActed)
            {
                // Suppressors haven't acted yet
                if (role == UnitRole.Suppressor)
                {
                    multiplier *= config.SuppressorPriorityBoost;
                }
                else if (role == UnitRole.DamageDealer)
                {
                    multiplier *= config.DamageDealerPenalty;
                }
            }
        }

        // === Focus Fire ===
        if (config.EnableFocusFire && state.TargetedTiles.Count > 0)
        {
            // Check if this agent's behaviors target an already-targeted tile
            // BehaviorInfo from AI.GetBehaviors() extracts TargetTileX/Z from the
            // SkillBehavior.TargetTile object reference at +0x58
            var behaviors = AI.GetBehaviors(actor);
            foreach (var behavior in behaviors)
            {
                // Check if behavior targets a tile we've already attacked
                // TargetTileX/Z are extracted from SkillBehavior's TargetTile reference
                if (behavior.TargetTileX.HasValue && behavior.TargetTileZ.HasValue)
                {
                    // Use position matching since we track tile pointers in state
                    foreach (var (x, z) in GetTargetedTilePositions(state))
                    {
                        if (behavior.TargetTileX.Value == x && behavior.TargetTileZ.Value == z)
                        {
                            multiplier *= config.FocusFireBoost;
                            break;
                        }
                    }
                }
            }
        }

        return multiplier;
    }

    private static IEnumerable<(int x, int z)> GetTargetedTilePositions(FactionTurnState state)
    {
        // This would need tile pointer -> position lookup
        // For now, return empty - full implementation needs TileMap integration
        yield break;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Tile Score Modification
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Modify tile scores for an agent based on coordination rules.
    /// Call this from a PostProcessTileScores postfix.
    ///
    /// THREADING: This is safe because each agent has its own m_Tiles dictionary.
    /// Only modify tiles for the current agent, not shared state.
    /// </summary>
    public static void ApplyTileScoreModifiers(
    GameObj<Agent> agent,
    FactionTurnState state,
    CoordinationConfig config)
    {
        if (agent.Untyped.IsNull)
            return;

        if (!Offsets.Agent_Actor.Value.TryRead(agent, out var actorObj))
            return;

        if (!Offsets.Agent_Tiles.Value.TryRead(agent, out var tilesDictPtr))
            return;
        var tilesDict = GameObj.FromPointer(tilesDictPtr);

        // Get formation band for this unit
        FormationBand band = ClassifyFormationBand(actorObj);

        // Iterate tiles and apply modifiers
        var dict = new GameDict(tilesDict);

        foreach (var (tileKey, tileScore) in dict)
        {
            if (tileKey.IsNull || tileScore.IsNull)
                continue;

            float deltaUtility = 0f;

            // Get tile position (m_X/m_Z on BaseTile — "X"/"Y" were incorrect field names)
            var tileObj = GameObj<Tile>.Wrap(tileKey.Pointer);
            int tileX = Offsets.Tile_X.Value.Read(tileObj);
            int tileZ = Offsets.Tile_Z.Value.Read(tileObj);

            // === Center of Forces ===
            if (config.EnableCenterOfForces && state.AllyPositions.Count >= config.CenterOfForcesMinAllies)
            {
                float dx = tileX - state.AllyCentroid.x;
                float dz = tileZ - state.AllyCentroid.y;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);

                if (dist < config.CenterOfForcesMaxRange)
                {
                    float normalizedProximity = 1.0f - (dist / config.CenterOfForcesMaxRange);
                    deltaUtility += config.CenterOfForcesWeight * normalizedProximity;
                }
            }

            // === Formation Depth ===
            if (config.EnableFormationDepth && state.EnemyPositions.Count >= config.FormationDepthMinEnemies)
            {
                float dx = tileX - state.EnemyCentroid.x;
                float dz = tileZ - state.EnemyCentroid.y;
                float distToEnemy = (float)Math.Sqrt(dx * dx + dz * dz);

                float depthScore = ComputeDepthScore(distToEnemy, band, config);
                deltaUtility += depthScore * config.FormationDepthWeight;
            }

            // Apply delta to utility score
            if (Math.Abs(deltaUtility) > 0.001f)
            {
                var tsObj = GameObj<TileScore>.Wrap(tileScore.Pointer);
                float current = Offsets.TileScore_UtilityScore.Value.Read(tsObj);
                Offsets.TileScore_UtilityScore.Value.Write(tsObj, current + deltaUtility);
            }
        }
    }

    private static float ComputeDepthScore(float distanceToEnemy, FormationBand band, CoordinationConfig config)
    {
        float maxRange = config.FormationDepthMaxRange;
        float d = Math.Max(0f, Math.Min(distanceToEnemy, maxRange));

        // Compute band edges
        float frontEdge = maxRange * config.FrontlineFraction;
        float midEdge = maxRange * (config.FrontlineFraction + config.MidlineFraction);

        // Get ideal distance for this band
        float idealDist = band switch
        {
            FormationBand.Frontline => frontEdge / 2,
            FormationBand.Midline => (frontEdge + midEdge) / 2,
            FormationBand.Backline => (midEdge + maxRange) / 2,
            _ => maxRange / 2
        };

        // Score based on distance from ideal position
        return 1.0f - 2.0f * Math.Abs(d - idealDist) / maxRange;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Execution Tracking
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Record that an agent has executed its action.
    /// Call this from an Agent.Execute postfix.
    /// </summary>
    public static void RecordAgentExecution(GameObj<Agent> agent, int factionIndex)
    {
        if (agent.Untyped.IsNull)
            return;

        var state = GetFactionState(factionIndex);

        if (!Offsets.Agent_Actor.Value.TryRead(agent, out var actorObj))
            return;

        state.ActedCount++;

        // Track if suppressor/damage dealer acted
        if (IsSuppressor(actorObj))
            state.HasSuppressorActed = true;
        if (IsDamageDealer(actorObj))
            state.HasDamageDealerActed = true;

        // Track targeted tile
        // m_TargetTile is on SkillBehavior at 0x58 — "TargetTile" was incorrect field name
        if (Offsets.Agent_ActiveBehavior.Value.TryRead(agent, out var activeBehavior))
        {
            var skillBehavior = GameObj<AISkillBehavior>.Wrap(activeBehavior.Untyped);
            if (Offsets.SkillBehavior_TargetTile.Value.TryRead(skillBehavior, out var targetTile))
            {
                state.TargetedTiles.Add(targetTile.Untyped.Pointer);

                if (state.TargetedTileCounts.TryGetValue(targetTile.Untyped.Pointer, out int count))
                    state.TargetedTileCounts[targetTile.Untyped.Pointer] = count + 1;
                else
                    state.TargetedTileCounts[targetTile.Untyped.Pointer] = 1;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Turn Start Initialization
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize faction state at the start of a turn.
    /// Call this from an AIFaction.OnTurnStart postfix.
    ///
    /// Collects ally/enemy positions and computes centroids.
    /// </summary>
    public static void InitializeTurnState(int factionIndex)
    {
        var state = GetFactionState(factionIndex);
        state.Reset();

        // Collect ally positions
        var allies = EntitySpawner.ListEntities(factionIndex);
        foreach (var ally in allies)
        {
            var info = EntitySpawner.GetEntityInfo(ally);
            if (info == null || !info.IsAlive)
                continue;

            var pos = GetActorTilePosition(GameObj<Actor>.Wrap(ally));
            if (pos.x >= 0 && pos.y >= 0)
                state.AllyPositions.Add(pos);
        }

        // Collect enemy positions (faction 0 = player, others are enemies relative to each other)
        // This is simplified - a full implementation would use AIFaction.m_Opponents
        int enemyFaction = factionIndex == 0 ? 1 : 0;
        var enemies = EntitySpawner.ListEntities(enemyFaction);
        foreach (var enemy in enemies)
        {
            var info = EntitySpawner.GetEntityInfo(enemy);
            if (info == null || !info.IsAlive)
                continue;

            var pos = GetActorTilePosition(GameObj<Actor>.Wrap(enemy));
            if (pos.x >= 0 && pos.y >= 0)
                state.EnemyPositions.Add(pos);
        }

        // Compute centroids
        state.ComputeCentroids();
    }

    /// <summary>
    /// Get an actor's current tile position.
    /// Uses Actor.GetTile() method (not direct field access) per audit findings.
    /// </summary>
    public static (int x, int y) GetActorTilePosition(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return (-1, -1);

        try
        {
            var tile = actor.AsManaged().GetTile();
            if (tile == null)
                return (-1, -1);

            var tileObj = GameObj<Tile>.Wrap(tile.Pointer);
            int x = Offsets.Tile_X.Value.Read(tileObj);
            int z = Offsets.Tile_Z.Value.Read(tileObj);
            return (x, z);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.GetActorTilePosition", "Failed", ex);
            return (-1, -1);
        }
    }

    /// <summary>
    /// Get faction index from an AIFaction object.
    /// Uses BaseFaction.GetIndex() method per audit findings.
    /// </summary>
    public static int GetFactionIndex(GameObj<AIFaction> aiFaction)
    {
        if (aiFaction.Untyped.IsNull)
            return -1;

        try
        {
            return aiFaction.AsManaged().GetIndex();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.GetFactionIndex", "Failed", ex);
            return -1;
        }
    }

    /// <summary>
    /// Get faction index from an Agent object.
    /// Uses GetAIFaction().GetIndex() method chain per audit findings.
    /// </summary>
    public static int GetAgentFactionIndex(GameObj<Agent> agent)
    {
        if (agent.Untyped.IsNull)
            return -1;

        try
        {
            var faction = agent.AsManaged().GetFaction();
            if (faction == null)
                return -1;

            return GetFactionIndex(GameObj<AIFaction>.Wrap(faction.Pointer));
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.GetAgentFactionIndex", "Failed", ex);
            return -1;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Configuration
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Configuration for coordination features.
    /// </summary>
    public class CoordinationConfig
    {
        // Agent Sequencing
        public bool EnableSequencing { get; set; } = true;
        public float SuppressorPriorityBoost { get; set; } = 1.5f;
        public float DamageDealerPenalty { get; set; } = 0.7f;

        // Focus Fire
        public bool EnableFocusFire { get; set; } = true;
        public float FocusFireBoost { get; set; } = 1.4f;

        // Center of Forces
        public bool EnableCenterOfForces { get; set; } = true;
        public float CenterOfForcesWeight { get; set; } = 3.0f;
        public float CenterOfForcesMaxRange { get; set; } = 12f;
        public int CenterOfForcesMinAllies { get; set; } = 2;

        // Formation Depth
        public bool EnableFormationDepth { get; set; } = true;
        public float FormationDepthWeight { get; set; } = 2.5f;
        public float FormationDepthMaxRange { get; set; } = 18f;
        public float FrontlineFraction { get; set; } = 0.33f;
        public float MidlineFraction { get; set; } = 0.34f;
        public int FormationDepthMinEnemies { get; set; } = 1;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("aicoord", "[faction]", "Show AI coordination state for faction", args =>
        {
            int faction = args.Length > 0 && int.TryParse(args[0], out int f) ? f : 1;
            var state = GetFactionState(faction);

            return $"Faction {faction} coordination state:\n" +
                   $"  Allies: {state.AllyPositions.Count} at centroid ({state.AllyCentroid.x:F1}, {state.AllyCentroid.y:F1})\n" +
                   $"  Enemies: {state.EnemyPositions.Count} at centroid ({state.EnemyCentroid.x:F1}, {state.EnemyCentroid.y:F1})\n" +
                   $"  Acted: {state.ActedCount}, Suppressor acted: {state.HasSuppressorActed}\n" +
                   $"  Targeted tiles: {state.TargetedTiles.Count}";
        });

        DevConsole.RegisterCommand("aiclassify", "[actor_name]", "Classify actor's role and formation band", args =>
        {
            Actor actor;
            if (args.Length > 0)
            {
                var name = string.Join(" ", args);
                actor = GameQuery.FindByName<Actor>(name);
                if (actor == null)
                    return $"Actor '{name}' not found";
            }
            else
            {
                actor = new Actor(TacticalController.GetActiveActor().Pointer);
                if (actor == null)
                    return "No active actor";
            }

            var actorObj = GameObj<Actor>.Wrap(actor.Pointer);
            var role = ClassifyUnit(actorObj);
            var band = ClassifyFormationBand(actorObj);
            var roleData = AI.GetRoleData(actorObj);

            return $"{actor.DebugName}:\n" +
                   $"  Role: {role}\n" +
                   $"  Formation band: {band}\n" +
                   $"  Weights: damage={roleData.InflictDamageWeight:F1} suppress={roleData.InflictSuppressionWeight:F1} " +
                   $"move={roleData.MoveWeight:F1} safety={roleData.SafetyScale:F1}";
        });
    }
}
