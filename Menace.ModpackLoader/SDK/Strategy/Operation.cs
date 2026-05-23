using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for campaign operation management.
/// Provides safe access to operations, missions, factions, and strategic assets.
///
/// Based on reverse engineering findings:
/// - Operation.Template @ +0x10
/// - Operation.EnemyFaction @ +0x18
/// - Operation.FriendlyFaction @ +0x20
/// - Operation.CurrentMissionIndex @ +0x40
/// - Operation.Missions @ +0x50
/// - Operation.TimeSpent/TimeLimit @ +0x58, +0x5C
/// </summary>
public static class Operation
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // Reference fields on Il2CppMenace.Strategy.Operation
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.OperationTemplate> _hTemplate;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.StoryFactionTemplate> _hClientFaction;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.FactionTemplate> _hEnemyFaction;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.OperationResult> _hResult;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.PlanetTemplate> _hPlanetTemplate;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.OperationDurationTemplate> _hDuration;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Tools.PseudoRandom> _hRandom;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.OperationProperties> _hInitialProperties;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppMenace.Strategy.OperationProperties> _hCurrentProperties;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, UnityEngine.Texture2D> _hScreenshot;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Operation, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.Mission>> _hMissions;

    // Value fields on Il2CppMenace.Strategy.Operation
    private static FieldHandle<Il2CppMenace.Strategy.Operation, int> _hCurrentMissionIdx;
    private static FieldHandle<Il2CppMenace.Strategy.Operation, int> _hMaxTimeUntilTimeout;
    private static FieldHandle<Il2CppMenace.Strategy.Operation, int> _hPassedTime;
    private static FieldHandle<Il2CppMenace.Strategy.Operation, int> _hSeed;
    private static FieldHandle<Il2CppMenace.Strategy.Operation, int> _hIntroId;
    private static FieldHandle<Il2CppMenace.Strategy.Operation, bool> _hNeedsAutoSave;
    private static FieldHandle<Il2CppMenace.Strategy.Operation, bool> _hAfterOperationFinishedEventsTriggered;

    // Value fields on OperationsManager
    private static FieldHandle<Il2CppMenace.Strategy.OperationsManager, int> _hCurrentOperationIdx;
    private static ObjFieldHandle<Il2CppMenace.Strategy.OperationsManager, Il2CppMenace.Strategy.OperationsManager> _hOperations;
    private static ObjFieldHandle<Il2CppMenace.Strategy.OperationsManager, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.Operation>> _hAvailableOperations;
    private static ObjFieldHandle<Il2CppMenace.Strategy.OperationsManager, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.OperationTemplate>> _hCompletedOperationTypes;

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
            _hTemplate = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_Template);
            _hClientFaction = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_ClientFaction);
            _hEnemyFaction = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_EnemyFaction);
            _hResult = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_Result);
            _hPlanetTemplate = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_PlanetTemplate);
            _hDuration = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_Duration);
            _hRandom = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_Random);
            _hInitialProperties = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_InitialProperties);
            _hCurrentProperties = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_CurrentProperties);
            _hScreenshot = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_Screenshot);

            _hCurrentMissionIdx = GameObj<Il2CppMenace.Strategy.Operation>.ResolveField(x => x.m_CurrentMissionIdx);
            _hMissions = GameObj<Il2CppMenace.Strategy.Operation>.ResolveObjField(x => x.m_Missions);
            _hMaxTimeUntilTimeout = GameObj<Il2CppMenace.Strategy.Operation>.ResolveField(x => x.m_MaxTimeUntilTimeout);
            _hPassedTime = GameObj<Il2CppMenace.Strategy.Operation>.ResolveField(x => x.m_PassedTime);
            _hSeed = GameObj<Il2CppMenace.Strategy.Operation>.ResolveField(x => x.m_Seed);
            _hIntroId = GameObj<Il2CppMenace.Strategy.Operation>.ResolveField(x => x.m_IntroId);
            _hNeedsAutoSave = GameObj<Il2CppMenace.Strategy.Operation>.ResolveField(x => x.m_NeedsAutoSave);
            _hAfterOperationFinishedEventsTriggered = GameObj<Il2CppMenace.Strategy.Operation>.ResolveField(x => x.m_AfterOperationFinishedEventsTriggered);

            _hCurrentOperationIdx = GameObj<Il2CppMenace.Strategy.OperationsManager>.ResolveField(x => x.m_CurrentOperationIdx);
            _hAvailableOperations = GameObj<Il2CppMenace.Strategy.OperationsManager>.ResolveObjField(x => x.m_AvailableOperations);
            _hCompletedOperationTypes = GameObj<Il2CppMenace.Strategy.OperationsManager>.ResolveObjField(x => x.m_CompletedOperationTypes);

            Templates._hDataTemplateId = GameObj<Il2CppMenace.Tools.DataTemplate>.ResolveStringField(x => x.m_ID);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.ResolveHandles", "Field handle resolution failed", ex);
        }
    }

    /// <summary>
    /// Operation information structure.
    /// </summary>
    public class OperationInfo
    {
        public string TemplateName { get; set; }
        public string EnemyFaction { get; set; }
        public string FriendlyFaction { get; set; }
        public string Planet { get; set; }
        public int CurrentMissionIndex { get; set; }
        public int MissionCount { get; set; }
        public int TimeSpent { get; set; }
        public int TimeLimit { get; set; }
        public int TimeRemaining { get; set; }
        public bool HasCompletedOnce { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the current active operation.
    /// </summary>
    public static GameObj GetCurrentOperation()
    {
        try
        {
            var strategyState = GameQuery.FindAllCached<Il2CppMenace.States.StrategyState>().FirstOrDefault();
            if (strategyState == null) return GameObj.Null;

            var strategyStateObj = GameObj<Il2CppMenace.States.StrategyState>.Wrap(
                new GameObj(strategyState.Pointer));
            if (strategyStateObj.Untyped.CheckAlive() != AliveStatus.Alive) return GameObj.Null;

            // StrategyState.Operations is public readonly at 0x58
            var omPtr = strategyStateObj.Untyped.ReadPtr(0x58);
            if (omPtr == IntPtr.Zero) return GameObj.Null;

            if (!GameObj<Il2CppMenace.Strategy.OperationsManager>.TryWrap(new GameObj(omPtr), out var om)) return GameObj.Null;
            if (om.Untyped.CheckAlive() != AliveStatus.Alive) return GameObj.Null;

            if (!_hCurrentOperationIdx.TryRead(om, out var idx)) return GameObj.Null;
            if (!_hAvailableOperations.TryRead(om, out var opsObj)) return GameObj.Null;

            var opsList = new GameList(opsObj.Untyped.Pointer);
            if (idx < 0 || idx >= opsList.Count) return GameObj.Null;

            return opsList[idx];
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetCurrentOperation", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get information about the current operation.
    /// </summary>
    public static OperationInfo GetOperationInfo()
    {
        var op = GetCurrentOperation();
        if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed)) return null;
        return GetOperationInfo(typed);
    }

    /// <summary>
    /// Get information about an operation.
    /// </summary>
    public static OperationInfo GetOperationInfo(GameObj<Il2CppMenace.Strategy.Operation> operation)
    {
        if (operation.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var info = new OperationInfo { Pointer = operation.Untyped.Pointer };

            // Template name
            if (_hTemplate.TryRead(operation, out var templateObj))
                info.TemplateName = templateObj.Untyped.GetName();

            // Enemy faction
            if (_hEnemyFaction.TryRead(operation, out var enemyObj))
                info.EnemyFaction = enemyObj.Untyped.GetName();

            // Friendly faction — client faction is the hiring party (friendly side)
            if (_hClientFaction.TryRead(operation, out var clientObj))
                info.FriendlyFaction = clientObj.Untyped.GetName();

            // Planet name sits on m_PlanetTemplate, not the Planet scene object
            if (_hPlanetTemplate.TryRead(operation, out var planetTemplateObj))
                info.Planet = planetTemplateObj.Untyped.GetName();

            // Mission index and count
            if (_hCurrentMissionIdx.TryRead(operation, out var missionIdx))
                info.CurrentMissionIndex = missionIdx;

            if (_hMissions.TryRead(operation, out var missionsObj))
                info.MissionCount = new GameList(missionsObj.Untyped.Pointer).Count;

            // Time
            if (_hPassedTime.TryRead(operation, out var passed))
                info.TimeSpent = passed;

            if (_hMaxTimeUntilTimeout.TryRead(operation, out var limit))
                info.TimeLimit = limit;

            info.TimeRemaining = GameMethod.CallInt<Il2CppMenace.Strategy.Operation>(
                operation, x => x.GetRemainingTime());

            // HasCompletedOnce still requires OperationsManager — leave as default
            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetOperationInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the current mission from the operation.
    /// </summary>
    public static GameObj GetCurrentMission()
    {
        try
        {
            var op = GetCurrentOperation();
            if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed)) return GameObj.Null;
            if (typed.Untyped.CheckAlive() != AliveStatus.Alive) return GameObj.Null;

            var result = GameMethod.Call<Il2CppMenace.Strategy.Operation>(typed, x => x.GetCurrentMission());
            if (result == null) return GameObj.Null;
            return GameObj.FromPointer(((Il2CppObjectBase)result).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetCurrentMission", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all missions in the current operation.
    /// </summary>
    public static List<GameObj> GetMissions()
    {
        var result = new List<GameObj>();

        try
        {
            var op = GetCurrentOperation();
            if (op.IsNull) return result;

            // Get missions via direct field access at offset +0x50
            var missionsPtr = op.ReadPtr(0x50);
            if (missionsPtr == IntPtr.Zero) return result;

            var missionsList = new GameList(missionsPtr);
            for (int i = 0; i < missionsList.Count; i++)
            {
                var mission = missionsList[i];
                if (!mission.IsNull)
                    result.Add(mission);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetMissions", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Check if there is an active operation.
    /// </summary>
    public static bool HasActiveOperation()
    {
        return GetCurrentOperation().CheckAlive() == AliveStatus.Alive;
    }

    /// <summary>
    /// Get remaining time in the operation.
    /// </summary>
    public static int GetRemainingTime()
    {
        var op = GetCurrentOperation();
        if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed)) return 0;
        if (typed.Untyped.CheckAlive() != AliveStatus.Alive) return 0;
        return GameMethod.CallInt<Il2CppMenace.Strategy.Operation>(typed, x => x.GetRemainingTime());
    }

    /// <summary>
    /// Check if operation can time out.
    /// </summary>
    public static bool CanTimeOut()
    {
        var op = GetCurrentOperation();
        if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed)) return false;
        if (typed.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        return GameMethod.CallBool<Il2CppMenace.Strategy.Operation>(typed, x => x.CanTimeOut());
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Multi-Operation Support
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the OperationsManager instance.
    /// </summary>
    public static GameObj GetOperationsManager()
    {
        try
        {
            var strategyState = GameQuery.FindAllCached<Il2CppMenace.States.StrategyState>().FirstOrDefault();
            if (strategyState == null) return GameObj.Null;

            var strategyStateObj = GameObj<Il2CppMenace.States.StrategyState>.Wrap(
                new GameObj(strategyState.Pointer));
            if (strategyStateObj.Untyped.CheckAlive() != AliveStatus.Alive) return GameObj.Null;

            var omPtr = strategyStateObj.Untyped.ReadPtr(0x58);
            if (omPtr == IntPtr.Zero) return GameObj.Null;

            return GameObj.FromPointer(omPtr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetOperationsManager", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all active operations (not just the current one).
    /// </summary>
    public static List<GameObj> GetAllOperations()
    {
        var result = new List<GameObj>();

        try
        {
            var om = GetOperationsManager();
            if (!GameObj<Il2CppMenace.Strategy.OperationsManager>.TryWrap(om, out var typedOm)) return result;
            if (typedOm.Untyped.CheckAlive() != AliveStatus.Alive) return result;

            if (!_hAvailableOperations.TryRead(typedOm, out var opsObj)) return result;

            var opsList = new GameList(opsObj.Untyped.Pointer);
            for (int i = 0; i < opsList.Count; i++)
            {
                var op = opsList[i];
                if (op.CheckAlive() == AliveStatus.Alive)
                    result.Add(op);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetAllOperations", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get all operation info (for all active operations).
    /// </summary>
    public static List<OperationInfo> GetAllOperationInfo()
    {
        var result = new List<OperationInfo>();
        var operations = GetAllOperations();
        foreach (var op in operations)
        {
            if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed)) continue;
            var info = GetOperationInfo(typed);
            if (info != null)
                result.Add(info);
        }
        return result;
    }

    /// <summary>
    /// Find an operation by faction name.
    /// </summary>
    /// <param name="factionName">Name of enemy or friendly faction.</param>
    public static GameObj FindByFaction(string factionId)
    {
        if (string.IsNullOrEmpty(factionId)) return GameObj.Null;

        var operations = GetAllOperations();
        foreach (var op in operations)
        {
            if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed)) continue;

            if (_hEnemyFaction.TryRead(typed, out var enemy) &&
                GameObj<Il2CppMenace.Tools.DataTemplate>.TryWrap(enemy.Untyped, out var enemyTemplate) &&
                Templates._hDataTemplateId.TryRead(enemyTemplate, out var enemyId) &&
                enemyId == factionId)
                return op;

            if (_hClientFaction.TryRead(typed, out var client) &&
                GameObj<Il2CppMenace.Tools.DataTemplate>.TryWrap(client.Untyped, out var clientTemplate) &&
                Templates._hDataTemplateId.TryRead(clientTemplate, out var clientId) &&
                clientId == factionId)
                return op;
        }
        return GameObj.Null;
    }

    /// <summary>
    /// Find an operation by planet name.
    /// </summary>
    public static GameObj FindByPlanet(string planetId)
    {
        if (string.IsNullOrEmpty(planetId)) return GameObj.Null;

        var operations = GetAllOperations();
        foreach (var op in operations)
        {
            if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed)) continue;

            if (_hPlanetTemplate.TryRead(typed, out var planetTemplate) &&
                GameObj<Il2CppMenace.Tools.DataTemplate>.TryWrap(planetTemplate.Untyped, out var dataTemplate) &&
                Templates._hDataTemplateId.TryRead(dataTemplate, out var id) &&
                id == planetId)
                return op;
        }
        return GameObj.Null;
    }

    /// <summary>
    /// Get completed operation types (operations that have been completed at least once).
    /// </summary>
    public static List<string> GetCompletedOperationTypes()
    {
        var result = new List<string>();

        try
        {
            var om = GetOperationsManager();
            if (!GameObj<Il2CppMenace.Strategy.OperationsManager>.TryWrap(om, out var typedOm)) return result;
            if (typedOm.Untyped.CheckAlive() != AliveStatus.Alive) return result;

            if (!_hCompletedOperationTypes.TryRead(typedOm, out var completedObj)) return result;

            var completedList = new GameList(completedObj.Untyped.Pointer);
            for (int i = 0; i < completedList.Count; i++)
            {
                var entry = completedList[i];
                if (entry.CheckAlive() != AliveStatus.Alive) continue;

                if (GameObj<Il2CppMenace.Tools.DataTemplate>.TryWrap(entry, out var dataTemplate) &&
                    Templates._hDataTemplateId.TryRead(dataTemplate, out var id))
                    result.Add(id);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetCompletedOperationTypes", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Check if an operation type has been completed before.
    /// </summary>
    public static bool HasCompletedOperationType(string operationTemplateId)
    {
        var completed = GetCompletedOperationTypes();
        return completed.Contains(operationTemplateId);
    }

    /// <summary>
    /// Register console commands for Operation SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // operation - Show current operation info
        DevConsole.RegisterCommand("operation", "", "Show current operation info", args =>
        {
            var info = GetOperationInfo();
            if (info == null)
                return "No active operation";

            var timeInfo = info.TimeLimit > 0
                ? $"Time: {info.TimeSpent}/{info.TimeLimit} ({info.TimeRemaining} remaining)"
                : "Time: Unlimited";

            return $"Operation: {info.TemplateName}\n" +
                   $"Planet: {info.Planet ?? "Unknown"}\n" +
                   $"Enemy: {info.EnemyFaction ?? "Unknown"}\n" +
                   $"Allied: {info.FriendlyFaction ?? "Unknown"}\n" +
                   $"Missions: {info.CurrentMissionIndex + 1}/{info.MissionCount}\n" +
                   $"{timeInfo}\n" +
                   $"Completed Before: {info.HasCompletedOnce}";
        });

        // missions - List operation missions
        DevConsole.RegisterCommand("opmissions", "", "List missions in current operation", args =>
        {
            var missions = GetMissions();
            if (missions.Count == 0)
                return "No missions in operation";

            var info = GetOperationInfo();
            var currentIdx = info?.CurrentMissionIndex ?? -1;

            var lines = new List<string> { $"Operation Missions ({missions.Count}):" };
            for (int i = 0; i < missions.Count; i++)
            {
                var typed = GameObj<Il2CppMenace.Strategy.Mission>.Wrap(missions[i]);
                var mission = typed.AsManaged();
                var missionInfo = mission != null ? Mission.GetMissionInfo(mission) : null;
                var current = i == currentIdx ? " <-- CURRENT" : "";
                var status = missionInfo?.Status.ToString() ?? "Unknown";
                lines.Add($"  {i}. {missionInfo?.TemplateName ?? "Unknown"} [{status}]{current}");
            }
            return string.Join("\n", lines);
        });

        // optime - Show operation time
        DevConsole.RegisterCommand("optime", "", "Show operation time remaining", args =>
        {
            var info = GetOperationInfo();
            if (info == null)
                return "No active operation";

            if (info.TimeLimit <= 0)
                return "Operation has no time limit";

            return $"Time: {info.TimeSpent}/{info.TimeLimit}\n" +
                   $"Remaining: {info.TimeRemaining}";
        });

        // alloperations - List all active operations
        DevConsole.RegisterCommand("alloperations", "", "List all active operations", args =>
        {
            var operations = GetAllOperationInfo();
            if (operations.Count == 0)
                return "No active operations";

            var current = GetCurrentOperation();
            var currentPtr = current.IsNull ? IntPtr.Zero : current.Pointer;

            var lines = new List<string> { $"Active Operations ({operations.Count}):" };
            foreach (var op in operations)
            {
                var isCurrent = op.Pointer == currentPtr ? " <-- CURRENT" : "";
                var time = op.TimeLimit > 0 ? $" (Time: {op.TimeRemaining} left)" : "";
                lines.Add($"  {op.TemplateName}: {op.EnemyFaction} vs {op.FriendlyFaction}{time}{isCurrent}");
                lines.Add($"    Planet: {op.Planet}, Mission {op.CurrentMissionIndex + 1}/{op.MissionCount}");
            }
            return string.Join("\n", lines);
        });

        // completedops - List completed operation types
        DevConsole.RegisterCommand("completedops", "", "List completed operation types", args =>
        {
            var completed = GetCompletedOperationTypes();
            if (completed.Count == 0)
                return "No operations completed yet";

            var lines = new List<string> { $"Completed Operation Types ({completed.Count}):" };
            foreach (var c in completed)
            {
                lines.Add($"  {c}");
            }
            return string.Join("\n", lines);
        });

        // findop <faction|planet> - Find operation by faction or planet
        DevConsole.RegisterCommand("findop", "<id>", "Find operation by faction or planet ID", args =>
        {
            if (args.Length == 0)
                return "Usage: findop <faction_or_planet_id>";

            var id = string.Join(" ", args);

            var op = FindByFaction(id);
            if (op.CheckAlive() == AliveStatus.Alive)
            {
                if (GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed))
                {
                    var info = GetOperationInfo(typed);
                    return $"Found by faction:\n" +
                           $"  {info.TemplateName}\n" +
                           $"  Enemy: {info.EnemyFaction}\n" +
                           $"  Friendly: {info.FriendlyFaction}\n" +
                           $"  Planet: {info.Planet}";
                }
            }

            op = FindByPlanet(id);
            if (op.CheckAlive() == AliveStatus.Alive)
            {
                if (GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(op, out var typed))
                {
                    var info = GetOperationInfo(typed);
                    return $"Found by planet:\n" +
                           $"  {info.TemplateName}\n" +
                           $"  Enemy: {info.EnemyFaction}\n" +
                           $"  Friendly: {info.FriendlyFaction}\n" +
                           $"  Planet: {info.Planet}";
                }
            }

            return $"No operation found for '{id}'";
        });
    }

    // --- Internal helpers ---

    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);
}
