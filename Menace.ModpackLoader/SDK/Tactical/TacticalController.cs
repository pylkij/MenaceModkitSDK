using System;
using System.Reflection;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Faction types matching the game's FactionType enum.
/// </summary>
public enum FactionType
{
    Neutral = 0,
    Player = 1,
    PlayerAI = 2,
    Civilian = 3,
    AlliedLocalForces = 4,
    EnemyLocalForces = 5,
    Pirates = 6,
    Wildlife = 7,
    Constructs = 8,
    RogueArmy = 9
}

/// <summary>
/// Reason for finishing a tactical mission.
/// </summary>
public enum TacticalFinishReason
{
    None = 0,
    AllPlayerUnitsDead = 1,
    Leave = 2,
    LoadingSavegame = 3
}
public class TacticalStateInfo
{
    public int RoundNumber { get; set; }
    public FactionType CurrentFactionType { get; set; }
    public bool IsPlayerTurn { get; set; }
    public bool IsPaused { get; set; }
    public float TimeScale { get; set; }
    public bool IsMissionRunning { get; set; }
    public string ActiveActorName { get; set; }
    public bool IsAnyPlayerAlive { get; set; }
    public bool IsAnyEnemyAlive { get; set; }
    public int TotalEnemyCount { get; set; }
    public int DeadEnemyCount { get; set; }
    public int AliveEnemyCount { get; set; }
}

/// <summary>
/// Controls tactical game state including rounds, turns, time scale, and mission flow.
/// Wraps <c>TacticalManager</c> and <c>TacticalState</c> singletons.
/// Safe to call any time after <c>GameState.SceneLoaded</c> has fired for a tactical scene.
/// </summary>
public static class TacticalController
{
    /// <summary>
    /// Get the current round number (1-indexed).
    /// </summary>
    public static int GetCurrentRound()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return 0;

