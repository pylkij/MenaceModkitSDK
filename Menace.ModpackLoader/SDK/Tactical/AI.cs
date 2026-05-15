using Il2CppInterop.Runtime;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Data;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;

using SkillBehavior = Il2CppMenace.Tactical.AI.SkillBehavior;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for the AI decision-making system.
///
/// The Menace AI uses a utility-based decision system where Agent objects
/// evaluate Behavior options using a multi-criteria scoring system, then
/// execute the highest-utility action.
///
/// Based on reverse engineering in docs/reverse-engineering/ai-decisions.md
/// </summary>
public static class AI
{
    // Cached types
    private static readonly GameType _agentType = GameType.Of<Il2CppMenace.Tactical.AI.Agent>();
    private static readonly GameType _aiFactionType = GameType.Of<Il2CppMenace.Tactical.AI.AIFaction>();
    private static readonly GameType _behaviorType = GameType.Of<Il2CppMenace.Tactical.AI.Behavior>();
    private static readonly GameType _roleDataType = GameType.Of<Il2CppMenace.Tactical.AI.Data.RoleData>();
    private static readonly GameType _actorType = GameType.Of<Il2CppMenace.Tactical.Actor>();

    private static class Offsets
    {
        // Agent
        internal static readonly Lazy<ObjFieldHandle<Agent, Behavior>> ActiveBehavior
            = new(() => GameObj<Agent>.ResolveObjField(x => x.m_ActiveBehavior));

        // SkillBehavior
        internal static readonly Lazy<ObjFieldHandle<SkillBehavior, Tile>> TargetTile
            = new(() => GameObj<SkillBehavior>.ResolveObjField(x => x.m_TargetTile));

        // TileScore
        internal static readonly Lazy<FieldHandle<TileScore, float>> DistanceScore
            = new(() => GameObj<TileScore>.ResolveField(x => x.DistanceScore));

        internal static readonly Lazy<FieldHandle<TileScore, float>> SafetyScore
            = new(() => GameObj<TileScore>.ResolveField(x => x.SafetyScore));

        internal static readonly Lazy<FieldHandle<TileScore, float>> UtilityScore
            = new(() => GameObj<TileScore>.ResolveField(x => x.UtilityScore));

        internal static readonly Lazy<FieldHandle<Behavior, int>> Score
            = new(() => GameObj<Behavior>.ResolveField(x => x.m_Score));

        // RoleData
        internal static readonly Lazy<FieldHandle<RoleData, float>> UtilityScale
            = new(() => GameObj<RoleData>.ResolveField(x => x.UtilityScale));
        internal static readonly Lazy<FieldHandle<RoleData, float>> SafetyScale
            = new(() => GameObj<RoleData>.ResolveField(x => x.SafetyScale));
        internal static readonly Lazy<FieldHandle<RoleData, float>> DistanceScale
            = new(() => GameObj<RoleData>.ResolveField(x => x.DistanceScale));
        internal static readonly Lazy<FieldHandle<RoleData, float>> FriendlyFirePenalty
            = new(() => GameObj<RoleData>.ResolveField(x => x.FriendlyFirePenalty));
        internal static readonly Lazy<FieldHandle<RoleData, float>> Move
            = new(() => GameObj<RoleData>.ResolveField(x => x.Move));
        internal static readonly Lazy<FieldHandle<RoleData, float>> InflictDamage
            = new(() => GameObj<RoleData>.ResolveField(x => x.InflictDamage));
        internal static readonly Lazy<FieldHandle<RoleData, float>> InflictSuppression
            = new(() => GameObj<RoleData>.ResolveField(x => x.InflictSuppression));
        internal static readonly Lazy<FieldHandle<RoleData, float>> Stun
            = new(() => GameObj<RoleData>.ResolveField(x => x.Stun));
        internal static readonly Lazy<FieldHandle<RoleData, bool>> IsAllowedToEvadeEnemies
            = new(() => GameObj<RoleData>.ResolveField(x => x.IsAllowedToEvadeEnemies));
        internal static readonly Lazy<FieldHandle<RoleData, bool>> AttemptToStayOutOfSight
            = new(() => GameObj<RoleData>.ResolveField(x => x.AttemptToStayOutOfSight));
        internal static readonly Lazy<FieldHandle<RoleData, bool>> PeekInAndOutOfCover
            = new(() => GameObj<RoleData>.ResolveField(x => x.PeekInAndOutOfCover));
        internal static readonly Lazy<FieldHandle<RoleData, bool>> AvoidOpponents
            = new(() => GameObj<RoleData>.ResolveField(x => x.AvoidOpponents));
        internal static readonly Lazy<FieldHandle<RoleData, bool>> CoverAgainstOpponents
            = new(() => GameObj<RoleData>.ResolveField(x => x.CoverAgainstOpponents));
        internal static readonly Lazy<FieldHandle<RoleData, bool>> ThreatFromOpponents
            = new(() => GameObj<RoleData>.ResolveField(x => x.ThreatFromOpponents));
    }

