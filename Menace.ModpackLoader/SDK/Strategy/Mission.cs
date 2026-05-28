using Il2CppMenace.Strategy;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Objectives;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for mission system operations.
/// Provides safe access to mission state, objectives, and mission flow control.
///
/// Based on reverse engineering findings:
/// - Mission class @ 0x180588900
/// - Mission.Template @ +0x10
/// - Mission.Status @ +0xB8
/// - Mission.Objectives @ +0x40
/// </summary>
public static class Mission
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    private static ObjFieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.Missions.MissionTemplate> _hTemplate;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.MissionDifficultyTemplate> _hDifficulty;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Mission, Il2CppTactical.Weather.WeatherTemplate> _hWeather;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.BiomeTemplate> _hBiome;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.FactionTemplate> _hClientFaction;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.OperationAssetTemplate> _hAssetReward;

    private static FieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.MissionStatus> _hStatus;
    private static FieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.MissionLayer> _hLayer;
    private static FieldHandle<Il2CppMenace.Strategy.Mission, int> _hSeed;
    private static FieldHandle<Il2CppMenace.Strategy.Mission, int> _hLayerIdx;
    private static FieldHandle<Il2CppMenace.Strategy.Mission, int> _hIdx;
    private static FieldHandle<Il2CppMenace.Strategy.Mission, float> _hTacticalPlaytimeInSec;
    private static FieldHandle<Il2CppMenace.Strategy.Mission, bool> _hReinforcementsEnabled;
    private static FieldHandle<Il2CppMenace.Strategy.Mission, Il2CppMenace.Strategy.LightConditionType> _hLightConditions;

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
            _hTemplate = GameObj<Il2CppMenace.Strategy.Mission>.ResolveObjField(x => x.m_Template);
            _hDifficulty = GameObj<Il2CppMenace.Strategy.Mission>.ResolveObjField(x => x.m_Difficulty);
            _hWeather = GameObj<Il2CppMenace.Strategy.Mission>.ResolveObjField(x => x.m_Weather);
            _hBiome = GameObj<Il2CppMenace.Strategy.Mission>.ResolveObjField(x => x.m_Biome);
            _hClientFaction = GameObj<Il2CppMenace.Strategy.Mission>.ResolveObjField(x => x.m_ClientFaction);
            _hAssetReward = GameObj<Il2CppMenace.Strategy.Mission>.ResolveObjField(x => x.m_AssetReward);

            _hStatus = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.m_Status);
            _hLayer = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.m_Layer);
            _hSeed = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.m_Seed);
            _hLayerIdx = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.m_LayerIdx);
            _hIdx = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.m_Idx);
            _hTacticalPlaytimeInSec = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.TacticalPlaytimeInSec);
            _hReinforcementsEnabled = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.m_ReinforcementsEnabled);
            _hLightConditions = GameObj<Il2CppMenace.Strategy.Mission>.ResolveField(x => x.m_LightConditions);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Mission.ResolveHandles: Field handle resolution failed", ex);
        }
    }

    // Mission class enums
    public enum MissionStatus { Playable = 0, Locked = 1, Played = 2, Unplayable = 3 }
    public enum MissionLayer { Invalid = 0, First = 1, Middle = 2, Final = 3 }
    public enum LightConditionType { Dawn = 0, Day = 1, Dusk = 2, Night = 3, Random = 4 }

    /// <summary>
    /// Mission information structure.
    /// </summary>
    public class MissionInfo
    {
        public string TemplateId { get; set; }
        public MissionStatus Status { get; set; }
        public MissionLayer Layer { get; set; }
        public int Seed { get; set; }
        public string BiomeId { get; set; }
        public string WeatherId { get; set; }
        public LightConditionType LightCondition { get; set; }
        public string DifficultyId { get; set; }
        public float EnemyArmyPoints { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Objective information structure.
    /// </summary>
    public class ObjectiveInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsComplete { get; set; }
        public bool IsFailed { get; set; }
        public int Progress { get; set; }
        public int TargetProgress { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Retrieves the currently active mission via the TacticalManager.
    /// Returns <c>null</c> if no mission is active or the manager is unavailable.
    /// </summary>
    public static Il2CppMenace.Strategy.Mission GetMission()
    {
        try
        {
            var tm = TacticalManager.Get();
            if (tm == null)
            {
                SdkLogger.Warning("Mission.GetMission: TacticalManager returned null");
                return null;
            }
            return tm.GetMission();
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Mission.GetMission: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Reads and returns a <see cref="MissionInfo"/> snapshot for the given mission,
    /// including its template name, status, layer, seed, biome, weather, light condition,
    /// difficulty, and enemy army points. Fields that fail to read are left at their
    /// default values; a warning or error is logged for each failure.
    /// Returns <c>null</c> if <paramref name="mission"/> is <c>null</c>.
    /// </summary>
    public static MissionInfo GetMissionInfo(Il2CppMenace.Strategy.Mission mission)
    {
        if (mission == null)
        {
            SdkLogger.Warning("Mission.GetMissionInfo: Mission is null");
            return null;
        }

        var info = new MissionInfo { Pointer = mission.Pointer };
        var missionObj = GameObj<Il2CppMenace.Strategy.Mission>.Wrap(mission.Pointer);

        if (!_hTemplate.TryRead(missionObj, out var templateObj))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading Template");
        else if (!Templates._hDataTemplateId.TryRead(GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer), out var templateId))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading TemplateId");
        else
            info.TemplateId = templateId;

        if (!_hStatus.TryRead(missionObj, out var status))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading Status");
        else
            info.Status = (MissionStatus)(int)status;

        if (!_hLayer.TryRead(missionObj, out var layer))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading Layer");
        else
            info.Layer = (MissionLayer)(int)layer;

        if (!_hSeed.TryRead(missionObj, out var seed))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading Seed");
        else
            info.Seed = seed;

        if (!_hBiome.TryRead(missionObj, out var biomeObj))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading Biome");
        else if (!Templates._hDataTemplateId.TryRead(GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(biomeObj.Untyped.Pointer), out var biomeId))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading BiomeId");
        else
            info.BiomeId = biomeId;

        if (!_hWeather.TryRead(missionObj, out var weatherObj))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading Weather");
        else if (!Templates._hDataTemplateId.TryRead(GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(weatherObj.Untyped.Pointer), out var weatherId))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading WeatherId");
        else
            info.WeatherId = weatherId;

        if (!_hLightConditions.TryRead(missionObj, out var lightCondition))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading LightCondition");
        else
            info.LightCondition = (LightConditionType)(int)lightCondition;

        if (!_hDifficulty.TryRead(missionObj, out var difficultyObj))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading Difficulty");
        else if (!Templates._hDataTemplateId.TryRead(GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(difficultyObj.Untyped.Pointer), out var difficultyId))
            SdkLogger.Warning("Mission.GetMissionInfo: Failed reading DifficultyId");
        else
            info.DifficultyId = difficultyId;

        try { info.EnemyArmyPoints = mission.GetEnemyArmyPoints(); }
        catch (Exception ex) { SdkLogger.Error("Mission.GetMissionInfo: Failed reading EnemyArmyPoints", ex); }

        return info;
    }

    /// <summary>
    /// Returns a list of <see cref="ObjectiveInfo"/> snapshots for all objectives
    /// on the given mission. Each entry captures the objective's title, description,
    /// completion/failure state, and current and required progress values.
    /// Null or unreadable objectives are skipped with a warning logged.
    /// Returns an empty list if <paramref name="mission"/> is <c>null</c> or has no objective manager.
    /// </summary>
    public static List<ObjectiveInfo> GetObjectives(Il2CppMenace.Strategy.Mission mission)
    {
        var result = new List<ObjectiveInfo>();

        if (mission == null)
        {
            SdkLogger.Warning("Mission.GetObjectives: Mission is null");
            return result;
        }

        var objectiveManager = mission.Objectives;
        if (objectiveManager == null)
        {
            SdkLogger.Warning("Mission.GetObjectives: ObjectiveManager is null");
            return result;
        }

        IReadOnlyList<Objective> objectives = null;
        try 
        { 
            objectives = (IReadOnlyList<Objective>)objectiveManager.GetObjectives(); 
        }
        catch (Exception ex) 
        { 
            SdkLogger.Error("Mission.GetObjectives: Failed calling GetObjectives", ex); 
            return result; 
        }

        if (objectives == null)
        {
            SdkLogger.Warning("Mission.GetObjectives: GetObjectives returned null");
            return result;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            var obj = objectives[i];

            if (obj == null)
            {
                SdkLogger.Warning("Mission.GetObjectives: Null entry in objectives list");
                continue;
            }

            var info = new ObjectiveInfo { Pointer = obj.Pointer };

            try { info.Name = obj.GetTitle(); }
            catch (Exception ex) { SdkLogger.Error("Mission.GetObjectives: Failed reading Name", ex); }

            try { info.Description = obj.GetTranslatedObjectiveText(); }
            catch (Exception ex) { SdkLogger.Error("Mission.GetObjectives: Failed reading Description", ex); }

            try { info.IsComplete = obj.IsCompleted(); }
            catch (Exception ex) { SdkLogger.Error("Mission.GetObjectives: Failed reading IsComplete", ex); }

            try { info.IsFailed = obj.IsFailed(); }
            catch (Exception ex) { SdkLogger.Error("Mission.GetObjectives: Failed reading IsFailed", ex); }

            try { info.Progress = obj.GetProgress(); }
            catch (Exception ex) { SdkLogger.Error("Mission.GetObjectives: Failed reading Progress", ex); }

            try 
            { 
                info.TargetProgress = obj.GetRequiredProgress(); 
            }
            catch (Exception ex) 
            { 
                SdkLogger.Error("Mission.GetObjectives: Failed reading TargetProgress", ex); 
            }

            result.Add(info);
        }

        return result;
    }

    /// <summary>
    /// Returns the <see cref="MissionStatus"/> of the current active mission.
    /// Returns <c>null</c> if no mission is active or the TacticalManager is unavailable.
    /// </summary>
    public static MissionStatus? GetStatus()
    {
        try
        {
            var mission = TacticalManager.Get()?.GetMission();
            if (mission == null)
            {
                SdkLogger.Warning("Mission.GetStatus: No active mission");
                return null;
            }
            return (MissionStatus)(int)mission.GetStatus();
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Mission.GetStatus: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if mission is playable.
    /// </summary>
    public static bool IsPlayable()
    {
        var status = GetStatus();
        if (status == null) return false;
        return status == MissionStatus.Playable;
    }

    /// <summary>
    /// Check if mission is locked.
    /// </summary>
    public static bool IsLocked()
    {
        var status = GetStatus();
        if (status == null) return false;
        return status == MissionStatus.Locked;
    }

    /// <summary>
    /// Check if mission has been played.
    /// </summary>
    public static bool IsPlayed()
    {
        var status = GetStatus();
        if (status == null) return false;
        return status == MissionStatus.Played;
    }

    /// <summary>
    /// Check if the mission is unplayable.
    /// </summary>
    public static bool IsUnplayable()
    {
        var status = GetStatus();
        if (status == null) return false;
        return status == MissionStatus.Unplayable;
    }

    /// <summary>
    /// Force-completes all objectives on the current mission that are not already
    /// completed or failed. Silently skips objectives that are in a terminal state.
    /// Logs a warning or error if the TacticalManager, mission, or objective manager
    /// is unavailable, or if <c>ForceComplete</c> throws on any individual objective.
    /// </summary>
    public static void CompletePendingObjectives()
    {
        var tm = TacticalManager.Get();
        if (tm == null)
        {
            SdkLogger.Warning("Mission.CompletePendingObjectives: TacticalManager is null");
            return;
        }

        var mission = tm.GetMission();
        if (mission == null)
        {
            SdkLogger.Warning("Mission.CompletePendingObjectives: Mission is null");
            return;
        }

        var objectiveManager = mission.Objectives;
        if (objectiveManager == null)
        {
            SdkLogger.Warning("Mission.CompletePendingObjectives: ObjectiveManager is null");
            return;
        }

        IReadOnlyList<Objective> objectives = null;
        try 
        { 
            objectives = (IReadOnlyList<Objective>)objectiveManager.GetObjectives(); 
        }
        catch (Exception ex) 
        { 
            SdkLogger.Error("Mission.CompletePendingObjectives: Failed calling GetObjectives", ex); return; 
        }

        if (objectives == null)
        {
            SdkLogger.Warning("Mission.CompletePendingObjectives: GetObjectives returned null");
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            var obj = objectives[i];

            if (obj == null)
            {
                SdkLogger.Warning("Mission.CompletePendingObjectives: Null entry in objectives list");
                continue;
            }

            if (obj.IsCompleted() || obj.IsFailed()) continue;

            try 
            { 
                obj.ForceComplete(); 
            }
            catch (Exception ex) 
            { 
                SdkLogger.Error("Mission.CompletePendingObjectives: Failed calling ForceComplete", ex); 
            }
        }
    }

    /// <summary>
    /// Force-completes the objective at the specified <paramref name="index"/> in the
    /// current mission's objective list. Does nothing and returns <c>false</c> if the
    /// index is out of range, the objective is already completed or failed, or any
    /// required manager is unavailable. Returns <c>true</c> on success.
    /// </summary>
    public static bool CompleteObjective(int index)
    {
        var tm = TacticalManager.Get();
        if (tm == null)
        {
            SdkLogger.Warning("Mission.CompleteObjective: TacticalManager is null");
            return false;
        }

        var mission = tm.GetMission();
        if (mission == null)
        {
            SdkLogger.Warning("Mission.CompleteObjective: Mission is null");
            return false;
        }

        var objectiveManager = mission.Objectives;
        if (objectiveManager == null)
        {
            SdkLogger.Warning("Mission.CompleteObjective: ObjectiveManager is null");
            return false;
        }

        IReadOnlyList<Objective> objectives = null;
        try { objectives = (IReadOnlyList<Objective>)objectiveManager.GetObjectives(); }
        catch (Exception ex) { SdkLogger.Error("Mission.CompleteObjective: Failed calling GetObjectives", ex); return false; }

        if (objectives == null)
        {
            SdkLogger.Warning("Mission.CompleteObjective: GetObjectives returned null");
            return false;
        }

        if (index < 0 || index >= objectives.Count)
        {
            SdkLogger.Warning($"Mission.CompleteObjective: Index {index} out of range (count: {objectives.Count})");
            return false;
        }

        var obj = objectives[index];
        if (obj == null)
        {
            SdkLogger.Warning($"Mission.CompleteObjective: Objective at index {index} is null");
            return false;
        }

        if (obj.IsCompleted() || obj.IsFailed()) return false;

        try 
        { 
            obj.ForceComplete(); 
            return true; 
        }
        catch (Exception ex) 
        { 
            SdkLogger.Error($"Mission.CompleteObjective: Failed calling ForceComplete on objective {index}", ex); 
            return false; 
        }
    }

    /// <summary>
    /// Registers the following dev-console commands for in-game use:
    /// <list type="bullet">
    /// <item><c>mission</c> — Prints template name, status, layer, seed, biome, weather, light condition, difficulty, and enemy army points for the active mission.</item>
    /// <item><c>objectives</c> — Lists all objectives with their index, completion state, and progress counters.</item>
    /// <item><c>completeobjective &lt;index&gt;</c> — Force-completes the objective at the given index.</item>
    /// <item><c>missionstatus</c> — Prints the current mission status and a summary count of completed, failed, and remaining objectives.</item>
    /// </list>
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // mission - Show current mission info
        DevConsole.RegisterCommand("mission", "", "Show current mission info", args =>
        {
            var tm = TacticalManager.Get();
            if (tm == null)
                return "No active mission";

            var mission = tm.GetMission();
            if (mission == null)
                return "No active mission";

            var info = GetMissionInfo(mission);
            if (info == null)
                return "No active mission";

            return $"Mission: {info.TemplateId}\n" +
                   $"Status: {info.Status}, Layer: {info.Layer}\n" +
                   $"Seed: {info.Seed}\n" +
                   $"Biome: {info.BiomeId ?? "N/A"}, Weather: {info.WeatherId ?? "N/A"}\n" +
                   $"Light: {info.LightCondition}, Difficulty: {info.DifficultyId ?? "N/A"}\n" +
                   $"Enemy Army Points: {info.EnemyArmyPoints}";
        });

        // objectives - List mission objectives
        DevConsole.RegisterCommand("objectives", "", "List mission objectives", args =>
        {
            var tm = TacticalManager.Get();
            if (tm == null)
                return "No active mission";

            var mission = tm.GetMission();
            if (mission == null)
                return "No active mission";

            var objectives = GetObjectives(mission);
            if (objectives.Count == 0)
                return "No objectives";

            var lines = new List<string> { $"Objectives ({objectives.Count}):" };
            for (int i = 0; i < objectives.Count; i++)
            {
                var obj = objectives[i];
                var status = obj.IsComplete ? "[DONE]" : obj.IsFailed ? "[FAIL]" : "[    ]";
                var progress = obj.TargetProgress > 0 ? $" [{obj.Progress}/{obj.TargetProgress}]" : "";
                lines.Add($"  {i}. {status} {obj.Name}{progress}");
            }
            return string.Join("\n", lines);
        });

        // completeobjective <index> - Complete an objective
        DevConsole.RegisterCommand("completeobjective", "<index>", "Complete an objective", args =>
        {
            if (args.Length == 0)
                return "Usage: completeobjective <index>";
            if (!int.TryParse(args[0], out int index))
                return "Invalid index";

            var mission = TacticalManager.Get()?.GetMission();
            if (mission == null)
                return "No active mission";

            var objectives = GetObjectives(mission);
            if (index < 0 || index >= objectives.Count)
                return $"Objective {index} is out of range";

            var obj = objectives[index];
            if (obj.IsComplete || obj.IsFailed)
                return $"Objective {index} is already complete or failed";

            return CompleteObjective(index)
                ? $"Completed objective {index}"
                : "Failed to complete objective";
        });

        // missionstatus - Show mission status
        DevConsole.RegisterCommand("missionstatus", "", "Show mission status", args =>
        {
            var tm = TacticalManager.Get();
            if (tm == null)
                return "No active mission";

            var mission = tm.GetMission();
            if (mission == null)
                return "No active mission";

            var status = GetStatus();
            if (status == null)
                return "Error: could not read mission status";
            var objectives = GetObjectives(mission);
            int complete = objectives.FindAll(o => o.IsComplete).Count;
            int failed = objectives.FindAll(o => o.IsFailed).Count;

            return $"Mission Status: {status}\n" +
                   $"Objectives: {complete} complete, {failed} failed, {objectives.Count - complete - failed} remaining";
        });
    }
}
