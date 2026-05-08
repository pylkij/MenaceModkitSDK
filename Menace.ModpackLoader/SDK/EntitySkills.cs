using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for entity skill manipulation and modification.
/// Provides comprehensive access to skill container operations, cooldown management,
/// skill parameter modification, and state queries.
///
/// Based on Ghidra reverse engineering findings:
/// - Skill.enabled @ +0x38 (bool)
/// - Skill.actionPointCost @ +0xA0 (int32)
/// - Skill.minRange @ +0xB4, maxRange @ +0xBC, optimalRange @ +0xB8
/// - Skill.eventHandlers @ +0x48 (List of effect handlers)
/// - CooldownEffectHandler.remainingCooldown @ handler+0x20 (int32)
/// - SkillContainer.Add() @ 0x1806e76e0
/// - SkillContainer.RemoveSkillByIndex() @ 0x1806edfb0
/// - Skill.SetRanges() @ 0x1806e1d80
/// - Skill.ChangeActionPointCost() @ 0x1806d8190
/// </summary>
public static class EntitySkills
{
    // Cached types
    private static GameType _actorType;
    private static GameType _skillType;
    private static GameType _skillContainerType;
    private static GameType _skillTemplateType;
    private static GameType _cooldownHandlerType;

    // Skill field offsets from Ghidra decompilation
    private const uint OFFSET_SKILL_ENABLED = 0x38;
    private const uint OFFSET_SKILL_EVENT_HANDLERS = 0x48;
    private const uint OFFSET_SKILL_AP_COST = 0xA0;
    private const uint OFFSET_SKILL_MIN_RANGE = 0xB4;
    private const uint OFFSET_SKILL_OPTIMAL_RANGE = 0xB8;
    private const uint OFFSET_SKILL_MAX_RANGE = 0xBC;

    // CooldownEffectHandler offsets
    private const uint OFFSET_COOLDOWN_HANDLER_REMAINING = 0x20;

    /// <summary>
    /// Skill state information structure.
    /// </summary>
    public class SkillStateInfo
    {
        public string SkillID { get; set; }
        public bool IsEnabled { get; set; }
        public int APCost { get; set; }
        public int MinRange { get; set; }
        public int OptimalRange { get; set; }
        public int MaxRange { get; set; }
        public int RemainingCooldown { get; set; }
        public bool IsUsable { get; set; }
        public string TemplateName { get; set; }
    }

    #region Skill Container Operations