    // Agent state enum values
    public const int STATE_NONE = 0;
    public const int STATE_EVALUATING_TILES = 1;
    public const int STATE_EVALUATING_BEHAVIORS = 2;
    public const int STATE_READY_TO_EXECUTE = 3;
    public const int STATE_EXECUTING = 4;

    /// <summary>
    /// AI Agent state info for a unit.
    /// </summary>
    public class AgentInfo
    {
        public bool HasAgent { get; set; }
        public int State { get; set; }
        public string StateName { get; set; }
        public string ActiveBehavior { get; set; }
        public int BehaviorScore { get; set; }
        public int? TargetTileX { get; set; }  // Only available on SkillBehavior
        public int? TargetTileZ { get; set; }  // Only available on SkillBehavior
        public string TargetActorName { get; set; }  // Only available on SkillBehavior
        public int EvaluatedTileCount { get; set; }
        public int AvailableBehaviorCount { get; set; }
    }

    /// <summary>
    /// RoleData defines per-unit AI configuration from the EntityTemplate.
    /// Controls how the AI values different actions and positions.
    /// </summary>
    public class RoleDataInfo
    {
        // Criterion weights
        public float UtilityScale { get; set; }
        public float SafetyScale { get; set; }
        public float DistanceScale { get; set; }
        public float FriendlyFirePenalty { get; set; }

        // Behavior weights
        public float MoveWeight { get; set; }
        public float InflictDamageWeight { get; set; }
        public float InflictSuppressionWeight { get; set; }
        public float StunWeight { get; set; }

        // Behavioral settings
        public bool IsAllowedToEvadeEnemies { get; set; }
        public bool AttemptToStayOutOfSight { get; set; }
        public bool PeekInAndOutOfCover { get; set; }

        // Criterion toggles
        public bool AvoidOpponents { get; set; }
        public bool CoverAgainstOpponents { get; set; }
        public bool ThreatFromOpponents { get; set; }
    }

    /// <summary>
    /// Tile score from AI evaluation.
    /// </summary>
    public class TileScoreInfo
    {
        public int X { get; set; }
        public int Z { get; set; }
        public float UtilityScore { get; set; }
        public float SafetyScore { get; set; }
        public float DistanceScore { get; set; }
        public float CombinedScore { get; set; }  // Computed via GetScore() method
    }

    /// <summary>
    /// Behavior info from AI evaluation.
    /// </summary>
    public class BehaviorInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int Score { get; set; }
        public int? TargetTileX { get; set; }  // Only available on SkillBehavior subclass
        public int? TargetTileZ { get; set; }  // Only available on SkillBehavior subclass
        public string TargetActorName { get; set; }  // Only available on SkillBehavior subclass
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// AIFaction info for a faction.
    /// </summary>
    public class AIFactionInfo
    {
        public int FactionIndex { get; set; }
        public int ActorCount { get; set; }
        public int OpponentCount { get; set; }
        public bool IsThinking { get; set; }
    }

