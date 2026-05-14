using Il2CppInterop.Runtime;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// SDK module for managing entity visibility and detection states.
/// Provides control over faction-based detection and temporary visibility overrides.
///
/// Based on reverse engineering findings:
/// - Actor.m_DetectedByFactionMask @ 0x138 (int32 bitmask, one bit per faction)
/// - Supports 32 factions (bits 0-31)
/// </summary>
public static class EntityVisibility
{
    // Field offset from Intercept.cs
    private const uint OFFSET_ACTOR_DETECTED_BY_FACTION_MASK = 0x138;

    // Temporary visibility override storage
    private static Dictionary<IntPtr, VisibilityOverride> _overrides = new();

    /// <summary>
    /// Temporary visibility override data.
    /// </summary>
    private class VisibilityOverride
    {
        public int OriginalMask { get; set; }
        public int OverrideMask { get; set; }
        public int TurnsRemaining { get; set; }
        public GameObj Viewer { get; set; }
    }

    /// <summary>
    /// Reveal actor to a specific faction.
    /// </summary>
    /// <param name="actor">The actor to reveal</param>
    /// <param name="factionIndex">Faction index (0-31)</param>
    /// <returns>True if successful</returns>
    public static bool RevealToFaction(GameObj actor, int factionIndex)
    {
        if (actor.IsNull || factionIndex < 0 || factionIndex >= 32)
            return false;

        try
        {
            var mask = Marshal.ReadInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK);
            mask |= (1 << factionIndex);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, mask);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityVisibility.RevealToFaction", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Conceal actor from a specific faction.
    /// </summary>
    /// <param name="actor">The actor to conceal</param>
    /// <param name="factionIndex">Faction index (0-31)</param>
    /// <returns>True if successful</returns>
    public static bool ConcealFromFaction(GameObj actor, int factionIndex)
    {
        if (actor.IsNull || factionIndex < 0 || factionIndex >= 32)
            return false;

        try
        {
            var mask = Marshal.ReadInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK);
            mask &= ~(1 << factionIndex);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, mask);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityVisibility.ConcealFromFaction", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set the entire detection mask at once.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="bitmask">The detection bitmask (one bit per faction)</param>
    /// <returns>True if successful</returns>
    public static bool SetDetectionMask(GameObj actor, int bitmask)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, bitmask);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityVisibility.SetDetectionMask", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the current detection mask.
    /// </summary>
    /// <param name="actor">The actor to query</param>
    /// <returns>The detection bitmask (one bit per faction)</returns>
    public static int GetDetectionMask(GameObj actor)
    {
        if (actor.IsNull)
            return 0;

        try
        {
            return Marshal.ReadInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Force actor to be visible to a specific viewer for N turns.
    /// Uses temporary override system that restores original visibility.
    /// </summary>
    /// <param name="actor">The actor to make visible</param>
    /// <param name="viewer">The viewing actor</param>
    /// <param name="turns">Number of turns to maintain visibility (default: 1)</param>
    /// <returns>True if successful</returns>
    public static bool ForceVisibleTo(GameObj actor, GameObj viewer, int turns = 1)
    {
        if (actor.IsNull || viewer.IsNull || turns <= 0)
            return false;

        try
        {
            // Get viewer's faction
            var klass = IL2CPP.il2cpp_object_get_class(viewer.Pointer);
            var factionOffset = OffsetCache.GetOrResolve(klass, "m_Faction");
            var viewerFaction = viewer.ReadInt(factionOffset);
            if (viewerFaction < 0 || viewerFaction >= 32)
                return false;

            // Store original mask
            var originalMask = GetDetectionMask(actor);

            // Create override
            var newMask = originalMask | (1 << viewerFaction);

            _overrides[actor.Pointer] = new VisibilityOverride
            {
                OriginalMask = originalMask,
                OverrideMask = newMask,
                TurnsRemaining = turns,
                Viewer = viewer
            };

            // Apply override
            SetDetectionMask(actor, newMask);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityVisibility.ForceVisibleTo", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Force actor to be concealed from a specific viewer for N turns.
    /// Uses temporary override system that restores original visibility.
    /// </summary>
    /// <param name="actor">The actor to conceal</param>
    /// <param name="viewer">The viewing actor</param>
    /// <param name="turns">Number of turns to maintain concealment (default: 1)</param>
    /// <returns>True if successful</returns>
    public static bool ForceConcealedFrom(GameObj actor, GameObj viewer, int turns = 1)
    {
        if (actor.IsNull || viewer.IsNull || turns <= 0)
            return false;

        try
        {
            // Get viewer's faction
            var klass = IL2CPP.il2cpp_object_get_class(viewer.Pointer);
            var factionOffset = OffsetCache.GetOrResolve(klass, "m_Faction");
            var viewerFaction = viewer.ReadInt(factionOffset);
            if (viewerFaction < 0 || viewerFaction >= 32)
                return false;

            // Store original mask
            var originalMask = GetDetectionMask(actor);

            // Create override
            var newMask = originalMask & ~(1 << viewerFaction);

            _overrides[actor.Pointer] = new VisibilityOverride
            {
                OriginalMask = originalMask,
                OverrideMask = newMask,
                TurnsRemaining = turns,
                Viewer = viewer
            };

            // Apply override
            SetDetectionMask(actor, newMask);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityVisibility.ForceConcealedFrom", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Update visibility overrides (call from TacticalEventHooks.OnTurnEnd).
    /// </summary>
    internal static void UpdateOverrides()
    {
        var expired = new List<IntPtr>();

        foreach (var kvp in _overrides)
        {
            var actorPtr = kvp.Key;
            var over = kvp.Value;

            over.TurnsRemaining--;

            if (over.TurnsRemaining <= 0)
            {
                // Restore original mask
                try
                {
                    Marshal.WriteInt32(actorPtr + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, over.OriginalMask);
                }
                catch { }

                expired.Add(actorPtr);
            }
        }

        // Remove expired overrides
        foreach (var ptr in expired)
        {
            _overrides.Remove(ptr);
        }
    }

    /// <summary>
    /// Clear all visibility overrides.
    /// </summary>
    public static void ClearAllOverrides()
    {
        foreach (var kvp in _overrides)
        {
            try
            {
                Marshal.WriteInt32(kvp.Key + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, kvp.Value.OriginalMask);
            }
            catch { }
        }

        _overrides.Clear();
    }
}