    /// <summary>
    /// Add a skill to an actor's skill container by template ID.
    /// </summary>
    /// <param name="actor">The actor to add the skill to</param>
    /// <param name="skillTemplateID">The skill template identifier (e.g., "skill.overwatch")</param>
    /// <returns>True if the skill was successfully added</returns>
    /// <remarks>
    /// Uses SkillContainer.Add() method at 0x1806e76e0.
    /// The skill template must exist in the game's resources.
    /// </remarks>
    public static bool AddSkill(GameObj actor, string skillTemplateID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillTemplateID))
            return false;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return false;

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null) return false;

            // Get SkillContainer via GetSkills() method
            var getSkillsMethod = actorType.GetMethod("GetSkills",
                BindingFlags.Public | BindingFlags.Instance);
            if (getSkillsMethod == null) return false;

            var skillContainer = getSkillsMethod.Invoke(actorProxy, null);
            if (skillContainer == null) return false;

            // Find the skill template
            var template = FindSkillTemplate(skillTemplateID);
            if (template.IsNull) return false;

            var templateProxy = GetManagedProxy(template, _skillTemplateType?.ManagedType);
            if (templateProxy == null) return false;

            // Use SkillContainer.Add(SkillTemplate) method
            var containerType = _skillContainerType?.ManagedType;
            if (containerType == null) return false;

            var addMethod = containerType.GetMethod("Add",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { _skillTemplateType.ManagedType },
                null);

            if (addMethod == null) return false;

            addMethod.Invoke(skillContainer, new[] { templateProxy });
            ModError.Info("EntitySkills", $"Added skill '{skillTemplateID}' to {actor.GetName()}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.AddSkill", $"Failed to add {skillTemplateID}", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove a skill from an actor's skill container by skill ID.
    /// </summary>
    /// <param name="actor">The actor to remove the skill from</param>
    /// <param name="skillID">The skill identifier to remove</param>
    /// <returns>True if the skill was successfully removed</returns>
    /// <remarks>
    /// Uses SkillContainer.RemoveSkillByIndex() at 0x1806edfb0.
    /// Finds the skill index first, then removes it.
    /// </remarks>
    public static bool RemoveSkill(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return false;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return false;

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null) return false;

            // Get SkillContainer
            var getSkillsMethod = actorType.GetMethod("GetSkills",
                BindingFlags.Public | BindingFlags.Instance);
            if (getSkillsMethod == null) return false;

            var skillContainer = getSkillsMethod.Invoke(actorProxy, null);
            if (skillContainer == null) return false;

            var containerType = _skillContainerType?.ManagedType;
            if (containerType == null) return false;

            // Get all skills to find the index
            var getAllSkillsMethod = containerType.GetMethod("GetAllSkills",
                BindingFlags.Public | BindingFlags.Instance);
            if (getAllSkillsMethod == null) return false;

            var skillsList = getAllSkillsMethod.Invoke(skillContainer, null);
            if (skillsList == null) return false;

            // Find the skill index
            int skillIndex = -1;
            int currentIndex = 0;

            var enumerator = skillsList.GetType().GetMethod("GetEnumerator")?.Invoke(skillsList, null);
            if (enumerator == null) return false;

            var moveNext = enumerator.GetType().GetMethod("MoveNext");
            var current = enumerator.GetType().GetProperty("Current");

            while ((bool)moveNext.Invoke(enumerator, null))
            {
                var skill = current.GetValue(enumerator);
                if (skill != null)
                {
                    var getIdMethod = skill.GetType().GetMethod("GetID",
                        BindingFlags.Public | BindingFlags.Instance);
                    var id = Il2CppUtils.ToManagedString(getIdMethod?.Invoke(skill, null));

                    if (id == skillID)
                    {
                        skillIndex = currentIndex;
                        break;
                    }
                    currentIndex++;
                }
            }

            if (skillIndex < 0)
                return false; // Skill not found

            // Remove by index using RemoveSkillByIndex(int index)
            var removeMethod = containerType.GetMethod("RemoveSkillByIndex",
                BindingFlags.Public | BindingFlags.Instance);

            if (removeMethod == null) return false;

            removeMethod.Invoke(skillContainer, new object[] { skillIndex });
            ModError.Info("EntitySkills", $"Removed skill '{skillID}' from {actor.GetName()}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.RemoveSkill", $"Failed to remove {skillID}", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if an actor has a specific skill.
    /// </summary>
    /// <param name="actor">The actor to check</param>
    /// <param name="skillID">The skill identifier to look for</param>
    /// <returns>True if the actor has the skill</returns>
    public static bool HasSkill(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return false;

        var skill = GetSkillByID(actor, skillID);
        return !skill.IsNull;
    }

    /// <summary>
    /// Get all skill IDs for an actor.
    /// </summary>
    /// <param name="actor">The actor to query</param>
    /// <returns>List of skill IDs</returns>
    public static List<string> GetSkillIDs(GameObj actor)
    {
        var result = new List<string>();

        if (actor.IsNull)
            return result;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return result;

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null) return result;

            // Get SkillContainer
            var getSkillsMethod = actorType.GetMethod("GetSkills",
                BindingFlags.Public | BindingFlags.Instance);
            if (getSkillsMethod == null) return result;

            var skillContainer = getSkillsMethod.Invoke(actorProxy, null);
            if (skillContainer == null) return result;

            // Get all skills
            var getAllSkillsMethod = skillContainer.GetType().GetMethod("GetAllSkills",
                BindingFlags.Public | BindingFlags.Instance);
            if (getAllSkillsMethod == null) return result;

            var skillsList = getAllSkillsMethod.Invoke(skillContainer, null);
            if (skillsList == null) return result;

            var enumerator = skillsList.GetType().GetMethod("GetEnumerator")?.Invoke(skillsList, null);
            if (enumerator == null) return result;

            var moveNext = enumerator.GetType().GetMethod("MoveNext");
            var current = enumerator.GetType().GetProperty("Current");

            while ((bool)moveNext.Invoke(enumerator, null))
            {
                var skill = current.GetValue(enumerator);
                if (skill != null)
                {
                    var getIdMethod = skill.GetType().GetMethod("GetID",
                        BindingFlags.Public | BindingFlags.Instance);
                    var id = Il2CppUtils.ToManagedString(getIdMethod?.Invoke(skill, null));

                    if (!string.IsNullOrEmpty(id))
                        result.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.GetSkillIDs", "Failed", ex);
        }

        return result;
    }

    #endregion

    #region Cooldown Management

    /// <summary>
    /// Set the cooldown for a specific skill.
    /// </summary>
    /// <param name="actor">The actor whose skill cooldown to set</param>
    /// <param name="skillID">The skill identifier</param>
    /// <param name="turns">Number of turns for the cooldown</param>
    /// <returns>True if the cooldown was successfully set</returns>
    /// <remarks>
    /// Cooldown is stored in CooldownEffectHandler.remainingCooldown at handler+0x20.
    /// Searches the skill's eventHandlers list at +0x48 to find the cooldown handler.
    /// </remarks>
    public static bool SetCooldown(GameObj actor, string skillID, int turns)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return false;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return false;

            // Access eventHandlers list at skill+0x48
            var handlersPtr = skill.ReadPtr(OFFSET_SKILL_EVENT_HANDLERS);
            if (handlersPtr == IntPtr.Zero) return false;

            var handlersList = new GameList(handlersPtr);
            if (handlersList.Count == 0) return false;

            // Find CooldownEffectHandler in the list
            for (int i = 0; i < handlersList.Count; i++)
            {
                var handler = handlersList[i];
                if (handler.IsNull) continue;

                // Check if this is a CooldownEffectHandler by type name
                var typeName = handler.GetTypeName();
                if (typeName != null && typeName.Contains("CooldownEffectHandler"))
                {
                    // Write to remainingCooldown at handler+0x20
                    Marshal.WriteInt32(handler.Pointer + (int)OFFSET_COOLDOWN_HANDLER_REMAINING, turns);
                    return true;
                }
            }

            // If no cooldown handler exists, skill doesn't support cooldowns
            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.SetCooldown", $"Failed for {skillID}", ex);
            return false;
        }
    }

    /// <summary>
    /// Reset the cooldown for a specific skill (set to 0).
    /// </summary>
    /// <param name="actor">The actor whose skill cooldown to reset</param>
    /// <param name="skillID">The skill identifier</param>
    /// <returns>True if the cooldown was successfully reset</returns>
    public static bool ResetCooldown(GameObj actor, string skillID)
    {
        return SetCooldown(actor, skillID, 0);
    }

    /// <summary>
    /// Modify the cooldown for a specific skill by a delta amount.
    /// </summary>
    /// <param name="actor">The actor whose skill cooldown to modify</param>
    /// <param name="skillID">The skill identifier</param>
    /// <param name="delta">Amount to add to current cooldown (can be negative)</param>
    /// <returns>True if the cooldown was successfully modified</returns>
    public static bool ModifyCooldown(GameObj actor, string skillID, int delta)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return false;

        var currentCooldown = GetRemainingCooldown(actor, skillID);
        var newCooldown = Math.Max(0, currentCooldown + delta);
        return SetCooldown(actor, skillID, newCooldown);
    }

    /// <summary>
    /// Get the remaining cooldown turns for a specific skill.
    /// </summary>
    /// <param name="actor">The actor to query</param>
    /// <param name="skillID">The skill identifier</param>
    /// <returns>Remaining cooldown turns, or 0 if skill has no cooldown or is ready</returns>
    /// <remarks>
    /// Reads from CooldownEffectHandler.remainingCooldown at handler+0x20.
    /// </remarks>
    public static int GetRemainingCooldown(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return 0;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return 0;

            // Access eventHandlers list at skill+0x48
            var handlersPtr = skill.ReadPtr(OFFSET_SKILL_EVENT_HANDLERS);
            if (handlersPtr == IntPtr.Zero) return 0;

            var handlersList = new GameList(handlersPtr);
            if (handlersList.Count == 0) return 0;

            // Find CooldownEffectHandler in the list
            for (int i = 0; i < handlersList.Count; i++)
            {
                var handler = handlersList[i];
                if (handler.IsNull) continue;

                // Check if this is a CooldownEffectHandler
                var typeName = handler.GetTypeName();
                if (typeName != null && typeName.Contains("CooldownEffectHandler"))
                {
                    // Read remainingCooldown at handler+0x20
                    return Marshal.ReadInt32(handler.Pointer + (int)OFFSET_COOLDOWN_HANDLER_REMAINING);
                }
            }

            return 0; // No cooldown handler found
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.GetRemainingCooldown", $"Failed for {skillID}", ex);
            return 0;
        }
    }

    #endregion

    #region Skill Parameter Modification

    /// <summary>
    /// Modify the range parameters for a specific skill.
    /// </summary>
    /// <param name="actor">The actor whose skill to modify</param>
    /// <param name="skillID">The skill identifier</param>
    /// <param name="newRange">New maximum range value</param>
    /// <returns>True if the range was successfully modified</returns>
    /// <remarks>
    /// Can use Skill.SetRanges() method at 0x1806e1d80 or direct field write.
    /// This implementation uses direct field writes at +0xB4 (min), +0xB8 (optimal), +0xBC (max).
    /// </remarks>
    public static bool ModifySkillRange(GameObj actor, string skillID, int newRange)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID) || newRange < 0)
            return false;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return false;

            // Try using SetRanges() method first
            EnsureTypesLoaded();
            var skillType = _skillType?.ManagedType;
            if (skillType != null)
            {
                var skillProxy = GetManagedProxy(skill, skillType);
                if (skillProxy != null)
                {
                    // SetRanges(int min, int optimal, int max)
                    var setRangesMethod = skillType.GetMethod("SetRanges",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (setRangesMethod != null)
                    {
                        // Set min=0, optimal=newRange, max=newRange
                        setRangesMethod.Invoke(skillProxy, new object[] { 0, newRange, newRange });
                        return true;
                    }
                }
            }

            // Fallback: direct field writes
            Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_MIN_RANGE, 0);
            Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_OPTIMAL_RANGE, newRange);
            Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_MAX_RANGE, newRange);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.ModifySkillRange", $"Failed for {skillID}", ex);
            return false;
        }
    }

    /// <summary>
    /// Modify the Action Point cost for a specific skill.
    /// </summary>
    /// <param name="actor">The actor whose skill to modify</param>
    /// <param name="skillID">The skill identifier</param>
    /// <param name="newCost">New AP cost value</param>
    /// <returns>True if the AP cost was successfully modified</returns>
    /// <remarks>
    /// Can use Skill.ChangeActionPointCost() method at 0x1806d8190 or direct write at +0xA0.
    /// This implementation tries the method first, then falls back to direct write.
    /// Based on Ghidra decompilation of actionPointCost field at +0xA0.
    /// </remarks>
    public static bool ModifySkillAPCost(GameObj actor, string skillID, int newCost)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID) || newCost < 0)
            return false;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return false;

            // Try using ChangeActionPointCost() method first
            EnsureTypesLoaded();
            var skillType = _skillType?.ManagedType;
            if (skillType != null)
            {
                var skillProxy = GetManagedProxy(skill, skillType);
                if (skillProxy != null)
                {
                    var changeAPMethod = skillType.GetMethod("ChangeActionPointCost",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (changeAPMethod != null)
                    {
                        changeAPMethod.Invoke(skillProxy, new object[] { newCost });
                        return true;
                    }
                }
            }

            // Fallback: direct field write at +0xA0
            Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_AP_COST, newCost);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.ModifySkillAPCost", $"Failed for {skillID}", ex);
            return false;
        }
    }

    /// <summary>
    /// Enable a specific skill.
    /// </summary>
    /// <param name="actor">The actor whose skill to enable</param>
    /// <param name="skillID">The skill identifier</param>
    /// <returns>True if the skill was successfully enabled</returns>
    /// <remarks>
    /// Writes true to Skill.enabled at +0x38 based on Ghidra decompilation.
    /// </remarks>
    public static bool EnableSkill(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return false;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return false;

            // Write true to enabled field at +0x38
            Marshal.WriteByte(skill.Pointer + (int)OFFSET_SKILL_ENABLED, 1);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.EnableSkill", $"Failed for {skillID}", ex);
            return false;
        }
    }

    /// <summary>
    /// Disable a specific skill.
    /// </summary>
    /// <param name="actor">The actor whose skill to disable</param>
    /// <param name="skillID">The skill identifier</param>
    /// <returns>True if the skill was successfully disabled</returns>
    /// <remarks>
    /// Writes false to Skill.enabled at +0x38 based on Ghidra decompilation.
    /// </remarks>
    public static bool DisableSkill(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return false;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return false;

            // Write false to enabled field at +0x38
            Marshal.WriteByte(skill.Pointer + (int)OFFSET_SKILL_ENABLED, 0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.DisableSkill", $"Failed for {skillID}", ex);
            return false;
        }
    }

    #endregion

    #region Skill State Queries

    /// <summary>
    /// Get comprehensive state information for a specific skill.
    /// </summary>
    /// <param name="actor">The actor to query</param>
    /// <param name="skillID">The skill identifier</param>
    /// <returns>Skill state information, or null if skill not found</returns>
    /// <remarks>
    /// Returns a structure containing enabled state, AP cost, range, cooldown, and usability.
    /// </remarks>
    public static SkillStateInfo GetSkillState(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return null;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return null;

            var info = new SkillStateInfo
            {
                SkillID = skillID
            };

            // Read enabled state at +0x38
            info.IsEnabled = Marshal.ReadByte(skill.Pointer + (int)OFFSET_SKILL_ENABLED) != 0;

            // Read AP cost at +0xA0
            info.APCost = Marshal.ReadInt32(skill.Pointer + (int)OFFSET_SKILL_AP_COST);

            // Read range values
            info.MinRange = Marshal.ReadInt32(skill.Pointer + (int)OFFSET_SKILL_MIN_RANGE);
            info.OptimalRange = Marshal.ReadInt32(skill.Pointer + (int)OFFSET_SKILL_OPTIMAL_RANGE);
            info.MaxRange = Marshal.ReadInt32(skill.Pointer + (int)OFFSET_SKILL_MAX_RANGE);

            // Get remaining cooldown
            info.RemainingCooldown = GetRemainingCooldown(actor, skillID);

            // Check if skill is usable via reflection
            EnsureTypesLoaded();
            var skillType = _skillType?.ManagedType;
            if (skillType != null)
            {
                var skillProxy = GetManagedProxy(skill, skillType);
                if (skillProxy != null)
                {
                    var isUsableMethod = skillType.GetMethod("IsUsable",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        Type.EmptyTypes,
                        null);

                    if (isUsableMethod != null)
                    {
                        info.IsUsable = (bool)isUsableMethod.Invoke(skillProxy, null);
                    }

                    // Get template name
                    var getTemplateMethod = skillType.GetMethod("GetTemplate",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (getTemplateMethod != null)
                    {
                        var template = getTemplateMethod.Invoke(skillProxy, null);
                        if (template != null)
                        {
                            var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                            info.TemplateName = templateObj.GetName();
                        }
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.GetSkillState", $"Failed for {skillID}", ex);
            return null;
        }
    }

    /// <summary>
    /// Reset all modifications to a skill, restoring template defaults.
    /// </summary>
    /// <param name="actor">The actor whose skill to reset</param>
    /// <param name="skillID">The skill identifier</param>
    /// <returns>True if the skill was successfully reset</returns>
    /// <remarks>
    /// Reads values from the skill's template and applies them to the skill instance.
    /// This restores AP cost, ranges, and cooldown to their default values.
    /// </remarks>
    public static bool ResetSkillModifications(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return false;

        try
        {
            var skill = GetSkillByID(actor, skillID);
            if (skill.IsNull) return false;

            EnsureTypesLoaded();
            var skillType = _skillType?.ManagedType;
            if (skillType == null) return false;

            var skillProxy = GetManagedProxy(skill, skillType);
            if (skillProxy == null) return false;

            // Get the skill template
            var getTemplateMethod = skillType.GetMethod("GetTemplate",
                BindingFlags.Public | BindingFlags.Instance);
            if (getTemplateMethod == null) return false;

            var template = getTemplateMethod.Invoke(skillProxy, null);
            if (template == null) return false;

            var templateType = template.GetType();

            // Reset AP cost
            var apCostProp = templateType.GetProperty("ActionPointCost",
                BindingFlags.Public | BindingFlags.Instance);
            if (apCostProp != null)
            {
                var defaultAPCost = (int)apCostProp.GetValue(template);
                Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_AP_COST, defaultAPCost);
            }

            // Reset ranges
            var minRangeProp = templateType.GetProperty("MinRange",
                BindingFlags.Public | BindingFlags.Instance);
            var optimalRangeProp = templateType.GetProperty("OptimalRange",
                BindingFlags.Public | BindingFlags.Instance);
            var maxRangeProp = templateType.GetProperty("MaxRange",
                BindingFlags.Public | BindingFlags.Instance);

            if (minRangeProp != null && optimalRangeProp != null && maxRangeProp != null)
            {
                var minRange = (int)minRangeProp.GetValue(template);
                var optimalRange = (int)optimalRangeProp.GetValue(template);
                var maxRange = (int)maxRangeProp.GetValue(template);

                Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_MIN_RANGE, minRange);
                Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_OPTIMAL_RANGE, optimalRange);
                Marshal.WriteInt32(skill.Pointer + (int)OFFSET_SKILL_MAX_RANGE, maxRange);
            }

            // Reset cooldown to 0 (ready to use)
            ResetCooldown(actor, skillID);

            // Ensure skill is enabled
            Marshal.WriteByte(skill.Pointer + (int)OFFSET_SKILL_ENABLED, 1);

            ModError.Info("EntitySkills", $"Reset skill '{skillID}' to template defaults");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.ResetSkillModifications", $"Failed for {skillID}", ex);
            return false;
        }
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Get a skill object by ID for a given actor.
    /// </summary>
    private static GameObj GetSkillByID(GameObj actor, string skillID)
    {
        if (actor.IsNull || string.IsNullOrEmpty(skillID))
            return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return GameObj.Null;

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null) return GameObj.Null;

            // Get SkillContainer
            var getSkillsMethod = actorType.GetMethod("GetSkills",
                BindingFlags.Public | BindingFlags.Instance);
            if (getSkillsMethod == null) return GameObj.Null;

            var skillContainer = getSkillsMethod.Invoke(actorProxy, null);
            if (skillContainer == null) return GameObj.Null;

            // Try GetSkillByID method first
            // GetSkillByID has multiple overloads - specify parameter types to avoid AmbiguousMatchException
            var getSkillByIdMethods = skillContainer.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "GetSkillByID" && m.GetParameters().Length == 2);

            foreach (var method in getSkillByIdMethods)
            {
                try
                {
                    var skill = method.Invoke(skillContainer, new object[] { skillID, null });
                    if (skill != null)
                        return new GameObj(((Il2CppObjectBase)skill).Pointer);
                }
                catch
                {
                    // Try next overload
                    continue;
                }
            }

            // Fallback: iterate through all skills
            var getAllSkillsMethod = skillContainer.GetType().GetMethod("GetAllSkills",
                BindingFlags.Public | BindingFlags.Instance);
            if (getAllSkillsMethod == null) return GameObj.Null;

            var skillsList = getAllSkillsMethod.Invoke(skillContainer, null);
            if (skillsList == null) return GameObj.Null;

            var enumerator = skillsList.GetType().GetMethod("GetEnumerator")?.Invoke(skillsList, null);
            if (enumerator == null) return GameObj.Null;

            var moveNext = enumerator.GetType().GetMethod("MoveNext");
            var current = enumerator.GetType().GetProperty("Current");

            while ((bool)moveNext.Invoke(enumerator, null))
            {
                var skill = current.GetValue(enumerator);
                if (skill != null)
                {
                    var getIdMethod = skill.GetType().GetMethod("GetID",
                        BindingFlags.Public | BindingFlags.Instance);
                    var id = Il2CppUtils.ToManagedString(getIdMethod?.Invoke(skill, null));

                    if (id == skillID)
                        return new GameObj(((Il2CppObjectBase)skill).Pointer);
                }
            }

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySkills.GetSkillByID", $"Failed for {skillID}", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Find a skill template by name in game resources.
    /// </summary>
    private static GameObj FindSkillTemplate(string templateName)
    {
        try
        {
            EnsureTypesLoaded();

            var templateType = _skillTemplateType?.ManagedType;
            if (templateType == null) return GameObj.Null;

            var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(templateType);
            var templates = UnityEngine.Resources.FindObjectsOfTypeAll(il2cppType);

            if (templates != null)
            {
                foreach (var template in templates)
                {
                    if (template != null && template.name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new GameObj(((Il2CppObjectBase)template).Pointer);
                    }
                }
            }

            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Ensure all required IL2CPP types are loaded and cached.
    /// </summary>
    private static void EnsureTypesLoaded()
    {
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
        _skillType ??= GameType.Find("Menace.Tactical.Skills.BaseSkill");
        _skillContainerType ??= GameType.Find("Menace.Tactical.Skills.SkillContainer");
        _skillTemplateType ??= GameType.Find("Menace.Tactical.Skills.SkillTemplate");
        _cooldownHandlerType ??= GameType.Find("Menace.Tactical.Skills.CooldownEffectHandler");
    }

    /// <summary>
    /// Create a managed IL2CPP proxy object from a GameObj and managed type.
    /// </summary>
    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);

    #endregion
}
