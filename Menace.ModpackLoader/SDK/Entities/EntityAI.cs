using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Menace.SDK.Internal;
using System;

namespace Menace.SDK;

/// <summary>
/// SDK extension for AI control and manipulation including behavior forcing,
/// AI pause/resume, and morale-based threat/flee control.
///
/// Based on reverse engineering findings from agent a6148de:
/// - TacticalManager.m_IsAIPaused at 0xB9 (byte/bool)
/// - Agent.m_Actor at 0x18, m_ActiveBehavior at 0x28, m_State at 0x3C
/// - Agent.m_Behaviors list at 0x20 (each behavior has Score at +0x18)
/// - Actor.m_Morale at 0x158 (float) - controls flee/aggressive states
/// - No direct threat override mechanism - use morale system as proxy
///
/// THREAD SAFETY WARNING:
/// AI evaluation runs in parallel (multi-threaded). Most write methods are ONLY safe to call:
/// 1. During TacticalEventHooks.OnTurnStart/OnTurnEnd
/// 2. When AI.IsAnyFactionThinking() returns false
/// 3. When the game is paused
/// Calling these during parallel evaluation WILL cause race conditions and crashes.
/// </summary>
public static class EntityAI
{
    private static class Offsets
    {
        // Actor fields
        // NOTE: OFFSET_ACTOR_MORALE was 0x160 — corrected to 0x158 (m_Morale on Actor)
        // NOTE: OFFSET_ACTOR_AGENT was 0x18 — corrected to 0xC8 (m_Agent on Actor)
        internal static readonly Lazy<FieldHandle<Actor, float>> Actor_Morale
            = new(() => GameObj<Actor>.ResolveField(x => x.m_Morale));

        internal static readonly Lazy<ObjFieldHandle<Actor, Agent>> Actor_Agent
            = new(() => GameObj<Actor>.ResolveObjField(x => x.m_Agent));

        // Agent fields
        internal static readonly Lazy<ObjFieldHandle<Agent, Behavior>> Agent_ActiveBehavior
            = new(() => GameObj<Agent>.ResolveObjField(x => x.m_ActiveBehavior));

        // m_Behaviors is List<Behavior> — accessed via GameArray after reading the raw pointer
        internal static readonly Lazy<FieldHandle<Agent, IntPtr>> Agent_Behaviors
            = new(() => GameObj<Agent>.FieldAt<IntPtr>(0x20, "m_Behaviors"));

        // Behavior fields
        internal static readonly Lazy<FieldHandle<Behavior, int>> Behavior_Score
            = new(() => GameObj<Behavior>.ResolveField(x => x.m_Score));

        // SkillBehavior fields — used for target matching in ForceNextAction
        internal static readonly Lazy<ObjFieldHandle<Il2CppMenace.Tactical.AI.SkillBehavior, Tile>> SkillBehavior_TargetTile
            = new(() => GameObj<Il2CppMenace.Tactical.AI.SkillBehavior>.ResolveObjField(x => x.m_TargetTile));

        // TacticalManager fields
        // m_IsAIPaused confirmed at 0xB9 — SetAIPaused(bool) method exists and is preferred.
        // This entry exists only for the fallback Marshal.WriteByte paths, which are dead code
        // now that SetAIPaused is confirmed. Those paths will be removed during migration.
        internal static readonly Lazy<FieldHandle<TacticalManager, bool>> TacticalManager_IsAIPaused
            = new(() => GameObj<TacticalManager>.ResolveField(x => x.m_IsAIPaused));
    }

    // Morale thresholds (from game constants)
    public const float MORALE_PANICKED = 0.0f;      // Triggers flee state
    public const float MORALE_SHAKEN = 25.0f;       // Low morale
    public const float MORALE_STEADY = 50.0f;       // Normal morale
    public const float MORALE_CONFIDENT = 75.0f;    // High morale
    public const float MORALE_FEARLESS = 100.0f;    // Blocks flee state