        return GameMethod.CallInt<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.GetRound());
    }

    /// <summary>
    /// Get the currently active faction ID.
    /// </summary>
    internal static int GetCurrentFaction()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return -1;

        return GameMethod.CallInt<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.GetActiveFactionID());
    }

    /// <summary>
    /// Get the current faction type.
    /// </summary>
    public static FactionType GetCurrentFactionType()
    {
        var factionId = GetCurrentFaction();
        if (factionId < 0 || factionId > 9)
            return FactionType.Neutral;
        return (FactionType)factionId;
    }

    /// <summary>
    /// Check if it's the player's turn.
    /// </summary>
    public static bool IsPlayerTurn()
    {
        return GetCurrentFactionType() == FactionType.Player;
    }

    /// <summary>
    /// Check if the game is paused.
    /// </summary>
    public static bool IsPaused()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return false;

        return GameMethod.CallBool<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.IsPaused());
    }

    /// <summary>
    /// Pause or unpause the game.
    /// </summary>
    public static bool SetPaused(bool paused)
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return false;

        GameMethod.Call<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.SetPaused(default), new object[] { paused });
        SdkLogger.Msg($"Game {(paused ? "paused" : "unpaused")}");
        return true;
    }

    /// <summary>
    /// Toggle pause state.
    /// </summary>
    internal static bool TogglePause()
    {
        return SetPaused(!IsPaused());
    }

    /// <summary>
    /// Get the current time scale (game speed).
    /// </summary>
    public static float GetTimeScale()
    {
        return Time.timeScale;
    }

    /// <summary>
    /// Set the time scale (game speed).
    /// </summary>
    /// <param name="scale">Time scale (1.0 = normal, 2.0 = 2x speed, 0.5 = half speed)</param>
    public static bool SetTimeScale(float scale)
    {
        var clamped = Math.Clamp(scale, 0f, 10f);
        Time.timeScale = clamped;
        SdkLogger.Msg($"Time scale set to {clamped}");
        return true;
    }

    /// <summary>
    /// Advance to the next round.
    /// </summary>
    public static bool NextRound()
    {
        try
        {
            var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
                x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
            if (tm == null) return false;

            // NextRound() is private — GameMethod expression trees cannot reference private members.
            // Raw reflection is intentionally retained here until a public alternative is confirmed.
            var nextRoundMethod = typeof(Il2CppMenace.Tactical.TacticalManager)
                .GetMethod("NextRound", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nextRoundMethod == null) return false;

            nextRoundMethod.Invoke(tm, null);
            SdkLogger.Msg($"Advanced to round {GetCurrentRound()}");
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TacticalController.NextRound: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Advance to the next faction's turn.
    /// </summary>
    public static bool NextFaction()
    {
        try
        {
            var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
                x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
            if (tm == null) return false;

            // NextFaction() is private — GameMethod expression trees cannot reference private members.
            // Raw reflection is intentionally retained here until a public alternative is confirmed.
            var nextFactionMethod = typeof(Il2CppMenace.Tactical.TacticalManager)
                .GetMethod("NextFaction", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nextFactionMethod == null) return false;

            nextFactionMethod.Invoke(tm, null);
            SdkLogger.Msg($"Advanced to faction {GetCurrentFaction()}");
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("TacticalController.NextFaction: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// End the current turn (for player faction).
    /// </summary>
    public static bool EndTurn()
    {
        var ts = GameMethod.CallStatic<Il2CppMenace.States.TacticalState>(
            x => Il2CppMenace.States.TacticalState.Get()) as Il2CppMenace.States.TacticalState;
        if (ts == null) return false;

        GameMethod.Call<Il2CppMenace.States.TacticalState>(ts, x => x.EndTurn());
        SdkLogger.Msg("Ended turn");
        return true;
    }

    /// <summary>
    /// Get the currently active actor (selected unit).
    /// </summary>
    public static GameObj GetActiveActor()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return GameObj.Null;

        var result = GameMethod.Call<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.GetActiveActor());
        if (result is not Il2CppMenace.Tactical.Actor actor) return GameObj.Null;

        return new GameObj(actor.Pointer);
    }

    /// <summary>
    /// Set the active actor.
    /// </summary>
    public static bool SetActiveActor(GameObj actor)
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return false;

        var actorProxy = actor.IsNull ? null : actor.As<Il2CppMenace.Tactical.Actor>();
        GameMethod.Call<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.SetActiveActor(default, default), new object[] { actorProxy, true });
        return true;
    }

    /// <summary>
    /// Get total count of enemy actors.
    /// </summary>
    public static int GetTotalEnemyCount()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return 0;

        return GameMethod.CallInt<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.GetActorCount(default, default, default, default, default), new object[] { false, true, true, true, null });
    }

    /// <summary>
    /// Get count of dead enemy actors.
    /// </summary>
    public static int GetDeadEnemyCount()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return 0;

        return GameMethod.CallInt<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.GetActorCount(default, default, default, default, default), new object[] { false, true, false, true, null });
    }

    /// <summary>
    /// Check if the mission is still running.
    /// </summary>
    public static bool IsMissionRunning()
    {
        return (bool)(GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.IsMissionRunning()) ?? false);
    }

    /// <summary>
    /// Check if any player unit is still alive.
    /// </summary>
    public static bool IsAnyPlayerUnitAlive()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return false;

        return GameMethod.CallBool<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.IsAnyPlayerUnitAlive());
    }

    /// <summary>
    /// Check if any AI/enemy unit is still alive.
    /// </summary>
    public static bool IsAnyEnemyAlive()
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return false;

        return GameMethod.CallBool<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.IsAnyAIUnitAlive());
    }

    /// <summary>
    /// Get comprehensive tactical state info.
    /// </summary>
    public static TacticalStateInfo GetTacticalState()
    {
        var activeActor = GetActiveActor();
        string activeActorName = null;
        if (!activeActor.IsNull)
            activeActorName = activeActor.GetName();

        var currentFaction = GetCurrentFactionType();
        var totalEnemies = GetTotalEnemyCount();
        var deadEnemies = GetDeadEnemyCount();

        return new TacticalStateInfo
        {
            RoundNumber = GetCurrentRound(),
            CurrentFactionType = currentFaction,
            IsPlayerTurn = IsPlayerTurn(),
            IsPaused = IsPaused(),
            TimeScale = GetTimeScale(),
            IsMissionRunning = IsMissionRunning(),
            ActiveActorName = activeActorName,
            IsAnyPlayerAlive = IsAnyPlayerUnitAlive(),
            IsAnyEnemyAlive = IsAnyEnemyAlive(),
            TotalEnemyCount = totalEnemies,
            DeadEnemyCount = deadEnemies,
            AliveEnemyCount = totalEnemies - deadEnemies
        };
    }

    /// <summary>
    /// Clear all enemies from the battlefield.
    /// </summary>
    public static int ClearAllEnemies()
    {
        return EntitySpawner.ClearEnemies(immediate: true);
    }

    /// <summary>
    /// Skip the AI turn (immediately end enemy turn).
    /// </summary>
    public static bool SkipAITurn()
    {
        var faction = GetCurrentFactionType();
        // Skip if current faction is not player-controlled
        if (faction == FactionType.Player)
            return false;

        return NextFaction();
    }

    /// <summary>
    /// Finish the mission with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for finishing the mission</param>
    public static bool FinishMission(TacticalFinishReason reason = TacticalFinishReason.Leave)
    {
        var tm = GameMethod.CallStatic<Il2CppMenace.Tactical.TacticalManager>(
            x => Il2CppMenace.Tactical.TacticalManager.Get()) as Il2CppMenace.Tactical.TacticalManager;
        if (tm == null) return false;

        GameMethod.Call<Il2CppMenace.Tactical.TacticalManager>(tm, x => x.Finish(default), new object[] { (Il2CppMenace.Tactical.TacticalFinishReason)reason });
        SdkLogger.Msg($"Mission finished with reason: {reason}");
        return true;
    }
}
