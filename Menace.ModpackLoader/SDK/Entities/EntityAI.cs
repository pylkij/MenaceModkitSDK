using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// SDK extension for AI control and manipulation including behavior forcing,
/// AI pause/resume, and morale-based threat/flee control.
///
/// Based on reverse engineering findings from agent a6148de:
/// - TacticalManager.m_IsAIPaused at 0xB9 (byte/bool)
/// - Agent.m_Actor at 0x18, m_ActiveBehavior at 0x28, m_State at 0x3C
/// - Agent.m_Behaviors list at 0x20 (each behavior has Score at +0x18)
/// - Actor.m_Morale at 0x160 (float) - controls flee/aggressive states
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
    // Cached types
    private static readonly GameType _actorType = GameType.Of<Il2CppMenace.Tactical.Actor>();
    private static readonly GameType _agentType = GameType.Of<Il2CppMenace.Tactical.AI.Agent>();
    private static readonly GameType _tacticalManagerType = GameType.Of<Il2CppMenace.Tactical.TacticalManager>();
    private static readonly GameType _behaviorType = GameType.Of<Il2CppMenace.Tactical.AI.Behavior>();

    // TacticalManager offsets
    private const uint OFFSET_TACTICAL_MANAGER_IS_AI_PAUSED = 0xB9;

    // Actor offsets
    private const uint OFFSET_ACTOR_MORALE = 0x160;
    private const uint OFFSET_ACTOR_AGENT = 0x18;

    // Agent offsets
    private const uint OFFSET_AGENT_BEHAVIORS = 0x20;
    private const uint OFFSET_AGENT_ACTIVE_BEHAVIOR = 0x28;
    private const uint OFFSET_AGENT_STATE = 0x3C;

    // Behavior offsets
    private const uint OFFSET_BEHAVIOR_SCORE = 0x18;

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
    public static AIResult ForceNextAction(GameObj actor, string actionType, GameObj target = default, int scoreBoost = 10000)
    {
        if (actor.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate AI during evaluation (thread safety)");

        try
        {
            var agent = GetAgent(actor);
            if (agent.IsNull)
                return AIResult.Failed("Actor has no AI agent");

            // Get behaviors list
            var behaviorsPtr = agent.ReadPtr(OFFSET_AGENT_BEHAVIORS);
            if (behaviorsPtr == IntPtr.Zero)
                return AIResult.Failed("Agent has no behaviors list");

            var behaviors = new GameObj(behaviorsPtr);

            // Iterate behaviors and boost matching ones
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
                    // Check if target matches (for targeted behaviors)
                    if (!target.IsNull)
                    {
                        var behaviorKlass = IL2CPP.il2cpp_object_get_class(behavior.Pointer);
                        var behaviorTarget = behavior.ReadObj(OffsetCache.GetOrResolve(behaviorKlass, "TargetEntity"));
                        if (!behaviorTarget.IsNull && behaviorTarget.Pointer != target.Pointer)
                            continue; // Skip if target doesn't match
                    }

                    // Boost the behavior score
                    var currentScore = behavior.ReadInt(OFFSET_BEHAVIOR_SCORE);
                    var newScore = currentScore + scoreBoost;
                    Marshal.WriteInt32(behavior.Pointer + (int)OFFSET_BEHAVIOR_SCORE, newScore);
                    boostCount++;
                }
            }

            if (boostCount == 0)
                return AIResult.Failed($"No behaviors found matching '{actionType}'");

            ModError.Info("Menace.SDK", $"Boosted {boostCount} behaviors for {actor.GetName()}");
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
    /// <param name="actor">Any actor (used to get TacticalManager instance)</param>
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
    public static AIResult PauseAI(GameObj actor)
    {
        try
        {
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null)
                return AIResult.Failed("TacticalManager type not found");

            var tm = GetTacticalManagerProxy();
            if (tm == null)
                return AIResult.Failed("TacticalManager instance not found");

            // Try calling SetAIPaused(true) method first
            var setPausedMethod = tmType.GetMethod("SetAIPaused",
                BindingFlags.Public | BindingFlags.Instance);
            if (setPausedMethod != null)
            {
                setPausedMethod.Invoke(tm, new object[] { true });
                ModError.Info("Menace.SDK", "AI paused via SetAIPaused()");
                return AIResult.Ok();
            }

            // Fallback: write directly to m_IsAIPaused byte field
            var tmObj = new GameObj(((Il2CppObjectBase)tm).Pointer);
            Marshal.WriteByte(tmObj.Pointer + (int)OFFSET_TACTICAL_MANAGER_IS_AI_PAUSED, 1);
            ModError.Info("Menace.SDK", "AI paused via direct memory write");
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
    /// <param name="actor">Any actor (used to get TacticalManager instance)</param>
    /// <returns>Result indicating success or failure</returns>
    public static AIResult ResumeAI(GameObj actor)
    {
        try
        {
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null)
                return AIResult.Failed("TacticalManager type not found");

            var tm = GetTacticalManagerProxy();
            if (tm == null)
                return AIResult.Failed("TacticalManager instance not found");

            // Try calling SetAIPaused(false) method first
            var setPausedMethod = tmType.GetMethod("SetAIPaused",
                BindingFlags.Public | BindingFlags.Instance);
            if (setPausedMethod != null)
            {
                setPausedMethod.Invoke(tm, new object[] { false });
                ModError.Info("Menace.SDK", "AI resumed via SetAIPaused()");
                return AIResult.Ok();
            }

            // Fallback: write directly to m_IsAIPaused byte field
            var tmObj = new GameObj(((Il2CppObjectBase)tm).Pointer);
            Marshal.WriteByte(tmObj.Pointer + (int)OFFSET_TACTICAL_MANAGER_IS_AI_PAUSED, 0);
            ModError.Info("Menace.SDK", "AI resumed via direct memory write");
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
    /// <param name="actor">Any actor (used to get TacticalManager instance)</param>
    /// <returns>True if AI is paused, false otherwise</returns>
    public static bool IsAIPaused(GameObj actor)
    {
        try
        {
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null)
                return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null)
                return false;

            // Try calling IsAIPaused() method first
            var isPausedMethod = tmType.GetMethod("IsAIPaused",
                BindingFlags.Public | BindingFlags.Instance);
            if (isPausedMethod != null)
            {
                return (bool)isPausedMethod.Invoke(tm, null);
            }

            // Fallback: read m_IsAIPaused byte field directly
            var tmObj = new GameObj(((Il2CppObjectBase)tm).Pointer);
            return Marshal.ReadByte(tmObj.Pointer + (int)OFFSET_TACTICAL_MANAGER_IS_AI_PAUSED) != 0;
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
    public static AIResult SetThreatValueOverride(GameObj actor, GameObj target, float threat)
    {
        if (actor.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            // Convert threat (0-100) to morale (inverse relationship)
            // High threat = low morale (defensive), low threat = high morale (aggressive)
            float morale = Math.Clamp(100.0f - threat, 0.0f, 100.0f);

            // Write morale directly to actor
            var moraleInt = BitConverter.SingleToInt32Bits(morale);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_MORALE, moraleInt);

            ModError.Info("Menace.SDK", $"Set threat override for {actor.GetName()}: threat={threat:F1}, morale={morale:F1}");
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
    public static AIResult ClearThreatOverrides(GameObj actor)
    {
        if (actor.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            // Reset to steady state morale
            var moraleInt = BitConverter.SingleToInt32Bits(MORALE_STEADY);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_MORALE, moraleInt);

            ModError.Info("Menace.SDK", $"Cleared threat overrides for {actor.GetName()}");
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
    public static AIResult ForceFleeDecision(GameObj actor)
    {
        if (actor.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            // Set morale to panicked (0.0f) to trigger flee state
            var moraleInt = BitConverter.SingleToInt32Bits(MORALE_PANICKED);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_MORALE, moraleInt);

            ModError.Info("Menace.SDK", $"Forced flee decision for {actor.GetName()} (morale={MORALE_PANICKED})");
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
    public static AIResult BlockFleeDecision(GameObj actor)
    {
        if (actor.IsNull)
            return AIResult.Failed("Invalid actor");

        if (AI.IsAnyFactionThinking())
            return AIResult.Failed("Cannot manipulate morale during AI evaluation (thread safety)");

        try
        {
            // Set morale to fearless (100.0f) to block flee state
            var moraleInt = BitConverter.SingleToInt32Bits(MORALE_FEARLESS);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_MORALE, moraleInt);

            ModError.Info("Menace.SDK", $"Blocked flee decision for {actor.GetName()} (morale={MORALE_FEARLESS})");
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
    private static GameObj GetAgent(GameObj actor)
    {
        if (actor.IsNull)
            return GameObj.Null;

        try
        {
            // Agent is at Actor+0x18 (m_Agent field)
            var agentPtr = actor.ReadPtr(OFFSET_ACTOR_AGENT);
            return new GameObj(agentPtr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.GetAgent", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the TacticalManager singleton instance.
    /// </summary>
    private static object GetTacticalManagerProxy()
    {
        try
        {
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return null;

            // Try s_Singleton static property
            var singletonProp = tmType.GetProperty("s_Singleton",
                BindingFlags.Public | BindingFlags.Static);
            if (singletonProp != null)
                return singletonProp.GetValue(null);

            // Try Instance static property
            var instanceProp = tmType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
                return instanceProp.GetValue(null);

            // Try Get() static method
            var getMethod = tmType.GetMethod("Get",
                BindingFlags.Public | BindingFlags.Static);
            return getMethod?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityAI.GetTacticalManagerProxy", "Failed", ex);
            return null;
        }
    }
}