    /// <summary>
    /// Result of an AI manipulation operation.
    /// </summary>
    public class AIResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        public static AIResult Failed(string error) => new() { Success = false, Error = error };
        public static AIResult Ok() => new() { Success = true };
    }

    /// <summary>
    /// Force an actor to prioritize a specific action on their next turn.
    /// This manipulates the Agent.m_Behaviors list by boosting the score of behaviors
    /// matching the specified action type.
    ///
    /// THREAD SAFETY: Call only during OnTurnStart/OnTurnEnd or when AI is not evaluating.
    /// </summary>
    /// <param name="actor">The actor whose AI to manipulate</param>
    /// <param name="actionType">Type of action to prioritize (e.g., "AttackBehavior", "MoveBehavior")</param>
    /// <param name="target">Optional target actor for targeted actions</param>
    /// <param name="scoreBoost">Score boost to apply (default: 10000 to ensure selection)</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// This method works by finding behaviors matching the action type and boosting their
    /// Score field. The AI will then select the highest-scored behavior on its next evaluation.
    ///
    /// Common action types:
    /// - "AttackBehavior" - Forces attack actions
    /// - "MoveBehavior" - Forces movement
    /// - "SkillBehavior" - Forces skill/ability use
    /// - "ReloadBehavior" - Forces reload
    /// - "WaitBehavior" - Forces wait/overwatch
    ///
    /// Example:
    ///   EntityAI.ForceNextAction(enemy, "AttackBehavior", playerUnit);
    /// </remarks>
    public static AIResult ForceNextAction(GameObj<Actor> actor, string actionType, GameObj<Actor> target = default, int scoreBoost = 10000)
    {
        if (actor.Untyped.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate AI during evaluation (thread safety)");

        try
        {
            var agent = GetAgent(actor);
            if (agent.Untyped.IsNull)
                return AIResult.Failed("Actor has no AI agent");

            if (!Offsets.Agent_Behaviors.Value.TryRead(agent, out var behaviorsPtr) || behaviorsPtr == IntPtr.Zero)
                return AIResult.Failed("Agent has no behaviors list");

            var behaviors = GameObj.FromPointer(behaviorsPtr);
            int count = behaviors.ReadInt(OffsetCache.ListSizeOffset);
            var itemsPtr = behaviors.ReadPtr(OffsetCache.ListItemsOffset);
            if (itemsPtr == IntPtr.Zero)
                return AIResult.Failed("Behaviors list is empty");

            var items = new GameArray(itemsPtr);
            int boostCount = 0;

            for (int i = 0; i < count; i++)
            {
                var behavior = items[i];
                if (behavior.IsNull)
                    continue;

                var typeName = behavior.GetTypeName();
                if (typeName != null && typeName.Contains(actionType))
                {
                    // Check if target matches via SkillBehavior.GetTargetEntityForTile
                    if (!target.Untyped.IsNull && typeName.Contains("SkillBehavior"))
                    {
                        var skillBehavior = GameObj<Il2CppMenace.Tactical.AI.SkillBehavior>.Wrap(behavior.Pointer);
                        if (Offsets.SkillBehavior_TargetTile.Value.TryRead(skillBehavior, out var targetTile) && !targetTile.Untyped.IsNull)
                        {
                            var entity = targetTile.AsManaged().GetEntity();
                            if (entity == null || entity.Pointer != target.Untyped.Pointer)
                                continue;
                        }
                    }

                    // Boost the behavior score
                    var behaviorObj = GameObj<Behavior>.Wrap(behavior.Pointer);
                    int currentScore = Offsets.Behavior_Score.Value.Read(behaviorObj);
                    Offsets.Behavior_Score.Value.Write(behaviorObj, currentScore + scoreBoost);
                    boostCount++;
                }
            }

            if (boostCount == 0)
                return AIResult.Failed($"No behaviors found matching '{actionType}'");

            ModError.Info("Menace.SDK", $"Boosted {boostCount} behaviors for {actor.AsManaged().DebugName}");
            return AIResult.Ok();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.ForceNextAction", "Failed", ex);
            return AIResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Pause all AI evaluation and execution.
    /// This sets TacticalManager.m_IsAIPaused to true, halting all AI processing.
    ///
    /// THREAD SAFETY: Safe to call at any time (pauses parallel evaluation).
    /// </summary>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// When AI is paused:
    /// - No AI faction turns will progress
    /// - Behavior evaluation stops
    /// - Units remain frozen until ResumeAI is called
    ///
    /// Use this for debugging, cutscenes, or when you need to manipulate AI state safely.
    ///
    /// Example:
    ///   EntityAI.PauseAI(anyActor);
    ///   // Manipulate AI state...
    ///   EntityAI.ResumeAI(anyActor);
    /// </remarks>
    public static AIResult PauseAI()
    {
        try
        {
            var tm = TacticalManager.Get();
            if (tm == null)
                return AIResult.Failed("TacticalManager instance not found");

            tm.SetAIPaused(true);
            ModError.Info("Menace.SDK", "AI paused via SetAIPaused()");
            return AIResult.Ok();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.PauseAI", "Failed", ex);
            return AIResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Resume AI evaluation and execution after a pause.
    /// This sets TacticalManager.m_IsAIPaused to false.
    ///
    /// THREAD SAFETY: Safe to call at any time.
    /// </summary>
    /// <returns>Result indicating success or failure</returns>
    public static AIResult ResumeAI()
    {
        try
        {
            var tm = TacticalManager.Get();
            if (tm == null)
                return AIResult.Failed("TacticalManager instance not found");

            tm.SetAIPaused(false);
            ModError.Info("Menace.SDK", "AI resumed via SetAIPaused()");
            return AIResult.Ok();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.ResumeAI", "Failed", ex);
            return AIResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if AI is currently paused.
    /// Reads TacticalManager.m_IsAIPaused.
    ///
    /// THREAD SAFETY: Safe to call at any time.
    /// </summary>
    /// <returns>True if AI is paused, false otherwise</returns>
    public static bool IsAIPaused()
    {
        try
        {
            var tm = TacticalManager.Get();
            if (tm == null)
                return false;

            var tmObj = GameObj<TacticalManager>.Wrap(tm.Pointer);
            return Offsets.TacticalManager_IsAIPaused.Value.Read(tmObj);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.IsAIPaused", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Override an actor's threat perception of a target by manipulating morale.
    /// Since there's no direct threat override mechanism in the game, this uses morale
    /// as a proxy to influence AI decision-making.
    ///
    /// THREAD SAFETY: Call only during OnTurnStart/OnTurnEnd or when AI is not evaluating.
    /// </summary>
    /// <param name="actor">The actor whose threat perception to override</param>
    /// <param name="target">The target actor (currently unused - morale is global per actor)</param>
    /// <param name="threat">Threat value (higher = more threatened = lower morale)</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// This method uses morale as a proxy for threat:
    /// - High threat (75-100) -> Low morale (0-25) -> Defensive/flee behavior
    /// - Low threat (0-25) -> High morale (75-100) -> Aggressive behavior
    ///
    /// Note: Game has no per-target threat system, so this affects the actor's overall behavior.
    ///
    /// Example:
    ///   EntityAI.SetThreatValueOverride(enemy, player, 80.0f);  // Enemy becomes defensive
    /// </remarks>
    public static AIResult SetThreatValueOverride(GameObj<Actor> actor, GameObj<Actor> target, float threat)
    {
        if (actor.Untyped.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            // Convert threat (0-100) to morale (inverse relationship)
            // High threat = low morale (defensive), low threat = high morale (aggressive)
            float morale = Math.Clamp(100.0f - threat, 0.0f, 100.0f);

            Offsets.Actor_Morale.Value.Write(actor, morale);

            ModError.Info("Menace.SDK", $"Set threat override for {actor.AsManaged().DebugName}: threat={threat:F1}, morale={morale:F1}");
            return AIResult.Ok();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.SetThreatValueOverride", "Failed", ex);
            return AIResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all threat overrides for an actor by resetting morale to default steady state.
    ///
    /// THREAD SAFETY: Call only during OnTurnStart/OnTurnEnd or when AI is not evaluating.
    /// </summary>
    /// <param name="actor">The actor to clear threat overrides for</param>
    /// <returns>Result indicating success or failure</returns>
    public static AIResult ClearThreatOverrides(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            Offsets.Actor_Morale.Value.Write(actor, MORALE_STEADY);

            ModError.Info("Menace.SDK", $"Cleared threat overrides for {actor.AsManaged().DebugName}");
            return AIResult.Ok();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.ClearThreatOverrides", "Failed", ex);
            return AIResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Force an actor to make a flee decision by setting morale to panicked state.
    /// When morale reaches 0, the AI will prioritize flee/retreat behaviors.
    ///
    /// THREAD SAFETY: Call only during OnTurnStart/OnTurnEnd or when AI is not evaluating.
    /// </summary>
    /// <param name="actor">The actor to force into flee state</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// This is more reliable than direct behavior override because the morale system
    /// is the game's native mechanism for controlling flee behavior.
    ///
    /// The actor will:
    /// - Prioritize moving away from enemies
    /// - Avoid engaging in combat
    /// - Seek cover at maximum range
    ///
    /// Example:
    ///   EntityAI.ForceFleeDecision(enemy);  // Enemy will flee next turn
    /// </remarks>
    public static AIResult ForceFleeDecision(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            Offsets.Actor_Morale.Value.Write(actor, MORALE_PANICKED);

            ModError.Info("Menace.SDK", $"Forced flee decision for {actor.AsManaged().DebugName} (morale={MORALE_PANICKED})");
            return AIResult.Ok();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.ForceFleeDecision", "Failed", ex);
            return AIResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Prevent an actor from fleeing by setting morale to fearless state.
    /// High morale (100.0f) prevents the AI from entering flee/panic behaviors.
    ///
    /// THREAD SAFETY: Call only during OnTurnStart/OnTurnEnd or when AI is not evaluating.
    /// </summary>
    /// <param name="actor">The actor to prevent from fleeing</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// The actor will:
    /// - Never enter panic/flee state
    /// - Maintain aggressive posture even under heavy fire
    /// - Prioritize attack behaviors over retreat
    ///
    /// Example:
    ///   EntityAI.BlockFleeDecision(boss);  // Boss never flees
    /// </remarks>
    public static AIResult BlockFleeDecision(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            Offsets.Actor_Morale.Value.Write(actor, MORALE_FEARLESS);

            ModError.Info("Menace.SDK", $"Blocked flee decision for {actor.AsManaged().DebugName} (morale={MORALE_FEARLESS})");
            return AIResult.Ok();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.BlockFleeDecision", "Failed", ex);
            return AIResult.Failed($"Exception: {ex.Message}");
        }
    }

    // --- Helper methods ---

    /// <summary>
    /// Get the AI Agent for an actor using the verified offset from Ghidra.
    /// </summary>
    private static GameObj<Agent> GetAgent(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return default;

        try
        {
            if (!Offsets.Actor_Agent.Value.TryRead(actor, out var agent))
                return default;

            return agent;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.GetAgent", "Failed", ex);
            return default;
        }
    }
}