    /// <summary>
    /// Get the AI Agent for an actor.
    /// Returns null GameObj if the actor has no AI agent (e.g., player units).
    /// </summary>
    public static GameObj<Agent> GetAgent(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return default;

        try
        {
            var proxy = actor.AsManaged();
            if (proxy == null)
                return default;

            var agent = proxy.GetAgent();
            if (agent == null)
                return default;

            return GameObj<Agent>.Wrap(agent.Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetAgent", "Failed", ex);
            return default;
        }
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static GameObj<Agent> GetAgent(GameObj actor)
        => GetAgent(GameObj<Actor>.Wrap(actor));

    /// <summary>
    /// Get AI agent state for an actor.
    /// </summary>
    public static AgentInfo GetAgentInfo(GameObj<Actor> actor)
    {
        var info = new AgentInfo { HasAgent = false };

        if (actor.Untyped.IsNull)
            return info;

        try
        {
            var agent = GetAgent(actor);
            if (agent.Untyped.IsNull)
                return info;

            info.HasAgent = true;

            var agentProxy = agent.AsManaged();
            if (agentProxy == null)
                return info;

            // Read state via proxy method
            int state = (int)agentProxy.GetState();
            info.State = state;
            info.StateName = GetStateName(state);

            // Read evaluated tile count via proxy method
            var tiles = agentProxy.GetTiles();
            if (tiles != null)
                info.EvaluatedTileCount = tiles.Count;

            // Read behaviors via proxy method
            var behaviors = agentProxy.GetBehaviors();
            if (behaviors != null)
                info.AvailableBehaviorCount = behaviors.Count;

            // Get active behavior if ready to execute
            if (state >= STATE_READY_TO_EXECUTE)
            {
                if (Offsets.ActiveBehavior.Value.TryRead(agent, out var activeBehavior))
                {
                    var behaviorProxy = activeBehavior.AsManaged();
                    if (behaviorProxy != null)
                    {
                        info.ActiveBehavior = behaviorProxy.GetName() ?? "Unknown";
                        info.BehaviorScore = behaviorProxy.GetScore();
                    }

                    // TargetTile only exists on SkillBehavior subclass — explicit type check
                    if (GameObj<SkillBehavior>.TryWrap(activeBehavior.Untyped, out var skillBehavior))
                    {
                        if (Offsets.TargetTile.Value.TryRead(skillBehavior, out var targetTile))
                        {
                            var tileProxy = targetTile.AsManaged();
                            if (tileProxy != null)
                            {
                                info.TargetTileX = tileProxy.GetX();
                                info.TargetTileZ = tileProxy.GetZ();
                            }
                        }
                    }

                    // TargetEntity does not exist in the IL2CPP dump — removed
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetAgentInfo", "Failed", ex);
            return info;
        }
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static AgentInfo GetAgentInfo(GameObj actor)
        => GetAgentInfo(GameObj<Actor>.Wrap(actor));

    /// <summary>
    /// Get the RoleData (AI configuration) for an actor.
    /// RoleData is defined on the EntityTemplate at offset +0x310.
    /// </summary>
    public static RoleDataInfo GetRoleData(GameObj<Actor> actor)
    {
        var info = new RoleDataInfo();

        if (actor.Untyped.IsNull)
            return info;

        try
        {
            var agentProxy = actor.AsManaged().GetAgent();
            if (agentProxy == null)
                return info;

            var roleDataProxy = agentProxy.GetRole();
            if (roleDataProxy == null)
                return info;

            var roleData = GameObj<RoleData>.Wrap(roleDataProxy.Pointer);

            info.UtilityScale = Offsets.UtilityScale.Value.Read(roleData);
            info.SafetyScale = Offsets.SafetyScale.Value.Read(roleData);
            info.DistanceScale = Offsets.DistanceScale.Value.Read(roleData);
            info.FriendlyFirePenalty = Offsets.FriendlyFirePenalty.Value.Read(roleData);
            info.MoveWeight = Offsets.Move.Value.Read(roleData);
            info.InflictDamageWeight = Offsets.InflictDamage.Value.Read(roleData);
            info.InflictSuppressionWeight = Offsets.InflictSuppression.Value.Read(roleData);
            info.StunWeight = Offsets.Stun.Value.Read(roleData);
            info.IsAllowedToEvadeEnemies = Offsets.IsAllowedToEvadeEnemies.Value.Read(roleData);
            info.AttemptToStayOutOfSight = Offsets.AttemptToStayOutOfSight.Value.Read(roleData);
            info.PeekInAndOutOfCover = Offsets.PeekInAndOutOfCover.Value.Read(roleData);
            info.AvoidOpponents = Offsets.AvoidOpponents.Value.Read(roleData);
            info.CoverAgainstOpponents = Offsets.CoverAgainstOpponents.Value.Read(roleData);
            info.ThreatFromOpponents = Offsets.ThreatFromOpponents.Value.Read(roleData);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetRoleData", "Failed", ex);
            return info;
        }
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static RoleDataInfo GetRoleData(GameObj actor)
        => GetRoleData(GameObj<Actor>.Wrap(actor));

    /// <summary>
    /// Get all behaviors available to an actor's AI agent.
    /// </summary>
    public static List<BehaviorInfo> GetBehaviors(GameObj<Actor> actor)
    {
        var result = new List<BehaviorInfo>();

        if (actor.Untyped.IsNull)
            return result;

        try
        {
            var agent = GetAgent(actor);
            if (agent.Untyped.IsNull)
                return result;

            var agentProxy = agent.AsManaged();
            if (agentProxy == null)
                return result;

            var behaviors = agentProxy.GetBehaviors();
            if (behaviors == null)
                return result;

            GameObj<Behavior> activeBehavior = default;
            Offsets.ActiveBehavior.Value.TryRead(agent, out activeBehavior);

            for (int i = 0; i < behaviors.Count; i++)
            {
                var behavior = behaviors[i];
                if (behavior == null)
                    continue;

                var behaviorObj = GameObj<Behavior>.Wrap(behavior.Pointer);

                var info = new BehaviorInfo
                {
                    TypeName = behavior.GetName() ?? "Unknown",
                    Score = behavior.GetScore(),
                    IsSelected = !activeBehavior.Untyped.IsNull && behavior.Pointer == activeBehavior.Untyped.Pointer
                };

                info.Name = info.TypeName;

                // TargetTile only exists on SkillBehavior subclass — explicit type check
                if (GameObj<SkillBehavior>.TryWrap(behaviorObj.Untyped, out var skillBehavior))
                {
                    if (Offsets.TargetTile.Value.TryRead(skillBehavior, out var targetTile))
                    {
                        var tileProxy = targetTile.AsManaged();
                        if (tileProxy != null)
                        {
                            info.TargetTileX = tileProxy.GetX();
                            info.TargetTileZ = tileProxy.GetZ();
                        }
                    }
                }

                // TargetEntity does not exist in the IL2CPP dump — removed

                result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetBehaviors", "Failed", ex);
            return result;
        }
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static List<BehaviorInfo> GetBehaviors(GameObj actor)
        => GetBehaviors(GameObj<Actor>.Wrap(actor));

    /// <summary>
    /// Get tile scores from an actor's AI evaluation.
    /// Returns the top N tiles by score.
    /// </summary>
    public static List<TileScoreInfo> GetTileScores(GameObj<Actor> actor, int maxTiles = 10)
    {
        var result = new List<TileScoreInfo>();

        if (actor.Untyped.IsNull)
            return result;

        try
        {
            var agent = GetAgent(actor);
            if (agent.Untyped.IsNull)
                return result;

            var agentProxy = agent.AsManaged();
            if (agentProxy == null)
                return result;

            var tiles = agentProxy.GetTiles();
            if (tiles == null)
                return result;

            var allScores = new List<TileScoreInfo>();

            foreach (var kvp in tiles)
            {
                if (kvp.Key == null || kvp.Value == null)
                    continue;

                var tileScoreObj = GameObj<TileScore>.Wrap(kvp.Value.Pointer);

                var info = new TileScoreInfo
                {
                    X = kvp.Key.GetX(),
                    Z = kvp.Key.GetZ(),
                    UtilityScore = Offsets.UtilityScore.Value.Read(tileScoreObj),
                    SafetyScore = Offsets.SafetyScore.Value.Read(tileScoreObj),
                    DistanceScore = Offsets.DistanceScore.Value.Read(tileScoreObj),
                    CombinedScore = kvp.Value.GetScore()  // use actual GetScore() now
                };

                allScores.Add(info);

                if (allScores.Count >= 1000)
                    break;
            }

            allScores.Sort((a, b) => b.CombinedScore.CompareTo(a.CombinedScore));
            for (int i = 0; i < Math.Min(maxTiles, allScores.Count); i++)
                result.Add(allScores[i]);

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetTileScores", "Failed", ex);
            return result;
        }
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static List<TileScoreInfo> GetTileScores(GameObj actor, int maxTiles = 10)
        => GetTileScores(GameObj<Actor>.Wrap(actor), maxTiles);

    /// <summary>
    /// Get AIFaction info for a faction index.
    /// </summary>
    public static AIFactionInfo GetAIFactionInfo(int factionIndex)
    {
        var info = new AIFactionInfo { FactionIndex = factionIndex };

        try
        {
            // Find AIFaction for this faction
            var aiFactions = GameQuery.FindAll<AIFaction>();
            foreach (var aiFaction in aiFactions)
            {
                if (aiFaction.m_FactionIndex == factionIndex)
                {
                    // AIFaction uses m_Actors, not m_Agents
                    info.ActorCount = aiFaction.m_Actors?.Count ?? 0;

                    // m_Opponents is List<Opponent> on AIFaction
                    info.OpponentCount = aiFaction.m_Opponents?.Count ?? 0;

                    // IsThinking() is a real method on AIFaction
                    info.IsThinking = aiFaction.IsThinking();
                    break;
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetAIFactionInfo", "Failed", ex);
            return info;
        }
    }

    /// <summary>
    /// Get what the AI is planning for an actor.
    /// Convenience method that returns a summary of the AI's current intent.
    /// </summary>
    public static string GetAIIntent(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return "No actor";

        var info = GetAgentInfo(actor);
        if (!info.HasAgent)
            return "No AI agent (player unit?)";

        if (info.State < STATE_READY_TO_EXECUTE)
            return $"Evaluating ({info.StateName})";

        if (string.IsNullOrEmpty(info.ActiveBehavior))
            return "No behavior selected";

        string intent = $"{info.ActiveBehavior} (score: {info.BehaviorScore})";

        if (!string.IsNullOrEmpty(info.TargetActorName))
            intent += $" -> {info.TargetActorName}";
        else if (info.TargetTileX.HasValue || info.TargetTileZ.HasValue)
            intent += $" -> ({info.TargetTileX ?? 0}, {info.TargetTileZ ?? 0})";

        return intent;
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static string GetAIIntent(GameObj actor)
        => GetAIIntent(GameObj<Actor>.Wrap(actor));

    /// <summary>
    /// Register console commands for AI inspection.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("ai", "[actor_name]", "Show AI agent info for actor", args =>
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

            var info = GetAgentInfo(GameObj<Actor>.Wrap(actor.Pointer));
            if (!info.HasAgent)
                return $"{actor.DebugName}: No AI agent (player unit?)";

            string targetStr = !string.IsNullOrEmpty(info.TargetActorName)
                ? info.TargetActorName
                : $"({info.TargetTileX ?? 0}, {info.TargetTileZ ?? 0})";
            return $"{actor.DebugName}:\n" +
                   $"  State: {info.StateName}\n" +
                   $"  Tiles evaluated: {info.EvaluatedTileCount}\n" +
                   $"  Behaviors: {info.AvailableBehaviorCount}\n" +
                   $"  Active: {info.ActiveBehavior ?? "none"} (score: {info.BehaviorScore})\n" +
                   $"  Target: {targetStr}";
        });

        DevConsole.RegisterCommand("airole", "[actor_name]", "Show AI RoleData for actor", args =>
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

            var role = GetRoleData(GameObj<Actor>.Wrap(actor.Pointer));
            return $"{actor.DebugName} RoleData:\n" +
                   $"  Utility: {role.UtilityScale:F1}, Safety: {role.SafetyScale:F1}, Distance: {role.DistanceScale:F1}\n" +
                   $"  Move: {role.MoveWeight:F1}, Damage: {role.InflictDamageWeight:F1}, Suppress: {role.InflictSuppressionWeight:F1}\n" +
                   $"  Evade: {role.IsAllowedToEvadeEnemies}, StayHidden: {role.AttemptToStayOutOfSight}, Peek: {role.PeekInAndOutOfCover}";
        });

        DevConsole.RegisterCommand("aibehaviors", "[actor_name]", "List AI behaviors for actor", args =>
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

            var behaviors = GetBehaviors(GameObj<Actor>.Wrap(actor.Pointer));
            if (behaviors.Count == 0)
                return $"{actor.DebugName}: No behaviors";

            var lines = new List<string> { $"{actor.DebugName} behaviors:" };
            foreach (var b in behaviors)
            {
                string marker = b.IsSelected ? " [SELECTED]" : "";
                string target = !string.IsNullOrEmpty(b.TargetActorName)
                    ? $" -> {b.TargetActorName}"
                    : b.TargetTileX.HasValue ? $" -> ({b.TargetTileX}, {b.TargetTileZ})" : "";
                lines.Add($"  {b.TypeName}: {b.Score}{target}{marker}");
            }
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("aitiles", "[actor_name] [count]", "Show top tile scores for actor", args =>
        {
            Actor actor;
            int count = 5;

            if (args.Length > 0 && int.TryParse(args[^1], out int n))
            {
                count = n;
                args = args[..^1];
            }

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

            var tiles = GetTileScores(GameObj<Actor>.Wrap(actor.Pointer), count);
            if (tiles.Count == 0)
                return $"{actor.DebugName}: No tile scores";

            var lines = new List<string> { $"{actor.DebugName} top {count} tiles:" };
            foreach (var t in tiles)
            {
                lines.Add($"  ({t.X}, {t.Z}): score={t.CombinedScore:F1} (util={t.UtilityScore:F1}, safe={t.SafetyScore:F1})");
            }
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("aiintent", "[actor_name]", "Show what the AI is planning", args =>
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

            return $"{actor.DebugName}: {GetAIIntent(GameObj<Actor>.Wrap(actor.Pointer))}";
        });
    }

    // ==========================================================================
    // WRITE METHODS
    // ==========================================================================
    // THREADING WARNING: These methods modify AI state. They are ONLY safe to call:
    //   1. Before the faction's turn begins (e.g., in OnTurnStart hook)
    //   2. After the faction's turn ends
    //   3. When IsAnyFactionThinking() returns false
    //
    // Calling these during parallel evaluation WILL cause race conditions and crashes.
    // ==========================================================================

    /// <summary>
    /// Check if any AI faction is currently thinking (parallel tile/behavior scoring).
    /// When this returns true, it is NOT safe to write to AI state.
    /// </summary>
    public static bool IsAnyFactionThinking()
    {
        try
        {
            var aiFactions = GameQuery.FindAll<AIFaction>();
            foreach (var aiFaction in aiFactions)
            {
                // Try m_IsThinking or m_Thinking field
                if (aiFaction.IsThinking()) return true;
            }
            return false;
        }
        catch
        {
            return true; // Assume unsafe if we can't check
        }
    }

    /// <summary>
    /// Get the RoleData object for an actor, for direct field modification.
    /// Returns GameObj.Null if actor has no RoleData.
    /// </summary>
    public static GameObj<RoleData> GetRoleDataObject(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return default;

        try
        {
            var agentProxy = actor.AsManaged().GetAgent();
            if (agentProxy == null)
                return default;

            var roleDataProxy = agentProxy.GetRole();
            if (roleDataProxy == null)
                return default;

            return GameObj<RoleData>.Wrap(roleDataProxy.Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetRoleDataObject", "Failed", ex);
            return default;
        }
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static GameObj<RoleData> GetRoleDataObject(GameObj actor)
        => GetRoleDataObject(GameObj<Actor>.Wrap(actor));

    /// <summary>
    /// Set a float field on an actor's RoleData.
    /// Returns true if successful, false if write failed or actor has no RoleData.
    ///
    /// Common fields: UtilityScale, SafetyScale, DistanceScale, FriendlyFirePenalty,
    ///                Move, InflictDamage, InflictSuppression, Stun
    /// </summary>
    public static bool SetRoleDataFloat(GameObj<Actor> actor, string fieldName, float value)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.SetRoleDataFloat",
                $"Cannot write during AI evaluation - will cause race condition. Field: {fieldName}");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.Untyped.IsNull)
        {
            ModError.ReportInternal("AI.SetRoleDataFloat", $"Actor has no RoleData");
            return false;
        }

        var roleKlass = IL2CPP.il2cpp_object_get_class(roleData.Untyped.Pointer);
        var offset = OffsetCache.GetOrResolve(roleKlass, fieldName);
        if (offset == 0)
        {
            ModError.ReportInternal("AI.SetRoleDataFloat", $"Could not resolve offset for field: {fieldName}");
            return false;
        }

        roleData.Untyped.WriteFloat(offset, value);
        return true;
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static bool SetRoleDataFloat(GameObj actor, string fieldName, float value)
        => SetRoleDataFloat(GameObj<Actor>.Wrap(actor), fieldName, value);

    /// <summary>
    /// Set a bool field on an actor's RoleData.
    /// Returns true if successful.
    ///
    /// Common fields: IsAllowedToEvadeEnemies, AttemptToStayOutOfSight, PeekInAndOutOfCover,
    ///                AvoidOpponents, CoverAgainstOpponents, ThreatFromOpponents
    /// </summary>
    public static bool SetRoleDataBool(GameObj<Actor> actor, string fieldName, bool value)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.SetRoleDataBool",
                $"Cannot write during AI evaluation - will cause race condition. Field: {fieldName}");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.Untyped.IsNull)
        {
            ModError.ReportInternal("AI.SetRoleDataBool", $"Actor has no RoleData");
            return false;
        }

        var roleKlass = IL2CPP.il2cpp_object_get_class(roleData.Untyped.Pointer);
        var offset = OffsetCache.GetOrResolve(roleKlass, fieldName);
        if (offset == 0)
        {
            ModError.ReportInternal("AI.SetRoleDataBool", $"Could not resolve offset for field: {fieldName}");
            return false;
        }

        roleData.Untyped.WriteInt(offset, value ? 1 : 0);
        return true;
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static bool SetRoleDataBool(GameObj actor, string fieldName, bool value)
        => SetRoleDataBool(GameObj<Actor>.Wrap(actor), fieldName, value);

    /// <summary>
    /// Apply a complete RoleData configuration to an actor.
    /// Only the fields that differ from the current values will be written.
    /// </summary>
    public static bool ApplyRoleData(GameObj<Actor> actor, RoleDataInfo newRole)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.ApplyRoleData",
                "Cannot write during AI evaluation - will cause race condition");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.Untyped.IsNull)
        {
            ModError.ReportInternal("AI.ApplyRoleData", "Actor has no RoleData");
            return false;
        }

        try
        {
            // Write criterion weights
            Offsets.UtilityScale.Value.Write(roleData, newRole.UtilityScale);
            Offsets.SafetyScale.Value.Write(roleData, newRole.SafetyScale);
            Offsets.DistanceScale.Value.Write(roleData, newRole.DistanceScale);
            Offsets.FriendlyFirePenalty.Value.Write(roleData, newRole.FriendlyFirePenalty);

            // Write behavior weights
            Offsets.Move.Value.Write(roleData, newRole.MoveWeight);
            Offsets.InflictDamage.Value.Write(roleData, newRole.InflictDamageWeight);
            Offsets.InflictSuppression.Value.Write(roleData, newRole.InflictSuppressionWeight);
            Offsets.Stun.Value.Write(roleData, newRole.StunWeight);

            // Write behavioral settings
            Offsets.IsAllowedToEvadeEnemies.Value.Write(roleData, newRole.IsAllowedToEvadeEnemies);
            Offsets.AttemptToStayOutOfSight.Value.Write(roleData, newRole.AttemptToStayOutOfSight);
            Offsets.PeekInAndOutOfCover.Value.Write(roleData, newRole.PeekInAndOutOfCover);

            // Write criterion toggles
            Offsets.AvoidOpponents.Value.Write(roleData, newRole.AvoidOpponents);
            Offsets.CoverAgainstOpponents.Value.Write(roleData, newRole.CoverAgainstOpponents);
            Offsets.ThreatFromOpponents.Value.Write(roleData, newRole.ThreatFromOpponents);

            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.ApplyRoleData", "Failed during write", ex);
            return false;
        }
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static bool ApplyRoleData(GameObj actor, RoleDataInfo newRole)
        => ApplyRoleData(GameObj<Actor>.Wrap(actor), newRole);

    /// <summary>
    /// Force-set a behavior's score. Use with extreme caution.
    /// This can override AI decisions but may cause unexpected behavior.
    /// </summary>
    public static bool SetBehaviorScore(GameObj<Actor> actor, string behaviorTypeName, int score)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.SetBehaviorScore",
                "Cannot write during AI evaluation - will cause race condition");
            return false;
        }

        var agent = GetAgent(actor);
        if (agent.Untyped.IsNull)
            return false;

        var agentProxy = agent.AsManaged();
        if (agentProxy == null)
            return false;

        var behaviors = agentProxy.GetBehaviors();
        if (behaviors == null)
            return false;

        for (int i = 0; i < behaviors.Count; i++)
        {
            var behavior = behaviors[i];
            if (behavior == null)
                continue;

            if (behavior.GetName() == behaviorTypeName)
            {
                var behaviorObj = GameObj<Behavior>.Wrap(behavior.Pointer);
                Offsets.Score.Value.Write(behaviorObj, score);
                return true;
            }
        }

        return false; // Behavior not found
    }

    [Obsolete("Use GameObj<Actor> overload")]
    public static bool SetBehaviorScore(GameObj actor, string behaviorTypeName, int score)
        => SetBehaviorScore(GameObj<Actor>.Wrap(actor), behaviorTypeName, score);

    // --- Internal helpers ---

    private static string GetStateName(int state)
    {
        return state switch
        {
            STATE_NONE => "None",
            STATE_EVALUATING_TILES => "EvaluatingTiles",
            STATE_EVALUATING_BEHAVIORS => "EvaluatingBehaviors",
            STATE_READY_TO_EXECUTE => "ReadyToExecute",
            STATE_EXECUTING => "Executing",
            _ => $"Unknown({state})"
        };
    }
}
