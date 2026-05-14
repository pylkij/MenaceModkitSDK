using Il2CppInterop.Runtime;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;

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
    public static GameObj GetAgent(GameObj actor)
    {
        if (actor.IsNull)
            return GameObj.Null;

        try
        {
            var actorKlass = IL2CPP.il2cpp_object_get_class(actor.Pointer);

            var agentObj = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "Agent"));
            if (!agentObj.IsNull)
                return agentObj;

            agentObj = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "m_Agent"));
            if (!agentObj.IsNull)
                return agentObj;

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetAgent", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get AI agent state for an actor.
    /// </summary>
    public static AgentInfo GetAgentInfo(GameObj actor)
    {
        var info = new AgentInfo { HasAgent = false };

        if (actor.IsNull)
            return info;

        try
        {
            var agent = GetAgent(actor);
            if (agent.IsNull)
                return info;

            info.HasAgent = true;

            var agentKlass = IL2CPP.il2cpp_object_get_class(agent.Pointer);

            // Read state
            int state = agent.ReadInt(OffsetCache.GetOrResolve(agentKlass, "m_State"));
            info.State = state;
            info.StateName = GetStateName(state);

            // Read evaluated tile count
            var tilesDict = agent.ReadObj(OffsetCache.GetOrResolve(agentKlass, "m_Tiles"));
            if (!tilesDict.IsNull)
            {
                var tilesDictKlass = IL2CPP.il2cpp_object_get_class(tilesDict.Pointer);
                info.EvaluatedTileCount = tilesDict.ReadInt(OffsetCache.GetOrResolve(tilesDictKlass, "_count"));
            }

            // Read behaviors
            var behaviors = agent.ReadObj(OffsetCache.GetOrResolve(agentKlass, "m_Behaviors"));
            if (!behaviors.IsNull)
            {
                info.AvailableBehaviorCount = behaviors.ReadInt(OffsetCache.ListSizeOffset);
            }

            // Get active behavior if ready to execute
            if (state >= STATE_READY_TO_EXECUTE)
            {
                var activeBehavior = agent.ReadObj(OffsetCache.GetOrResolve(agentKlass, "m_ActiveBehavior"));
                if (!activeBehavior.IsNull)
                {
                    var behaviorKlass = IL2CPP.il2cpp_object_get_class(activeBehavior.Pointer);

                    info.ActiveBehavior = activeBehavior.GetType()?.Name ?? "Unknown";
                    info.BehaviorScore = activeBehavior.ReadInt(OffsetCache.GetOrResolve(behaviorKlass, "Score"));

                    // TargetTile and TargetEntity only exist on SkillBehavior subclass
                    // Try to read them gracefully - will return null/default if not present
                    var targetTile = activeBehavior.ReadObj(OffsetCache.GetOrResolve(behaviorKlass, "TargetTile"));
                    if (!targetTile.IsNull)
                    {
                        var tileKlass = IL2CPP.il2cpp_object_get_class(targetTile.Pointer);
                        info.TargetTileX = targetTile.ReadInt(OffsetCache.GetOrResolve(tileKlass, "X"));
                        info.TargetTileZ = targetTile.ReadInt(OffsetCache.GetOrResolve(tileKlass, "Z"));
                    }

                    var targetEntity = activeBehavior.ReadObj(OffsetCache.GetOrResolve(behaviorKlass, "TargetEntity"));
                    if (!targetEntity.IsNull)
                    {
                        info.TargetActorName = targetEntity.GetName();
                    }
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

    /// <summary>
    /// Get the RoleData (AI configuration) for an actor.
    /// RoleData is defined on the EntityTemplate at offset +0x310.
    /// </summary>
    public static RoleDataInfo GetRoleData(GameObj actor)
    {
        var info = new RoleDataInfo();

        if (actor.IsNull)
            return info;

        try
        {
            var actorKlass = IL2CPP.il2cpp_object_get_class(actor.Pointer);

            // Get EntityTemplate from actor - try various possible field paths
            var template = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "Template"));
            if (template.IsNull)
                template = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "m_Template"));
            if (template.IsNull)
            {
                // Try via Entity base class hierarchy
                var entity = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "m_Entity"));
                if (!entity.IsNull)
                {
                    var entityKlass = IL2CPP.il2cpp_object_get_class(entity.Pointer);
                    template = entity.ReadObj(OffsetCache.GetOrResolve(entityKlass, "Template"));
                }
            }
            if (template.IsNull)
                return info;

            var templateKlass = IL2CPP.il2cpp_object_get_class(template.Pointer);

            // Get AIRole/RoleData from template
            var roleData = template.ReadObj(OffsetCache.GetOrResolve(templateKlass, "AIRole"));
            if (roleData.IsNull)
                roleData = template.ReadObj(OffsetCache.GetOrResolve(templateKlass, "m_AIRole"));
            if (roleData.IsNull)
                return info;

            var roleKlass = IL2CPP.il2cpp_object_get_class(roleData.Pointer);

            // Read criterion weights
            info.UtilityScale = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "UtilityScale"));
            info.SafetyScale = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "SafetyScale"));
            info.DistanceScale = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "DistanceScale"));
            info.FriendlyFirePenalty = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "FriendlyFirePenalty"));

            // Read behavior weights
            info.MoveWeight = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "Move"));
            info.InflictDamageWeight = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "InflictDamage"));
            info.InflictSuppressionWeight = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "InflictSuppression"));
            info.StunWeight = roleData.ReadFloat(OffsetCache.GetOrResolve(roleKlass, "Stun"));

            // Read behavioral settings
            info.IsAllowedToEvadeEnemies = roleData.ReadBool(OffsetCache.GetOrResolve(roleKlass, "IsAllowedToEvadeEnemies"));
            info.AttemptToStayOutOfSight = roleData.ReadBool(OffsetCache.GetOrResolve(roleKlass, "AttemptToStayOutOfSight"));
            info.PeekInAndOutOfCover = roleData.ReadBool(OffsetCache.GetOrResolve(roleKlass, "PeekInAndOutOfCover"));

            // Read criterion toggles
            info.AvoidOpponents = roleData.ReadBool(OffsetCache.GetOrResolve(roleKlass, "AvoidOpponents"));
            info.CoverAgainstOpponents = roleData.ReadBool(OffsetCache.GetOrResolve(roleKlass, "CoverAgainstOpponents"));
            info.ThreatFromOpponents = roleData.ReadBool(OffsetCache.GetOrResolve(roleKlass, "ThreatFromOpponents"));

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetRoleData", "Failed", ex);
            return info;
        }
    }

    /// <summary>
    /// Get all behaviors available to an actor's AI agent.
    /// </summary>
    public static List<BehaviorInfo> GetBehaviors(GameObj actor)
    {
        var result = new List<BehaviorInfo>();

        if (actor.IsNull)
            return result;

        try
        {
            var agent = GetAgent(actor);
            if (agent.IsNull)
                return result;

            var agentKlass = IL2CPP.il2cpp_object_get_class(agent.Pointer);

            var behaviors = agent.ReadObj(OffsetCache.GetOrResolve(agentKlass, "m_Behaviors"));
            if (behaviors.IsNull)
                return result;

            var activeBehavior = agent.ReadObj(OffsetCache.GetOrResolve(agentKlass, "m_ActiveBehavior"));

            // Iterate behaviors list
            int count = behaviors.ReadInt(OffsetCache.ListSizeOffset);
            var itemsPtr = behaviors.ReadPtr(OffsetCache.ListItemsOffset);
            if (itemsPtr == IntPtr.Zero)
                return result;

            var items = new GameArray(itemsPtr);
            for (int i = 0; i < count; i++)
            {
                var behavior = items[i];
                if (behavior.IsNull)
                    continue;

                var behaviorKlass = IL2CPP.il2cpp_object_get_class(behavior.Pointer);

                var info = new BehaviorInfo
                {
                    TypeName = behavior.GetType()?.Name ?? "Unknown",
                    Score = behavior.ReadInt(OffsetCache.GetOrResolve(behaviorKlass, "Score")),
                    IsSelected = !activeBehavior.IsNull && behavior.Pointer == activeBehavior.Pointer
                };

                info.Name = info.TypeName;

                // TargetTile and TargetEntity only exist on SkillBehavior subclass
                // Read gracefully - will be null if not present on this behavior type
                var targetTile = behavior.ReadObj(OffsetCache.GetOrResolve(behaviorKlass, "TargetTile"));
                if (!targetTile.IsNull)
                {
                    var tileKlass = IL2CPP.il2cpp_object_get_class(targetTile.Pointer);
                    info.TargetTileX = targetTile.ReadInt(OffsetCache.GetOrResolve(tileKlass, "X"));
                    info.TargetTileZ = targetTile.ReadInt(OffsetCache.GetOrResolve(tileKlass, "Z"));
                }

                var targetEntity = behavior.ReadObj(OffsetCache.GetOrResolve(behaviorKlass, "TargetEntity"));
                if (!targetEntity.IsNull)
                {
                    info.TargetActorName = targetEntity.GetName();
                }

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

    /// <summary>
    /// Get tile scores from an actor's AI evaluation.
    /// Returns the top N tiles by score.
    /// </summary>
    public static List<TileScoreInfo> GetTileScores(GameObj actor, int maxTiles = 10)
    {
        var result = new List<TileScoreInfo>();

        if (actor.IsNull)
            return result;

        try
        {
            var agent = GetAgent(actor);
            if (agent.IsNull)
                return result;

            var agentKlass = IL2CPP.il2cpp_object_get_class(agent.Pointer);
            var tilesDict = agent.ReadObj(OffsetCache.GetOrResolve(agentKlass, "m_Tiles"));
            if (tilesDict.IsNull)
                return result;

            // Iterate dictionary using GameDict wrapper
            var dict = new GameDict(tilesDict);
            var allScores = new List<TileScoreInfo>();

            foreach (var (tileKey, tileScore) in dict)
            {
                if (tileKey.IsNull || tileScore.IsNull)
                    continue;

                var tileKlass = IL2CPP.il2cpp_object_get_class(tileKey.Pointer);
                var tileScoreKlass = IL2CPP.il2cpp_object_get_class(tileScore.Pointer);

                float utilScore = tileScore.ReadFloat(OffsetCache.GetOrResolve(tileScoreKlass, "UtilityScore"));
                float safeScore = tileScore.ReadFloat(OffsetCache.GetOrResolve(tileScoreKlass, "SafetyScore"));
                float distScore = tileScore.ReadFloat(OffsetCache.GetOrResolve(tileScoreKlass, "DistanceScore"));
                var info = new TileScoreInfo
                {
                    X = tileKey.ReadInt(OffsetCache.GetOrResolve(tileKlass, "X")),
                    Z = tileKey.ReadInt(OffsetCache.GetOrResolve(tileKlass, "Z")),
                    UtilityScore = utilScore,
                    SafetyScore = safeScore,
                    DistanceScore = distScore,
                    // Approximate combined score (actual GetScore() may weight differently)
                    CombinedScore = utilScore + safeScore + distScore
                };

                allScores.Add(info);

                // Limit iterations for safety
                if (allScores.Count >= 1000)
                    break;
            }

            // Sort by CombinedScore descending and take top N
            allScores.Sort((a, b) => b.CombinedScore.CompareTo(a.CombinedScore));
            for (int i = 0; i < Math.Min(maxTiles, allScores.Count); i++)
            {
                result.Add(allScores[i]);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetTileScores", "Failed", ex);
            return result;
        }
    }

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
    public static string GetAIIntent(GameObj actor)
    {
        if (actor.IsNull)
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

            var info = GetAgentInfo(new GameObj(actor.Pointer));
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

            var role = GetRoleData(new GameObj(actor.Pointer));
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

            var behaviors = GetBehaviors(new GameObj(actor.Pointer));
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

            var tiles = GetTileScores(new GameObj(actor.Pointer), count);
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

            return $"{actor.DebugName}: {GetAIIntent(new GameObj(actor.Pointer))}";
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
    public static GameObj GetRoleDataObject(GameObj actor)
    {
        if (actor.IsNull)
            return GameObj.Null;

        try
        {
            var actorKlass = IL2CPP.il2cpp_object_get_class(actor.Pointer);

            var template = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "Template"));
            if (template.IsNull)
                template = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "m_Template"));
            if (template.IsNull)
            {
                var entity = actor.ReadObj(OffsetCache.GetOrResolve(actorKlass, "m_Entity"));
                if (!entity.IsNull)
                {
                    var entityKlass = IL2CPP.il2cpp_object_get_class(entity.Pointer);
                    template = entity.ReadObj(OffsetCache.GetOrResolve(entityKlass, "Template"));
                }
            }
            if (template.IsNull)
                return GameObj.Null;

            var templateKlass = IL2CPP.il2cpp_object_get_class(template.Pointer);

            var roleData = template.ReadObj(OffsetCache.GetOrResolve(templateKlass, "AIRole"));
            if (roleData.IsNull)
                roleData = template.ReadObj(OffsetCache.GetOrResolve(templateKlass, "m_AIRole"));

            return roleData;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetRoleDataObject", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Set a float field on an actor's RoleData.
    /// Returns true if successful, false if write failed or actor has no RoleData.
    ///
    /// Common fields: UtilityScale, SafetyScale, DistanceScale, FriendlyFirePenalty,
    ///                Move, InflictDamage, InflictSuppression, Stun
    /// </summary>
    public static bool SetRoleDataFloat(GameObj actor, string fieldName, float value)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.SetRoleDataFloat",
                $"Cannot write during AI evaluation - will cause race condition. Field: {fieldName}");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.IsNull)
        {
            ModError.ReportInternal("AI.SetRoleDataFloat", $"Actor has no RoleData");
            return false;
        }

        var roleKlass = IL2CPP.il2cpp_object_get_class(roleData.Pointer);
        var offset = OffsetCache.GetOrResolve(roleKlass, fieldName);
        if (offset == 0)
        {
            ModError.ReportInternal("AI.SetRoleDataFloat", $"Could not resolve offset for field: {fieldName}");
            return false;
        }

        roleData.WriteFloat(offset, value);
        return true;
    }

    /// <summary>
    /// Set a bool field on an actor's RoleData.
    /// Returns true if successful.
    ///
    /// Common fields: IsAllowedToEvadeEnemies, AttemptToStayOutOfSight, PeekInAndOutOfCover,
    ///                AvoidOpponents, CoverAgainstOpponents, ThreatFromOpponents
    /// </summary>
    public static bool SetRoleDataBool(GameObj actor, string fieldName, bool value)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.SetRoleDataBool",
                $"Cannot write during AI evaluation - will cause race condition. Field: {fieldName}");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.IsNull)
        {
            ModError.ReportInternal("AI.SetRoleDataBool", $"Actor has no RoleData");
            return false;
        }

        var roleKlass = IL2CPP.il2cpp_object_get_class(roleData.Pointer);
        var offset = OffsetCache.GetOrResolve(roleKlass, fieldName);
        if (offset == 0)
        {
            ModError.ReportInternal("AI.SetRoleDataBool", $"Could not resolve offset for field: {fieldName}");
            return false;
        }

        roleData.WriteInt(offset, value ? 1 : 0);
        return true;
    }

    /// <summary>
    /// Apply a complete RoleData configuration to an actor.
    /// Only the fields that differ from the current values will be written.
    /// </summary>
    public static bool ApplyRoleData(GameObj actor, RoleDataInfo newRole)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.ApplyRoleData",
                "Cannot write during AI evaluation - will cause race condition");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.IsNull)
        {
            ModError.ReportInternal("AI.ApplyRoleData", "Actor has no RoleData");
            return false;
        }

        var roleKlass = IL2CPP.il2cpp_object_get_class(roleData.Pointer);

        try
        {
            // Write criterion weights
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "UtilityScale"), newRole.UtilityScale);
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "SafetyScale"), newRole.SafetyScale);
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "DistanceScale"), newRole.DistanceScale);
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "FriendlyFirePenalty"), newRole.FriendlyFirePenalty);

            // Write behavior weights
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "Move"), newRole.MoveWeight);
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "InflictDamage"), newRole.InflictDamageWeight);
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "InflictSuppression"), newRole.InflictSuppressionWeight);
            roleData.WriteFloat(OffsetCache.GetOrResolve(roleKlass, "Stun"), newRole.StunWeight);

            // Write behavioral settings
            roleData.WriteInt(OffsetCache.GetOrResolve(roleKlass, "IsAllowedToEvadeEnemies"), newRole.IsAllowedToEvadeEnemies ? 1 : 0);
            roleData.WriteInt(OffsetCache.GetOrResolve(roleKlass, "AttemptToStayOutOfSight"), newRole.AttemptToStayOutOfSight ? 1 : 0);
            roleData.WriteInt(OffsetCache.GetOrResolve(roleKlass, "PeekInAndOutOfCover"), newRole.PeekInAndOutOfCover ? 1 : 0);

            // Write criterion toggles
            roleData.WriteInt(OffsetCache.GetOrResolve(roleKlass, "AvoidOpponents"), newRole.AvoidOpponents ? 1 : 0);
            roleData.WriteInt(OffsetCache.GetOrResolve(roleKlass, "CoverAgainstOpponents"), newRole.CoverAgainstOpponents ? 1 : 0);
            roleData.WriteInt(OffsetCache.GetOrResolve(roleKlass, "ThreatFromOpponents"), newRole.ThreatFromOpponents ? 1 : 0);

            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.ApplyRoleData", "Failed during write", ex);
            return false;
        }
    }

    /// <summary>
    /// Force-set a behavior's score. Use with extreme caution.
    /// This can override AI decisions but may cause unexpected behavior.
    /// </summary>
    public static bool SetBehaviorScore(GameObj actor, string behaviorTypeName, int score)
    {
        if (IsAnyFactionThinking())
        {
            ModError.ReportInternal("AI.SetBehaviorScore",
                "Cannot write during AI evaluation - will cause race condition");
            return false;
        }

        var agent = GetAgent(actor);
        if (agent.IsNull)
            return false;

        var agentKlass = IL2CPP.il2cpp_object_get_class(agent.Pointer);

        var behaviors = agent.ReadObj(OffsetCache.GetOrResolve(agentKlass, "m_Behaviors"));
        if (behaviors.IsNull)
            return false;

        int count = behaviors.ReadInt(OffsetCache.ListSizeOffset);
        var itemsPtr = behaviors.ReadPtr(OffsetCache.ListItemsOffset);
        if (itemsPtr == IntPtr.Zero)
            return false;

        var items = new GameArray(itemsPtr);
        for (int i = 0; i < count; i++)
        {
            var behavior = items[i];
            if (behavior.IsNull)
                continue;

            var typeName = behavior.GetType()?.Name;
            if (typeName == behaviorTypeName)
            {
                var behaviorKlass = IL2CPP.il2cpp_object_get_class(behavior.Pointer);
                behavior.WriteInt(OffsetCache.GetOrResolve(behaviorKlass, "Score"), score);
                return true;
            }
        }

        return false; // Behavior not found
    }

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
