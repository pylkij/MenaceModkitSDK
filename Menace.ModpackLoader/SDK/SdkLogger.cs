using System;
using MelonLoader;

namespace Menace.SDK;

/// <summary>
/// Severity of a log entry. Used by <see cref="SdkLog.OnError"/> subscribers
/// and DevConsole filtering.
/// </summary>
public enum LogSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Internal logging surface for SDK systems. Writes to MelonLogger, DevConsole,
/// and fires <see cref="OnError"/> for all log events across the SDK and any
/// <see cref="SdkLogger"/> instances created by mod authors.
/// </summary>
internal static class SdkLogger
{
    private static MelonLogger.Instance _instance;

    internal static void Initialize(MelonLogger.Instance logger)
    {
        _instance = logger;
    }

    internal static void Msg(string message)                         => Write("Menace.SDK", _instance, null, message, LogSeverity.Info,    null);
    internal static void Warning(string message)                     => Write("Menace.SDK", _instance, null, message, LogSeverity.Warning, null);
    internal static void Error(string message, Exception? ex = null) => Write("Menace.SDK", _instance, null, message, LogSeverity.Error,   ex);
    internal static void Fatal(string message, Exception? ex = null) => Write("Menace.SDK", _instance, null, message, LogSeverity.Fatal,   ex);

    internal static void Write(string modId, MelonLogger.Instance melon, string? context,
    string message, LogSeverity severity, Exception? ex)
    {
        var prefix = string.IsNullOrEmpty(context) ? $"[{modId}]" : $"[{modId}:{context}]";
        var text   = $"{prefix} {message}";

        switch (severity)
        {
            case LogSeverity.Info:
                melon?.Msg(text);
                DevConsole.Log(text);
                break;
            case LogSeverity.Warning:
                melon?.Warning(text);
                DevConsole.LogWarning(text);
                break;
            case LogSeverity.Error:
            case LogSeverity.Fatal:
                melon?.Error(text);
                if (ex != null) melon?.Error($"{prefix} {ex}");
                DevConsole.LogError(text);
                break;
        }
    }
}

/// <summary>
/// Per-mod logging surface. Create one instance per mod in <c>IModpackPlugin.OnInitialize()</c>:
/// <code>
///     _log = new SdkLogger("MyMod", logger);
/// </code>
/// All entries are written to MelonLogger, DevConsole, and fire <see cref="SdkLog.OnError"/>.
/// </summary>
public class SdkLog
{
    private readonly string _modId;
    private readonly MelonLogger.Instance _instance;

    /// <summary>
    /// Create a logger for a mod plugin. Call from <c>IModpackPlugin.OnInitialize()</c>.
    /// </summary>
    /// <param name="modId">The mod's identifier, used to prefix all log entries.</param>
    /// <param name="melonLogger">The <see cref="MelonLogger.Instance"/> provided by MelonLoader.</param>
    public SdkLog(string modId, MelonLogger.Instance melonLogger)
    {
        _modId = modId;
        _instance = melonLogger;
    }

    public void Msg(string message)                         => Log(null, message, LogSeverity.Info,    null);
    public void Warning(string message)                     => Log(null, message, LogSeverity.Warning, null);
    public void Error(string message, Exception? ex = null) => Log(null, message, LogSeverity.Error,   ex);
    public void Fatal(string message, Exception? ex = null) => Log(null, message, LogSeverity.Fatal,   ex);

    /// <summary>
    /// Log with an explicit sub-system context tag, e.g. "PatchLoader".
    /// Produces entries like "[MyMod:PatchLoader] message" in DevConsole.
    /// </summary>
    public void Log(string? context, string message, LogSeverity severity, Exception? ex = null)
    {
        SdkLogger.Write(_modId, _instance, context, message, severity, ex);
    }
}
