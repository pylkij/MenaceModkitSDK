using System;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// SDK module for manipulating entity state flags and visibility.
/// Provides direct memory access to actor boolean states and detection masks.
///
/// Based on reverse engineering findings from Intercept.cs:
/// - Actor.m_IsHeavyWeaponDeployed @ 0x16F
/// - Actor.m_DetectedByFactionMask @ 0x138 (int32 bitmask)
/// - Actor.m_HiddenToAICache @ 0x1A4
/// - Actor.m_IsDying @ 0x16A
/// - Actor.m_IsLeavingMap @ 0x16B
/// </summary>
public static class EntityState
{
    // Field offsets from Intercept.cs
    private const uint OFFSET_ACTOR_IS_HEAVY_WEAPON_DEPLOYED = 0x16F;
    private const uint OFFSET_ACTOR_DETECTED_BY_FACTION_MASK = 0x138;
    private const uint OFFSET_ACTOR_HIDDEN_TO_AI_CACHE = 0x1A4;
    private const uint OFFSET_ACTOR_IS_DYING = 0x16A;
    private const uint OFFSET_ACTOR_IS_LEAVING_MAP = 0x16B;

    /// <summary>
    /// State flags structure for comprehensive state queries.
    /// </summary>
    public struct StateFlags
    {
        public bool IsHeavyWeaponDeployed { get; set; }
        public int DetectionMask { get; set; }
        public bool IsHiddenToAI { get; set; }
        public bool IsDying { get; set; }
        public bool IsLeavingMap { get; set; }
    }

    /// <summary>
    /// Set whether a heavy weapon is deployed.
    /// </summary>
    /// <param name="actor">The actor with the heavy weapon</param>
    /// <param name="deployed">True to deploy, false to undeploy</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetHeavyWeaponDeployed(GameObj actor, bool deployed)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_HEAVY_WEAPON_DEPLOYED,
                deployed ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetHeavyWeaponDeployed", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Toggle heavy weapon deployment state.
    /// </summary>
    /// <param name="actor">The actor with the heavy weapon</param>
    /// <returns>True if state was toggled successfully</returns>
    public static bool ToggleHeavyWeapon(GameObj actor)
    {
        if (actor.IsNull)
            return false;

        try
        {
            var current = Marshal.ReadByte(actor.Pointer + (int)OFFSET_ACTOR_IS_HEAVY_WEAPON_DEPLOYED) != 0;
            return SetHeavyWeaponDeployed(actor, !current);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.ToggleHeavyWeapon", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set detection state for a specific faction.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="faction">Faction index (0-31)</param>
    /// <param name="detected">True to mark as detected, false to conceal</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetDetectedByFaction(GameObj actor, int faction, bool detected)
    {
        if (actor.IsNull || faction < 0 || faction >= 32)
            return false;

        try
        {
            var mask = Marshal.ReadInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK);
            var bit = 1 << faction;

            if (detected)
                mask |= bit;
            else
                mask &= ~bit;

            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, mask);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetDetectedByFaction", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set whether actor is hidden from AI.
    /// Note: Uses cached state field - may not affect all AI systems.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="hidden">True to hide from AI, false to reveal</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetHiddenToAI(GameObj actor, bool hidden)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_HIDDEN_TO_AI_CACHE,
                hidden ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetHiddenToAI", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set whether actor is hidden from player visibility.
    /// Note: Offset needs verification - may require additional research.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="hidden">True to hide from player, false to reveal</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetHiddenToPlayer(GameObj actor, bool hidden)
    {
        if (actor.IsNull)
            return false;

        try
        {
            // Note: This offset needs verification via Ghidra
            // Using adjacent offset as best estimate
            const uint OFFSET_ACTOR_HIDDEN_TO_PLAYER = 0x1A5;
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_HIDDEN_TO_PLAYER,
                hidden ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetHiddenToPlayer", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Reveal actor to all factions (set all detection bits).
    /// </summary>
    /// <param name="actor">The actor to reveal</param>
    /// <returns>True if state was set successfully</returns>
    public static bool RevealToAll(GameObj actor)
    {
        if (actor.IsNull)
            return false;

        try
        {
            // Set all bits to 1 (detected by all factions)
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, -1);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.RevealToAll", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Conceal actor from all factions (clear all detection bits).
    /// </summary>
    /// <param name="actor">The actor to conceal</param>
    /// <returns>True if state was set successfully</returns>
    public static bool ConcealFromAll(GameObj actor)
    {
        if (actor.IsNull)
            return false;

        try
        {
            // Set all bits to 0 (not detected by any faction)
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK, 0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.ConcealFromAll", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set whether actor is in dying state.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="dying">True to mark as dying, false to clear</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetDying(GameObj actor, bool dying)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_DYING,
                dying ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetDying", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set whether actor is leaving the map.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="leaving">True to mark as leaving, false to clear</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetLeavingMap(GameObj actor, bool leaving)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_LEAVING_MAP,
                leaving ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetLeavingMap", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set whether actor is a minion.
    /// Note: Offset needs verification via Ghidra - estimated based on structure.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="isMinion">True to mark as minion, false to clear</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetMinion(GameObj actor, bool isMinion)
    {
        if (actor.IsNull)
            return false;

        try
        {
            // Note: This offset needs verification via Ghidra
            // Estimated based on typical actor structure patterns
            const uint OFFSET_ACTOR_IS_MINION = 0x16D;
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_MINION,
                isMinion ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetMinion", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set whether actor can be selected by player.
    /// Note: Offset needs verification via Ghidra - estimated based on structure.
    /// </summary>
    /// <param name="actor">The actor to modify</param>
    /// <param name="selectable">True to allow selection, false to prevent</param>
    /// <returns>True if state was set successfully</returns>
    public static bool SetSelectableByPlayer(GameObj actor, bool selectable)
    {
        if (actor.IsNull)
            return false;

        try
        {
            // Note: This offset needs verification via Ghidra
            // Estimated based on typical actor structure patterns
            const uint OFFSET_ACTOR_IS_SELECTABLE = 0x16E;
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_SELECTABLE,
                selectable ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.SetSelectableByPlayer", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get all state flags for an actor.
    /// </summary>
    /// <param name="actor">The actor to query</param>
    /// <returns>StateFlags structure with all state information</returns>
    public static StateFlags GetStateFlags(GameObj actor)
    {
        var flags = new StateFlags();

        if (actor.IsNull)
            return flags;

        try
        {
            flags.IsHeavyWeaponDeployed = Marshal.ReadByte(actor.Pointer + (int)OFFSET_ACTOR_IS_HEAVY_WEAPON_DEPLOYED) != 0;
            flags.DetectionMask = Marshal.ReadInt32(actor.Pointer + (int)OFFSET_ACTOR_DETECTED_BY_FACTION_MASK);
            flags.IsHiddenToAI = Marshal.ReadByte(actor.Pointer + (int)OFFSET_ACTOR_HIDDEN_TO_AI_CACHE) != 0;
            flags.IsDying = Marshal.ReadByte(actor.Pointer + (int)OFFSET_ACTOR_IS_DYING) != 0;
            flags.IsLeavingMap = Marshal.ReadByte(actor.Pointer + (int)OFFSET_ACTOR_IS_LEAVING_MAP) != 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityState.GetStateFlags", "Failed", ex);
        }

        return flags;
    }
}
