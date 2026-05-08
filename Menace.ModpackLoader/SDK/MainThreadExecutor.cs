#nullable disable
using System;
using System.Collections.Generic;

namespace Menace.SDK;

/// <summary>
/// Executes actions on the Unity main thread.
/// Required for operations like SceneManager.LoadScene() that must run on the main thread.
/// </summary>
public static class MainThreadExecutor
{
    private static readonly Queue<Action> _actionQueue = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Queue an action to be executed on the Unity main thread during the next Update cycle.
    /// </summary>
    public static void Enqueue(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        lock (_lock)
        {
            _actionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Queue an action and wait for it to complete.
    /// WARNING: This blocks the calling thread. Do not call from the main thread!
    /// </summary>
    /// <param name="action">Action to execute</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 5000)</param>
    /// <returns>True if action completed, false if timed out</returns>
    public static bool EnqueueAndWait(Action action, int timeoutMs = 5000)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        var completed = false;
        var exception = (Exception)null;
        var lockObj = new object();

        Enqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                lock (lockObj)
                {
                    completed = true;
                    System.Threading.Monitor.PulseAll(lockObj);
                }
            }
        });

        // Wait for completion
        lock (lockObj)
        {
            var startTime = UnityEngine.Time.realtimeSinceStartup;
            while (!completed)
            {
                var waited = System.Threading.Monitor.Wait(lockObj, timeoutMs);
                if (!waited)
                    return false; // Timeout

                // Check if we should continue waiting
                if (UnityEngine.Time.realtimeSinceStartup - startTime > timeoutMs / 1000f)
                    return false;
            }
        }

        if (exception != null)
            throw exception;

        return true;
    }

    /// <summary>
    /// Process all queued actions. Called from ModpackLoaderMod.OnUpdate().
    /// </summary>
    internal static void ProcessQueue()
    {
        // Snapshot the queue to avoid holding the lock while executing
        Action[] actions;
        lock (_lock)
        {
            if (_actionQueue.Count == 0)
                return;

            actions = _actionQueue.ToArray();
            _actionQueue.Clear();
        }

        // Execute all actions on the main thread
        foreach (var action in actions)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"[MainThreadExecutor] Error executing action: {ex.Message}");
                SdkLogger.Error(ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// Get the number of pending actions.
    /// </summary>
    public static int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _actionQueue.Count;
            }
        }
    }
}
