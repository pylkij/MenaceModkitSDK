using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Strategy;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for the Emotional State system.
/// Provides safe access to temporary morale and psychological effects on unit leaders.
/// Emotions are triggered by in-game events (kills, injuries, ally deaths) and apply
/// skill modifiers that affect combat performance.
///
/// Based on reverse engineering findings (docs/reverse-engineering/emotional-system.md):
/// - EmotionalStates collection per BaseUnitLeader @ +0x58
/// - EmotionalStates.Owner @ +0x10
/// - EmotionalStates.States @ +0x18
/// - EmotionalState.Template @ +0x10
/// - EmotionalState.Trigger @ +0x18
/// - EmotionalState.TargetLeader @ +0x20
/// - EmotionalState.RemainingDuration @ +0x28
/// - EmotionalState.IsFirstMission @ +0x2C
/// </summary>
public static class Emotions
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // EmotionalStates fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.EmotionalStates, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.EmotionalState>> _hStates;

    // EmotionalState fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.EmotionalState, Il2CppMenace.Strategy.EmotionalStateTemplate> _hTemplate;
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalState, Il2CppMenace.Strategy.EmotionalTrigger> _hTrigger;
    private static ObjFieldHandle<Il2CppMenace.Strategy.EmotionalState, Il2CppMenace.Strategy.UnitLeaderTemplate> _hTarget;
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalState, int> _hRemainingDuration;
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalState, bool> _hIsNew;

    // EmotionalStateTemplate fields
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalStateTemplate, Il2CppMenace.Strategy.EmotionalStateType> _hStateType;
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalStateTemplate, Il2CppMenace.Strategy.EmotionalStateCategory> _hCategory;
    private static ObjFieldHandle<Il2CppMenace.Strategy.EmotionalStateTemplate, Il2CppMenace.Tactical.Skills.SkillTemplate> _hEffect;
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalStateTemplate, UnityEngine.Vector2Int> _hDurationInMissions;
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalStateTemplate, bool> _hIsPositive;
    private static FieldHandle<Il2CppMenace.Strategy.EmotionalStateTemplate, bool> _hIsSuperState;
    private static ObjFieldHandle<Il2CppMenace.Strategy.EmotionalStateTemplate, Il2CppMenace.Strategy.EmotionalStateTemplate> _hSuperState;

    // BaseUnitLeader fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, Il2CppMenace.Strategy.EmotionalStates> _hLeaderEmotionalStates;
    private static ObjFieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, Il2CppMenace.Strategy.UnitLeaderTemplate> _hLeaderTemplate;

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
            _hStates = GameObj<Il2CppMenace.Strategy.EmotionalStates>.ResolveObjField(x => x.m_States);

            _hTemplate = GameObj<Il2CppMenace.Strategy.EmotionalState>.ResolveObjField(x => x.m_Template);
            _hTrigger = GameObj<Il2CppMenace.Strategy.EmotionalState>.ResolveField(x => x.m_Trigger);
            _hTarget = GameObj<Il2CppMenace.Strategy.EmotionalState>.ResolveObjField(x => x.m_Target);
            _hRemainingDuration = GameObj<Il2CppMenace.Strategy.EmotionalState>.ResolveField(x => x.m_RemainingDuration);
            _hIsNew = GameObj<Il2CppMenace.Strategy.EmotionalState>.ResolveField(x => x.m_IsNew);

            _hStateType = GameObj<Il2CppMenace.Strategy.EmotionalStateTemplate>.ResolveField(x => x.StateType);
            _hCategory = GameObj<Il2CppMenace.Strategy.EmotionalStateTemplate>.ResolveField(x => x.Category);
            _hEffect = GameObj<Il2CppMenace.Strategy.EmotionalStateTemplate>.ResolveObjField(x => x.Effect);
            _hDurationInMissions = GameObj<Il2CppMenace.Strategy.EmotionalStateTemplate>.ResolveField(x => x.DurationInMissions);
            _hIsPositive = GameObj<Il2CppMenace.Strategy.EmotionalStateTemplate>.ResolveField(x => x.IsPositive);
            _hIsSuperState = GameObj<Il2CppMenace.Strategy.EmotionalStateTemplate>.ResolveField(x => x.IsSuperState);
            _hSuperState = GameObj<Il2CppMenace.Strategy.EmotionalStateTemplate>.ResolveObjField(x => x.SuperState);

            _hLeaderEmotionalStates = GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.ResolveObjField(x => x.m_EmotionalStates);
            _hLeaderTemplate = GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.ResolveObjField(x => x.LeaderTemplate);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.ResolveHandles: Field handle resolution failed", ex);
        }
    }

    /// <summary>
    /// Mirrors <see cref="Il2CppMenace.Strategy.EmotionalStateType"/>.
    /// This is a [Flags] bitmask — values may be combined.
    /// </summary>
    public enum EmotionalStateType
    {
        /// <summary>No emotional state.</summary>
        None = 0,

        /// <summary>Animosity towards a specific target.</summary>
        AnimosityTowards = 1,

        /// <summary>Determined - focused and resolute.</summary>
        Determined = 2,

        /// <summary>Weary - tired from extended duty.</summary>
        Weary = 4,

        /// <summary>Disheartened - morale reduced.</summary>
        Disheartened = 8,

        /// <summary>Eager - enthusiastic and ready for action.</summary>
        Eager = 16,

        /// <summary>Frustrated - annoyed and less effective.</summary>
        Frustrated = 32,

        /// <summary>Exhausted - severely fatigued.</summary>
        Exhausted = 64,

        /// <summary>GoodwillTowards a specific target.</summary>
        GoodwillTowards = 128,

        /// <summary>Hesitant - uncertain and cautious.</summary>
        Hesitant = 256,

        /// <summary>Overconfident - too bold, may make mistakes.</summary>
        Overconfident = 512,

        /// <summary>Injured - physically wounded.</summary>
        Injured = 1024,

        /// <summary>Bruised - minor physical damage.</summary>
        Bruised = 2048,

        /// <summary>Euphoric - extremely positive mood.</summary>
        Euphoric = 4096,

        /// <summary>Miserable - extremely negative mood.</summary>
        Miserable = 8192
    }

    /// <summary>
    /// Mirrors <see cref="Il2CppMenace.Strategy.EmotionalTrigger"/>.
    /// Triggers that can cause emotional states to be applied.
    /// </summary>
    public enum EmotionalTrigger
    {
        /// <summary>Stabilized by another unit.</summary>
        StabilizedBy = 0,

        /// <summary>Stabilized other units.</summary>
        StabilizedOthers = 1,

        /// <summary>Received friendly fire from another unit.</summary>
        ReceivedFriendlyFireFrom = 2,

        /// <summary>Deployed X times with another unit.</summary>
        DeployedXTimesWithOther = 3,

        /// <summary>Killed X enemy entities.</summary>
        KilledXEnemyEntities = 4,

        /// <summary>Killed X enemy mini-bosses.</summary>
        KilledXEnemyMiniBosses = 5,

        /// <summary>Deployed in the X missions before current.</summary>
        DeployedInTheXMissionsBeforeCurrent = 6,

        /// <summary>Not deployed in the X missions before current.</summary>
        NotDeployedInTheXMissionsBeforeCurrent = 7,

        /// <summary>Killed X civilian elements.</summary>
        KilledXCivElements = 8,

        /// <summary>Success on favorite planet.</summary>
        SuccessOnFavPlanet = 9,

        /// <summary>Failed on favorite planet.</summary>
        FailedOnFavPlanet = 10,

        /// <summary>Lost over X percent hitpoints.</summary>
        LostOverXPercentHitpoints = 11,

        /// <summary>Game effect trigger.</summary>
        GameEffect = 12,

        /// <summary>Event trigger.</summary>
        Event = 14,

        /// <summary>Cheat trigger.</summary>
        Cheat = 16,

        /// <summary>Other leader killed civilian element on favorite planet.</summary>
        OtherLeaderKilledCivElementOnFavPlanet = 18,

        /// <summary>Unit fled from combat.</summary>
        Fled = 19,

        /// <summary>Near death experience.</summary>
        NearDeathExperience = 20,

        /// <summary>Lost all squaddies.</summary>
        LostAllSquaddies = 21
    }

    /// <summary>
    /// Information about a single active emotional state.
    /// </summary>
    public class EmotionalStateInfo
    {
        /// <summary>The type of emotion.</summary>
        public EmotionalStateType Type { get; set; }

        /// <summary>What triggered this emotion.</summary>
        public EmotionalTrigger Trigger { get; set; }

        /// <summary>The category of this emotion (Normal, Injuries, Exhaustion, Relationship).</summary>
        public EmotionalStateCategory Category { get; set; }

        /// <summary>Target leader for targeted emotions (may be null).</summary>
        public string TargetLeaderName { get; set; }

        /// <summary>Missions remaining until this emotion expires.</summary>
        public int RemainingDuration { get; set; }

        /// <summary>True if this emotion was just applied this mission.</summary>
        public bool IsNew { get; set; }

        /// <summary>True if this is a positive emotion.</summary>
        public bool IsPositive { get; set; }

        /// <summary>True if this emotion is a super state.</summary>
        public bool IsSuperState { get; set; }

        /// <summary>Name of the skill modifier applied by this emotion.</summary>
        public string SkillName { get; set; }

        /// <summary>Pointer to the EmotionalState object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Information about a unit leader's emotional states collection.
    /// </summary>
    public class EmotionalStatesInfo
    {
        /// <summary>Name of the owning unit leader.</summary>
        public string OwnerName { get; set; }

        /// <summary>Pointer to the owner BaseUnitLeader.</summary>
        public IntPtr OwnerPointer { get; set; }

        /// <summary>Pointer to the EmotionalStates collection.</summary>
        public IntPtr Pointer { get; set; }

        /// <summary>List of all active emotional states.</summary>
        public List<EmotionalStateInfo> ActiveStates { get; set; } = new();

        /// <summary>Total count of active emotions.</summary>
        public int StateCount => ActiveStates.Count;

        /// <summary>Count of positive emotions.</summary>
        public int PositiveCount => ActiveStates.Count(s => s.IsPositive);

        /// <summary>Count of negative (non-positive) emotions.</summary>
        public int NegativeCount => ActiveStates.Count(s => !s.IsPositive);
    }

    /// <summary>
    /// Result from emotional state operations.
    /// </summary>
    public class EmotionResult
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error message if operation failed.</summary>
        public string Error { get; set; }

        /// <summary>The emotional state type involved.</summary>
        public EmotionalStateType StateType { get; set; }

        /// <summary>Action taken (Added, Extended, Replaced, Reduced, Removed).</summary>
        public string Action { get; set; }

        /// <summary>Create a failed result.</summary>
        public static EmotionResult Failed(string error) =>
            new() { Success = false, Error = error };

        /// <summary>Create a successful result.</summary>
        public static EmotionResult Ok(EmotionalStateType type, string action) =>
            new() { Success = true, StateType = type, Action = action };
    }

    /// <summary>
    /// Get the EmotionalStates collection for a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>GameObj representing the EmotionalStates collection, or Null if not found.</returns>
    public static GameObj GetEmotionalStates(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
    {
        try
        {
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return GameObj.Null;

            if (!_hLeaderEmotionalStates.TryRead(leader, out var emotions))
                return GameObj.Null;

            if (emotions.Untyped.CheckAlive() != AliveStatus.Alive)
                return GameObj.Null;

            return emotions.Untyped;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.GetEmotionalStates: Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get detailed information about all emotional states for a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>EmotionalStatesInfo with all active emotions, or null if not available.</returns>
    public static EmotionalStatesInfo GetEmotionalStatesInfo(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
    {
        try
        {
            var emotions = GetEmotionalStates(leader);
            if (emotions.CheckAlive() != AliveStatus.Alive)
                return null;

            if (!GameObj<Il2CppMenace.Strategy.EmotionalStates>.TryWrap(emotions, out var emotionsTyped))
                return null;

            var info = new EmotionalStatesInfo
            {
                Pointer = emotions.Pointer,
                OwnerPointer = leader.Untyped.Pointer,
                OwnerName = leader.Untyped.GetName() ?? "Unknown"
            };

            if (!_hStates.TryRead(emotionsTyped, out var statesListObj))
                return info;

            if (statesListObj.Untyped.CheckAlive() != AliveStatus.Alive)
                return info;

            var statesList = statesListObj.AsManaged();
            if (statesList == null)
                return info;

            for (int i = 0; i < statesList.Count; i++)
            {
                var stateManaged = statesList[i];
                if (stateManaged == null) continue;

                var stateObj = GameObj<Il2CppMenace.Strategy.EmotionalState>.Wrap(stateManaged.Pointer);

                var stateInfo = new EmotionalStateInfo
                {
                    Pointer = stateManaged.Pointer
                };

                if (_hTrigger.TryRead(stateObj, out var trigger))
                    stateInfo.Trigger = (EmotionalTrigger)(int)trigger;

                if (_hRemainingDuration.TryRead(stateObj, out var duration))
                    stateInfo.RemainingDuration = duration;

                if (_hIsNew.TryRead(stateObj, out var isNew))
                    stateInfo.IsNew = isNew;

                if (_hTarget.TryRead(stateObj, out var targetObj) &&
                    targetObj.Untyped.CheckAlive() == AliveStatus.Alive)
                    stateInfo.TargetLeaderName = targetObj.Untyped.GetName();

                if (_hTemplate.TryRead(stateObj, out var templateObj) &&
                    templateObj.Untyped.CheckAlive() == AliveStatus.Alive)
                {
                    if (_hStateType.TryRead(templateObj, out var stateType))
                        stateInfo.Type = (EmotionalStateType)(int)stateType;

                    if (_hCategory.TryRead(templateObj, out var category))
                        stateInfo.Category = (EmotionalStateCategory)(int)category;

                    if (_hIsPositive.TryRead(templateObj, out var isPositive))
                        stateInfo.IsPositive = isPositive;

                    if (_hIsSuperState.TryRead(templateObj, out var isSuperState))
                        stateInfo.IsSuperState = isSuperState;

                    if (_hEffect.TryRead(templateObj, out var effectObj) &&
                        effectObj.Untyped.CheckAlive() == AliveStatus.Alive)
                        stateInfo.SkillName = effectObj.Untyped.GetName();
                }

                info.ActiveStates.Add(stateInfo);
            }

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.GetEmotionalStatesInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if a unit leader has a specific emotional state type.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to check for.</param>
    /// <returns>True if the leader has an active emotion of that type.</returns>
    public static bool HasEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
    {
        try
        {
            var emotions = GetEmotionalStates(leader);
            if (emotions.CheckAlive() != AliveStatus.Alive)
                return false;

            var emotionsManaged = emotions.As<Il2CppMenace.Strategy.EmotionalStates>();
            if (emotionsManaged == null)
                return false;

            return emotionsManaged.HasState((Il2CppMenace.Strategy.EmotionalStateType)(int)type);
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.HasEmotion: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a unit leader has any of the specified emotional state types.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="types">Array of emotional state types to check for.</param>
    /// <returns>True if the leader has any of the specified emotion types.</returns>
    public static bool HasAnyEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, params EmotionalStateType[] types)
    {
        foreach (var type in types)
        {
            if (HasEmotion(leader, type))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the set of all active emotional state types for a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>HashSet of active EmotionalStateType values.</returns>
    public static EmotionalStateType GetStateSet(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
    {
        try
        {
            var emotions = GetEmotionalStates(leader);
            if (emotions.CheckAlive() != AliveStatus.Alive)
                return EmotionalStateType.None;

            var emotionsManaged = emotions.As<Il2CppMenace.Strategy.EmotionalStates>();
            if (emotionsManaged == null)
                return EmotionalStateType.None;

            return (EmotionalStateType)(int)emotionsManaged.GetStateSet();
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.GetStateSet: Failed", ex);
            return EmotionalStateType.None;
        }
    }

    /// <summary>
    /// Get information about a specific active emotion type on a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to get info for.</param>
    /// <returns>EmotionalStateInfo if found, null otherwise.</returns>
    public static EmotionalStateInfo GetEmotionInfo(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
    {
        var info = GetEmotionalStatesInfo(leader);
        return info?.ActiveStates.Find(s => s.Type == type);
    }

    /// <summary>
    /// Trigger an emotion on a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="trigger">The trigger event causing the emotion.</param>
    /// <param name="target">Optional target leader for targeted emotions.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult TriggerEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalTrigger trigger, GameObj<Il2CppMenace.Strategy.BaseUnitLeader> target = default)
    {
        try
        {
            var emotions = GetEmotionalStates(leader);
            if (emotions.CheckAlive() != AliveStatus.Alive)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            var emotionsManaged = emotions.As<Il2CppMenace.Strategy.EmotionalStates>();
            if (emotionsManaged == null)
                return EmotionResult.Failed("Failed to create EmotionalStates proxy");

            Il2CppMenace.Strategy.UnitLeaderTemplate targetTemplate = null;
            if (target.Untyped.CheckAlive() == AliveStatus.Alive)
            {
                if (_hLeaderTemplate.TryRead(target, out var leaderTemplate))
                    targetTemplate = leaderTemplate.AsManaged();
            }

            var random = new Il2CppMenace.Tools.PseudoRandom(Environment.TickCount);
            var mission = Mission.GetMission();

            emotionsManaged.TriggerEmotion(
                (Il2CppMenace.Strategy.EmotionalTrigger)(int)trigger,
                targetTemplate,
                random,
                mission);

            SdkLogger.Msg($"Menace.SDK] Triggered emotion: {trigger} on {leader.Untyped.GetName()}");
            return EmotionResult.Ok(EmotionalStateType.None, "Triggered");
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.TriggerEmotion: Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply a specific emotional state template to a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="templateName">Name of the EmotionalStateTemplate to apply.</param>
    /// <param name="trigger">The trigger causing this emotion.</param>
    /// <param name="target">Optional target leader for targeted emotions.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult ApplyEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, string templateId,
    EmotionalTrigger trigger = EmotionalTrigger.Cheat, GameObj<Il2CppMenace.Strategy.BaseUnitLeader> target = default)
    {
        if (string.IsNullOrEmpty(templateId))
            return EmotionResult.Failed("Template ID required");

        try
        {
            if (!Templates.TryGet<Il2CppMenace.Strategy.EmotionalStateTemplate>(templateId, out var template))
                return EmotionResult.Failed($"Template '{templateId}' not found");

            var emotions = GetEmotionalStates(leader);
            if (emotions.CheckAlive() != AliveStatus.Alive)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            var emotionsManaged = emotions.As<Il2CppMenace.Strategy.EmotionalStates>();
            if (emotionsManaged == null)
                return EmotionResult.Failed("Failed to create EmotionalStates proxy");

            Il2CppMenace.Strategy.UnitLeaderTemplate targetTemplate = null;
            if (target.Untyped.CheckAlive() == AliveStatus.Alive)
            {
                if (_hLeaderTemplate.TryRead(target, out var leaderTemplate))
                    targetTemplate = leaderTemplate.AsManaged();
            }

            var random = new Il2CppMenace.Tools.PseudoRandom(Environment.TickCount);

            var result = emotionsManaged.TryApplyEmotionalState(
                template.StateType,
                (Il2CppMenace.Strategy.EmotionalTrigger)(int)trigger,
                targetTemplate,
                random,
                false);

            if (result)
            {
                SdkLogger.Msg($"[Menace.SDK] Applied emotion '{templateId}' to {leader.Untyped.GetName()}");
                return EmotionResult.Ok((EmotionalStateType)(int)template.StateType, "Applied");
            }
            else
            {
                return EmotionResult.Failed("TryApplyEmotionalState returned false");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.ApplyEmotion: Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove a specific emotional state type from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to remove.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult RemoveEmotion(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
    {
        try
        {
            var emotions = GetEmotionalStates(leader);
            if (emotions.CheckAlive() != AliveStatus.Alive)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            var emotionsManaged = emotions.As<Il2CppMenace.Strategy.EmotionalStates>();
            if (emotionsManaged == null)
                return EmotionResult.Failed("Failed to create EmotionalStates proxy");

            var idx = emotionsManaged.GetStateIdx((Il2CppMenace.Strategy.EmotionalStateType)(int)type);
            if (idx < 0)
                return EmotionResult.Failed($"No active emotion of type {type}");

            var removeMethod = typeof(Il2CppMenace.Strategy.EmotionalStates).GetMethod("RemoveState",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (removeMethod == null)
                return EmotionResult.Failed("RemoveState method not found");

            removeMethod.Invoke(emotionsManaged, new object[] { idx });

            SdkLogger.Msg($"[Menace.SDK] Removed emotion {type} from {leader.Untyped.GetName()}");
            return EmotionResult.Ok(type, "Removed");
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.RemoveEmotion: Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove all emotional states from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>Number of emotions removed.</returns>
    public static int ClearEmotions(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
    {
        try
        {
            var info = GetEmotionalStatesInfo(leader);
            if (info == null || info.StateCount == 0)
                return 0;

            int removed = 0;
            for (int i = info.ActiveStates.Count - 1; i >= 0; i--)
            {
                var result = RemoveEmotion(leader, info.ActiveStates[i].Type);
                if (result.Success)
                    removed++;
            }

            return removed;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.ClearEmotions: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Clear all negative emotions from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>Number of negative emotions removed.</returns>
    public static int ClearNegativeEmotions(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
    {
        try
        {
            var info = GetEmotionalStatesInfo(leader);
            if (info == null || info.StateCount == 0)
                return 0;

            int removed = 0;
            for (int i = info.ActiveStates.Count - 1; i >= 0; i--)
            {
                if (!info.ActiveStates[i].IsPositive)
                {
                    var result = RemoveEmotion(leader, info.ActiveStates[i].Type);
                    if (result.Success)
                        removed++;
                }
            }

            return removed;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.ClearNegativeEmotions: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Clear all positive emotions from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>Number of positive emotions removed.</returns>
    public static int ClearPositiveEmotions(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader)
    {
        try
        {
            var info = GetEmotionalStatesInfo(leader);
            if (info == null || info.StateCount == 0)
                return 0;

            int removed = 0;
            for (int i = info.ActiveStates.Count - 1; i >= 0; i--)
            {
                if (info.ActiveStates[i].IsPositive)
                {
                    var result = RemoveEmotion(leader, info.ActiveStates[i].Type);
                    if (result.Success)
                        removed++;
                }
            }

            return removed;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.ClearPositiveEmotions: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Extend the duration of an active emotion.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to extend.</param>
    /// <param name="missions">Number of missions to add to duration.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult ExtendDuration(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type, int missions = 1)
    {
        try
        {
            var emotions = GetEmotionalStates(leader);
            if (emotions.CheckAlive() != AliveStatus.Alive)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            if (!GameObj<Il2CppMenace.Strategy.EmotionalStates>.TryWrap(emotions, out var emotionsTyped))
                return EmotionResult.Failed("Failed to wrap EmotionalStates");

            if (!_hStates.TryRead(emotionsTyped, out var statesListObj))
                return EmotionResult.Failed("Failed to read states list");

            if (statesListObj.Untyped.CheckAlive() != AliveStatus.Alive)
                return EmotionResult.Failed("States list is not alive");

            var statesList = statesListObj.AsManaged();
            if (statesList == null)
                return EmotionResult.Failed("Failed to get states list proxy");

            for (int i = 0; i < statesList.Count; i++)
            {
                var stateManaged = statesList[i];
                if (stateManaged == null) continue;

                var stateObj = GameObj<Il2CppMenace.Strategy.EmotionalState>.Wrap(stateManaged.Pointer);

                if (!_hTemplate.TryRead(stateObj, out var templateObj)) continue;
                if (!_hStateType.TryRead(templateObj, out var stateType)) continue;

                if ((EmotionalStateType)(int)stateType == type)
                {
                    stateManaged.ExtendDuration(missions);
                    SdkLogger.Msg($"[Menace.SDK] Extended {type} duration by {missions} on {leader.Untyped.GetName()}");
                    return EmotionResult.Ok(type, "Extended");
                }
            }

            return EmotionResult.Failed($"No active emotion of type {type}");
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Emotions.ExtendDuration: Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the remaining duration of an active emotion.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to check.</param>
    /// <returns>Remaining missions, or -1 if emotion not found.</returns>
    public static int GetRemainingDuration(GameObj<Il2CppMenace.Strategy.BaseUnitLeader> leader, EmotionalStateType type)
    {
        var info = GetEmotionInfo(leader, type);
        return info?.RemainingDuration ?? -1;
    }

    /// <summary>
    /// Register console commands for emotional state debugging.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("emotions", "<nickname>", "Show emotional states for a unit", args =>
        {
            if (args.Length == 0)
                return "Usage: emotions <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Unit '{nickname}' not found";

            var info = GetEmotionalStatesInfo(leader);
            if (info == null)
                return "Could not get emotional states";

            if (info.StateCount == 0)
                return $"{info.OwnerName} has no active emotions";

            var lines = new List<string>
        {
            $"Emotional States for {info.OwnerName} ({info.StateCount} active):",
            $"  Positive: {info.PositiveCount}, Negative: {info.NegativeCount}"
        };

            foreach (var state in info.ActiveStates)
            {
                var polarity = state.IsPositive ? "+" : "-";
                var target = !string.IsNullOrEmpty(state.TargetLeaderName)
                    ? $" -> {state.TargetLeaderName}"
                    : "";
                var isNew = state.IsNew ? " [NEW]" : "";
                lines.Add($"  [{polarity}] {state.Type}: {state.RemainingDuration} missions{target}{isNew}");
            }

            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("triggeremotion", "<nickname> <trigger>",
        "Trigger an emotion (KilledXEnemyEntities, GameEffect, Cheat, etc.)", args =>
        {
            if (args.Length < 2)
                return "Usage: triggeremotion <nickname> <trigger>";

            var nickname = args[0];
            var triggerName = args[1];

            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalTrigger>(triggerName, true, out var trigger))
                return $"Unknown trigger '{triggerName}'. Valid: StabilizedBy, StabilizedOthers, KilledXEnemyEntities, GameEffect, Event, Cheat, etc.";

            var result = TriggerEmotion(leader, trigger);
            return result.Success
                ? $"Triggered {trigger} on {leader.AsManaged().GetNickname()}"
                : $"Failed: {result.Error}";
        });

        DevConsole.RegisterCommand("applyemotion", "<nickname> <templateId>",
        "Apply an emotion template to a unit by template ID", args =>
        {
            if (args.Length < 2)
                return "Usage: applyemotion <nickname> <templateId>";

            var nickname = args[0];
            var templateId = args[1];

            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Unit '{nickname}' not found";

            var result = ApplyEmotion(leader, templateId);
            return result.Success
                ? $"Applied '{templateId}' to {leader.AsManaged().GetNickname()}: {result.Action}"
                : $"Failed: {result.Error}";
        });

        DevConsole.RegisterCommand("removeemotion", "<nickname> <type>",
        "Remove an emotion type (Determined, Weary, Eager, Frustrated, etc.)", args =>
        {
            if (args.Length < 2)
                return "Usage: removeemotion <nickname> <type>";

            var nickname = args[0];
            var typeName = args[1];

            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalStateType>(typeName, true, out var type))
                return $"Unknown emotion type '{typeName}'. Valid: Determined, Weary, Eager, Frustrated, Euphoric, Miserable, etc.";

            var result = RemoveEmotion(leader, type);
            return result.Success
                ? $"Removed {type} from {leader.AsManaged().GetNickname()}"
                : $"Failed: {result.Error}";
        });

        DevConsole.RegisterCommand("clearemotions", "<nickname> [negative|positive]",
        "Clear all, negative, or positive emotions from a unit", args =>
        {
            if (args.Length == 0)
                return "Usage: clearemotions <nickname> [negative|positive]";

            var nickname = args[0];
            var filter = args.Length > 1 ? args[1].ToLowerInvariant() : "all";

            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Unit '{nickname}' not found";

            int removed = filter switch
            {
                "negative" => ClearNegativeEmotions(leader),
                "positive" => ClearPositiveEmotions(leader),
                _ => ClearEmotions(leader)
            };

            return $"Removed {removed} {filter} emotion(s) from {leader.AsManaged().GetNickname()}";
        });

        DevConsole.RegisterCommand("emotemplates", "", "List available emotion templates", args =>
        {
            var templates = Templates.FindAll<Il2CppMenace.Strategy.EmotionalStateTemplate>();
            if (templates.Count == 0)
                return "No emotion templates found";

            var lines = new List<string> { $"Emotion Templates ({templates.Count}):" };
            foreach (var t in templates)
                lines.Add($"  {t.name}");

            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("hasemotion", "<nickname> <type>",
        "Check if a unit has a specific emotion type", args =>
        {
            if (args.Length < 2)
                return "Usage: hasemotion <nickname> <type>";

            var nickname = args[0];
            var typeName = args[1];

            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalStateType>(typeName, true, out var type))
                return $"Unknown emotion type '{typeName}'";

            var has = HasEmotion(leader, type);
            if (has)
            {
                var duration = GetRemainingDuration(leader, type);
                return $"{leader.AsManaged().GetNickname()} HAS {type} ({duration} missions remaining)";
            }
            return $"{leader.AsManaged().GetNickname()} does NOT have {type}";
        });

        DevConsole.RegisterCommand("extendemotion", "<nickname> <type> [missions]",
        "Extend the duration of an active emotion", args =>
        {
            if (args.Length < 2)
                return "Usage: extendemotion <nickname> <type> [missions]";

            var nickname = args[0];
            var typeName = args[1];
            var missions = args.Length > 2 && int.TryParse(args[2], out int m) ? m : 1;

            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalStateType>(typeName, true, out var type))
                return $"Unknown emotion type '{typeName}'";

            var result = ExtendDuration(leader, type, missions);
            return result.Success
                ? $"Extended {type} by {missions} mission(s)"
                : $"Failed: {result.Error}";
        });
    }
}
