using Il2CppInterop.Runtime;
using Il2CppMenace.Tactical;
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
/// - Actor.m_DetectedMask @ 0x138 (ulong bitmask, one bit per faction)
/// - Supports 64 factions (bits 0-63)
/// </summary>
public static class EntityVisibility
{
    private static class Offsets
    {
        // Actor.m_DetectedMask @ 0x138, typed ulong (64-faction mask).
        internal static readonly Lazy<FieldHandle<Actor, ulong>> DetectedMask
            = new(() => GameObj<Actor>.ResolveField(x => x.m_DetectedMask));

        // Entity.m_FactionID @ 0x4C, typed int.
        // Prior code resolved "m_Faction" via OffsetCache — that field does not exist.
        // Correct name is m_FactionID on Entity.
        internal static readonly Lazy<FieldHandle<Entity, int>> FactionID
            = new(() => GameObj<Entity>.ResolveField(x => x.m_FactionID));
    }

    // Temporary visibility override storage
    private static Dictionary<IntPtr, VisibilityOverride> _overrides = new();

    /// <summary>
    /// Temporary visibility override data.
    /// </summary>
    private class VisibilityOverride
    {
        public ulong OriginalMask { get; set; }
        public ulong OverrideMask { get; set; }
        public int TurnsRemaining { get; set; }
        public GameObj<Actor> Viewer { get; set; }
    }

    /// <summary>
    /// Reveal actor to a specific faction.
    /// </summary>
    /// <param name="actor">The actor to reveal</param>
    /// <param name="factionIndex">Faction index (0-63)</param>
    /// <returns>True if successful</returns>
    public static bool RevealToFaction(GameObj<Actor> actor, int factionIndex)
    {
        if (actor.Untyped.IsNull || factionIndex < 0 || factionIndex >= 64)
            return false;

        try
        {
            var mask = Offsets.DetectedMask.Value.Read(actor);
            mask |= (1UL << factionIndex);
            Offsets.DetectedMask.Value.Write(actor, mask);
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
    /// <param name="factionIndex">Faction index (0-63)</param>
    /// <returns>True if successful</returns>
    public static bool ConcealFromFaction(GameObj<Actor> actor, int factionIndex)
    {
        if (actor.Untyped.IsNull || factionIndex < 0 || factionIndex >= 64)
            return false;

        try
        {
            var mask = Offsets.DetectedMask.Value.Read(actor);
            mask &= ~(1UL << factionIndex);
            Offsets.DetectedMask.Value.Write(actor, mask);
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
    /// <param name="bitmask">The detection bitmask (one bit per faction, 64 factions supported)</param>
    /// <returns>True if successful</returns>
    public static bool SetDetectionMask(GameObj<Actor> actor, ulong bitmask)
    {
        if (actor.Untyped.IsNull)
            return false;

        try
        {
            Offsets.DetectedMask.Value.Write(actor, bitmask);
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
    public static ulong GetDetectionMask(GameObj<Actor> actor)
    {
        if (actor.Untyped.IsNull)
            return 0UL;

        try
        {
            return Offsets.DetectedMask.Value.Read(actor);
        }
        catch
        {
            return 0UL;
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
    public static bool ForceVisibleTo(GameObj<Actor> actor, GameObj<Actor> viewer, int turns = 1)
    {
        if (actor.Untyped.IsNull || viewer.Untyped.IsNull || turns <= 0)
            return false;

        try
        {
            var viewerAsEntity = GameObj<Entity>.Wrap(viewer.Untyped);
            var viewerFaction = Offsets.FactionID.Value.Read(viewerAsEntity);
            if (viewerFaction < 0 || viewerFaction >= 64)
                return false;

            var originalMask = GetDetectionMask(actor);
            var newMask = originalMask | (1UL << viewerFaction);

            _overrides[actor.Untyped.Pointer] = new VisibilityOverride
            {
                OriginalMask = originalMask,
                OverrideMask = newMask,
                TurnsRemaining = turns,
                Viewer = viewer
            };

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
    public static bool ForceConcealedFrom(GameObj<Actor> actor, GameObj<Actor> viewer, int turns = 1)
    {
        if (actor.Untyped.IsNull || viewer.Untyped.IsNull || turns <= 0)
            return false;

        try
        {
            var viewerAsEntity = GameObj<Entity>.Wrap(viewer.Untyped);
            var viewerFaction = Offsets.FactionID.Value.Read(viewerAsEntity);
            if (viewerFaction < 0 || viewerFaction >= 64)
                return false;

            var originalMask = GetDetectionMask(actor);
            var newMask = originalMask & ~(1UL << viewerFaction);

            _overrides[actor.Untyped.Pointer] = new VisibilityOverride
            {
                OriginalMask = originalMask,
                OverrideMask = newMask,
                TurnsRemaining = turns,
                Viewer = viewer
            };

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
            var over = kvp.Value;
            over.TurnsRemaining--;

            if (over.TurnsRemaining <= 0)
            {
                try
                {
                    var actor = GameObj<Actor>.Wrap(GameObj.FromPointer(kvp.Key));
                    SetDetectionMask(actor, over.OriginalMask);
                }
                catch { }

                expired.Add(kvp.Key);
            }
        }

        foreach (var ptr in expired)
            _overrides.Remove(ptr);
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
                var actor = GameObj<Actor>.Wrap(GameObj.FromPointer(kvp.Key));
                SetDetectionMask(actor, kvp.Value.OriginalMask);
            }
            catch { }
        }

        _overrides.Clear();
    }
}