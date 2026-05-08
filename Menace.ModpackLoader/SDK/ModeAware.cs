#nullable disable
using System;

namespace Menace.SDK;

/// <summary>
/// Helper for executing code that requires specific game modes (tactical, strategy, etc.).
/// Provides mode-aware wrappers that give clear error messages instead of crashes.
/// </summary>
public static class ModeAware
{
    [Flags]
    public enum GameMode
    {
        None = 0,
        Tactical = 1,
        Strategy = 2,
        MainMenu = 4,
        Any = Tactical | Strategy | MainMenu
    }

    /// <summary>
    /// Execute an action only if in the required game mode.
    /// Returns the result on success, or a formatted error message if wrong mode.
    /// </summary>
    /// <example>
    /// var result = ModeAware.Execute(GameMode.Tactical,
    ///     () => TileMap.GetMapInfo()?.Width.ToString() ?? "null",
    ///     "TileMap.GetMapInfo");
    /// </example>
    public static string Execute(GameMode required, Func<string> action, string operationName)
    {
        var currentMode = GetCurrentMode();

        if (!HasMode(currentMode, required))
        {
            return FormatModeError(operationName, required, currentMode);
        }

        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return $"ERROR in {operationName}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute an action only if in the required mode and additional condition is met.
    /// </summary>
    public static string ExecuteWith(GameMode required, Func<bool> condition, Func<string> action,
        string operationName, string conditionDescription)
    {
        var currentMode = GetCurrentMode();

        if (!HasMode(currentMode, required))
        {
            return FormatModeError(operationName, required, currentMode);
        }

        if (!condition())
        {
            return $"ERROR: {operationName} requires {conditionDescription}";
        }

        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return $"ERROR in {operationName}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute a void action only if in the required mode.
    /// Returns success/error message.
    /// </summary>
    public static string ExecuteVoid(GameMode required, Action action, string operationName)
    {
        var currentMode = GetCurrentMode();

        if (!HasMode(currentMode, required))
        {
            return FormatModeError(operationName, required, currentMode);
        }

        try
        {
            action();
            return $"{operationName} completed successfully";
        }
        catch (Exception ex)
        {
            return $"ERROR in {operationName}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// Check if currently in the specified mode.
    /// </summary>
    public static bool IsInMode(GameMode mode)
    {
        var currentMode = GetCurrentMode();
        return HasMode(currentMode, mode);
    }

    /// <summary>
    /// Get the current game mode.
    /// </summary>
    public static GameMode GetCurrentMode()
    {
        if (GameState.IsTactical)
            return GameMode.Tactical;

        var scene = GameState.CurrentScene?.ToLowerInvariant() ?? "";

        if (scene.Contains("strategy") || scene.Contains("geoscape"))
            return GameMode.Strategy;

        if (scene.Contains("main") || scene.Contains("menu") || scene.Contains("title"))
            return GameMode.MainMenu;

        return GameMode.None;
    }

    /// <summary>
    /// Get a human-readable name for the current mode.
    /// </summary>
    public static string GetCurrentModeName()
    {
        return GetCurrentMode() switch
        {
            GameMode.Tactical => "Tactical",
            GameMode.Strategy => "Strategy",
            GameMode.MainMenu => "Main Menu",
            _ => $"Unknown ({GameState.CurrentScene})"
        };
    }

    private static bool HasMode(GameMode current, GameMode required)
    {
        if (required == GameMode.Any)
            return current != GameMode.None;

        return (current & required) != 0;
    }

    private static string FormatModeError(string operation, GameMode required, GameMode current)
    {
        var requiredName = required switch
        {
            GameMode.Tactical => "tactical mode",
            GameMode.Strategy => "strategy mode",
            GameMode.MainMenu => "main menu",
            GameMode.Any => "any game mode",
            _ => required.ToString().ToLowerInvariant()
        };

        var currentName = current switch
        {
            GameMode.Tactical => "tactical mode",
            GameMode.Strategy => "strategy mode",
            GameMode.MainMenu => "main menu",
            GameMode.None => $"unknown mode (scene: {GameState.CurrentScene})",
            _ => current.ToString().ToLowerInvariant()
        };

        return $"ERROR: {operation} requires {requiredName}, but currently in {currentName}";
    }
}
