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
    // Mission status enums
    public enum MissionStatus { Pending = 0, Active = 1, Complete = 2, Failed = 3 }

    // Mission layer enums
    public enum MissionLayer { Surface = 0, Underground = 1, Interior = 2, Space = 3, Random = 4 }

    /// <summary>
    /// Mission information structure.
    /// </summary>
    public class MissionInfo
    {
        public string TemplateName { get; set; }
        public MissionStatus Status { get; set; }
        public MissionLayer Layer { get; set; }
        public int Seed { get; set; }
        public string BiomeName { get; set; }
        public string WeatherName { get; set; }
        public LightConditionType LightCondition { get; set; }
        public string DifficultyName { get; set; }
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
                ModError.WarnInternal("Mission.GetMission", "TacticalManager returned null");
                return null;
            }
            return tm.GetMission();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Mission.GetMission", "Failed", ex);
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
            ModError.WarnInternal("Mission.GetMissionInfo", "Mission is null");
            return null;
        }

        var info = new MissionInfo { Pointer = mission.Pointer };

        try { info.TemplateName = mission.GetTemplate()?.name; }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading TemplateName", ex); }

        try { info.Status = (MissionStatus)(int)mission.GetStatus(); }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading Status", ex); }

        try { info.Layer = (MissionLayer)(int)mission.GetLayer(); }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading Layer", ex); }

        try { info.Seed = mission.GetSeed(); }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading Seed", ex); }

        try { info.BiomeName = mission.GetBiome()?.name; }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading BiomeName", ex); }

        try { info.WeatherName = mission.GetWeatherTemplate()?.name; }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading WeatherName", ex); }

        try { info.LightCondition = (LightConditionType)(int)mission.GetLightCondition(); }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading LightCondition", ex); }

        try { info.DifficultyName = mission.GetDifficulty()?.name; }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading DifficultyName", ex); }

        try { info.EnemyArmyPoints = mission.GetEnemyArmyPoints(); }
        catch (Exception ex) { ModError.ReportInternal("Mission.GetMissionInfo", "Failed reading EnemyArmyPoints", ex); }

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
            ModError.WarnInternal("Mission.GetObjectives", "Mission is null");
            return result;
        }

        var objectiveManager = mission.Objectives;
        if (objectiveManager == null)
        {
            ModError.WarnInternal("Mission.GetObjectives", "ObjectiveManager is null");
            return result;
        }

        IReadOnlyList<Objective> objectives = null;
        try 
        { 
            objectives = (IReadOnlyList<Objective>)objectiveManager.GetObjectives(); 
        }
        catch (Exception ex) 
        { 
            ModError.ReportInternal("Mission.GetObjectives", "Failed calling GetObjectives", ex); 
            return result; 
        }

        if (objectives == null)
        {
            ModError.WarnInternal("Mission.GetObjectives", "GetObjectives returned null");
            return result;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            var obj = objectives[i];

            if (obj == null)
            {
                ModError.WarnInternal("Mission.GetObjectives", "Null entry in objectives list");
                continue;
            }

            var info = new ObjectiveInfo { Pointer = obj.Pointer };

            try { info.Name = obj.GetTitle(); }
            catch (Exception ex) { ModError.ReportInternal("Mission.GetObjectives", "Failed reading Name", ex); }

            try { info.Description = obj.GetTranslatedObjectiveText(); }
            catch (Exception ex) { ModError.ReportInternal("Mission.GetObjectives", "Failed reading Description", ex); }

            try { info.IsComplete = obj.IsCompleted(); }
            catch (Exception ex) { ModError.ReportInternal("Mission.GetObjectives", "Failed reading IsComplete", ex); }

            try { info.IsFailed = obj.IsFailed(); }
            catch (Exception ex) { ModError.ReportInternal("Mission.GetObjectives", "Failed reading IsFailed", ex); }

            try { info.Progress = obj.GetProgress(); }
            catch (Exception ex) { ModError.ReportInternal("Mission.GetObjectives", "Failed reading Progress", ex); }

            try 
            { 
                info.TargetProgress = obj.GetRequiredProgress(); 
            }
            catch (Exception ex) 
            { 
                ModError.ReportInternal("Mission.GetObjectives", "Failed reading TargetProgress", ex); 
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
                ModError.WarnInternal("Mission.GetStatus", "No active mission");
                return null;
            }
            return (MissionStatus)(int)mission.GetStatus();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Mission.GetStatus", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if mission is active.
    /// </summary>
    public static bool IsActive()
    {
        var status = GetStatus();
        if (status == null) return false;
        return status == MissionStatus.Active;
    }

    /// <summary>
    /// Check if mission is complete.
    /// </summary>
    public static bool IsComplete()
    {
        var status = GetStatus();
        if (status == null) return false;
        return status == MissionStatus.Complete;
    }

    /// <summary>
    /// Check if mission has failed.
    /// </summary>
    public static bool IsFailed()
    {
        var status = GetStatus();
        if (status == null) return false;
        return status == MissionStatus.Failed;
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
            ModError.WarnInternal("Mission.CompletePendingObjectives", "TacticalManager is null");
            return;
        }

        var mission = tm.GetMission();
        if (mission == null)
        {
            ModError.WarnInternal("Mission.CompletePendingObjectives", "Mission is null");
            return;
        }

        var objectiveManager = mission.Objectives;
        if (objectiveManager == null)
        {
            ModError.WarnInternal("Mission.CompletePendingObjectives", "ObjectiveManager is null");
            return;
        }

        IReadOnlyList<Objective> objectives = null;
        try 
        { 
            objectives = (IReadOnlyList<Objective>)objectiveManager.GetObjectives(); 
        }
        catch (Exception ex) 
        { 
            ModError.ReportInternal("Mission.CompletePendingObjectives", "Failed calling GetObjectives", ex); return; 
        }

        if (objectives == null)
        {
            ModError.WarnInternal("Mission.CompletePendingObjectives", "GetObjectives returned null");
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            var obj = objectives[i];

            if (obj == null)
            {
                ModError.WarnInternal("Mission.CompletePendingObjectives", "Null entry in objectives list");
                continue;
            }

            if (obj.IsCompleted() || obj.IsFailed()) continue;

            try 
            { 
                obj.ForceComplete(); 
            }
            catch (Exception ex) 
            { 
                ModError.ReportInternal("Mission.CompletePendingObjectives", "Failed calling ForceComplete", ex); 
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
            ModError.WarnInternal("Mission.CompleteObjective", "TacticalManager is null");
            return false;
        }

        var mission = tm.GetMission();
        if (mission == null)
        {
            ModError.WarnInternal("Mission.CompleteObjective", "Mission is null");
            return false;
        }

        var objectiveManager = mission.Objectives;
        if (objectiveManager == null)
        {
            ModError.WarnInternal("Mission.CompleteObjective", "ObjectiveManager is null");
            return false;
        }

        IReadOnlyList<Objective> objectives = null;
        try { objectives = (IReadOnlyList<Objective>)objectiveManager.GetObjectives(); }
        catch (Exception ex) { ModError.ReportInternal("Mission.CompleteObjective", "Failed calling GetObjectives", ex); return false; }

        if (objectives == null)
        {
            ModError.WarnInternal("Mission.CompleteObjective", "GetObjectives returned null");
            return false;
        }

        if (index < 0 || index >= objectives.Count)
        {
            ModError.WarnInternal("Mission.CompleteObjective", $"Index {index} out of range (count: {objectives.Count})");
            return false;
        }

        var obj = objectives[index];
        if (obj == null)
        {
            ModError.WarnInternal("Mission.CompleteObjective", $"Objective at index {index} is null");
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
            ModError.ReportInternal("Mission.CompleteObjective", $"Failed calling ForceComplete on objective {index}", ex); 
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

            return $"Mission: {info.TemplateName}\n" +
                   $"Status: {info.Status}, Layer: {info.Layer}\n" +
                   $"Seed: {info.Seed}\n" +
                   $"Biome: {info.BiomeName ?? "N/A"}, Weather: {info.WeatherName ?? "N/A"}\n" +
                   $"Light: {info.LightCondition}, Difficulty: {info.DifficultyName ?? "N/A"}\n" +
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
