using System;
using System.Collections.Generic;
using System.Reflection;

using Il2CppMenace.Tactical;

using Menace.SDK.Internal;

namespace Menace.SDK;

/// <summary>
/// Harmony hooks for TacticalManager events that fire Lua callbacks.
///
/// Hooks all InvokeOnX methods in TacticalManager to provide a comprehensive
/// event system for both C# plugins and Lua scripts.
///
/// C# Usage:
///   TacticalEventHooks.OnActorKilled += (actor, killer, faction) => { ... };
///   TacticalEventHooks.OnDamageReceived += (target, attacker, skill) => { ... };
///
/// Lua Usage:
///   on("actor_killed", function(data) log(data.actor .. " killed by " .. data.killer) end)
///   on("damage_received", function(data) log(data.target .. " took damage") end)
/// </summary>
public static class TacticalEventHooks
{
    private static bool _initialized;
    private const int OffsetActorFactionId = 0xBC;

    // ═══════════════════════════════════════════════════════════════════
    //  C# Events - Subscribe from plugins
    // ═══════════════════════════════════════════════════════════════════

    // Combat Events
    public static event Action<IntPtr, IntPtr, int> OnActorKilled;           // actor, killer, killerFaction
    public static event Action<IntPtr, IntPtr, IntPtr, IntPtr> OnDamageReceived;     // entity, attacker, skill, damageInfo
    public static event Action<IntPtr, IntPtr, IntPtr> OnAttackMissed;       // entity, attacker, skill
    public static event Action<IntPtr, IntPtr, IntPtr, float> OnAttackTileStart;            // attacker, skill, tile, attackDurationInSeconds
    public static event Action<IntPtr, int> OnBleedingOut;                   // leader, remainingRounds
    public static event Action<IntPtr, IntPtr> OnStabilized;                 // leader, savior
    public static event Action<IntPtr> OnSuppressed;                         // actor
    public static event Action<IntPtr, float, IntPtr> OnSuppressionApplied;  // actor, change, suppressor

    // Actor State Events
    public static event Action<IntPtr, int, int> OnActorStateChanged;        // actor, oldState, newState
    public static event Action<IntPtr, int> OnMoraleStateChanged;            // actor, moraleState
    public static event Action<IntPtr, float, int> OnHitpointsChanged;       // entity, hitpointsPct, animationDurationInMs
    public static event Action<IntPtr, float, int, int> OnArmorChanged;      // entity, armorDurability, armor, animationDurationInMs
    public static event Action<IntPtr, int, int> OnActionPointsChanged;      // actor, oldAp, newAp

    // Visibility Events
    public static event Action<IntPtr, IntPtr> OnDiscovered;                 // entity, discoverer
    public static event Action<IntPtr> OnVisibleToPlayer;                    // actor
    public static event Action<IntPtr> OnHiddenToPlayer;                     // actor

    // Movement Events
    public static event Action<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> OnMovementStarted; // actor, fromTile, toTile, movementAction, container
    public static event Action<IntPtr, IntPtr> OnMovementFinished;                   // actor, tile

    // Skill Events
    public static event Action<IntPtr, IntPtr, IntPtr> OnSkillUsed;          // actor, skill, targetTile
    public static event Action<IntPtr> OnSkillCompleted;                     // skill
    public static event Action<IntPtr, IntPtr, IntPtr, bool> OnSkillAdded;   // receiver, skill, source, success

    // Offmap Events
    public static event Action<IntPtr, IntPtr> OnOffmapAbilityUsed;          // ability, targetTile
    public static event Action<IntPtr> OnOffmapAbilityCanceled;              // ability
    public static event Action OnOffmapAbilityUpdateUsability;               // no args

    // Turn/Round Events
    public static event Action<IntPtr> OnActiveActorChanged;                 // actor
    public static event Action<IntPtr> OnTurnStart;                          // actor
    public static event Action<IntPtr> OnTurnEnd;                            // actor
    public static event Action OnPlayerTurn;                                 // no args
    public static event Action OnAITurn;                                     // no args
    public static event Action<int> OnRoundStart;                            // roundNumber
    public static event Action<int> OnRoundEnd;                              // roundNumber (fires before round increments)

    // Mission Events
    public static event Action<IntPtr, int, int> OnObjectiveStateChanged;    // objective, oldState, newState
    public static event Action<IntPtr> OnEntitySpawned;                      // entity
    public static event Action<IntPtr, IntPtr, IntPtr, IntPtr> OnElementDeath; // entity, element, attacker, damageInfo
    public static event Action<IntPtr, IntPtr> OnElementMalfunction;         // element, skill

    // ═══════════════════════════════════════════════════════════════════
    //  Initialization
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize tactical event hooks. Call from ModpackLoaderMod after game assembly is loaded.
    /// </summary>
    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized) return;

        try
        {
            var tacticalManager = typeof(Il2CppMenace.Tactical.TacticalManager);
            var hooks = typeof(TacticalEventHooks);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;

            int patchCount = 0;

            // Combat Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDeath", hooks.GetMethod(nameof(OnActorKilled_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDamageReceived", hooks.GetMethod(nameof(OnDamageReceived_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnAttackMissed", hooks.GetMethod(nameof(OnAttackMissed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnAttackTileStart", hooks.GetMethod(nameof(OnAttackTileStart_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnBleedingOut", hooks.GetMethod(nameof(OnBleedingOut_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnStabilized", hooks.GetMethod(nameof(OnStabilized_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSuppressed", hooks.GetMethod(nameof(OnSuppressed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSuppressionApplied", hooks.GetMethod(nameof(OnSuppressionApplied_Postfix), flags)) ? 1 : 0;

            // Actor State Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnActorStateChanged", hooks.GetMethod(nameof(OnActorStateChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnMoraleStateChanged", hooks.GetMethod(nameof(OnMoraleStateChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnHitpointsChanged", hooks.GetMethod(nameof(OnHitpointsChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnArmorChanged", hooks.GetMethod(nameof(OnArmorChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnActionPointsChanged", hooks.GetMethod(nameof(OnActionPointsChanged_Postfix), flags)) ? 1 : 0;

            // Visibility Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDiscovered", hooks.GetMethod(nameof(OnDiscovered_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnVisibleToPlayer", hooks.GetMethod(nameof(OnVisibleToPlayer_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnHiddenToPlayer", hooks.GetMethod(nameof(OnHiddenToPlayer_Postfix), flags)) ? 1 : 0;

            // Movement Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnMovement", hooks.GetMethod(nameof(OnMovementStarted_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnMovementFinished", hooks.GetMethod(nameof(OnMovementFinished_Postfix), flags)) ? 1 : 0;

            // Skill Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSkillUse", hooks.GetMethod(nameof(OnSkillUsed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnAfterSkillUse", hooks.GetMethod(nameof(OnSkillCompleted_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSkillAdded", hooks.GetMethod(nameof(OnSkillAdded_Postfix), flags)) ? 1 : 0;

            // Offmap Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnOffmapAbilityUsed", hooks.GetMethod(nameof(OnOffmapAbilityUsed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnOffmapAbilityCanceled", hooks.GetMethod(nameof(OnOffmapAbilityCanceled_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnOffmapAbilityRefreshUsability", hooks.GetMethod(nameof(OnOffmapAbilityUpdateUsability_Postfix), flags)) ? 1 : 0;

            // Turn/Round Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "SetActiveActor", hooks.GetMethod(nameof(OnActiveActorChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnTurnEnd", hooks.GetMethod(nameof(OnTurnEnd_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "NextRound", hooks.GetMethod(nameof(NextRound_Postfix), flags)) ? 1 : 0;

            // Mission Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnObjectiveStateChanged", hooks.GetMethod(nameof(OnObjectiveStateChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnEntitySpawned", hooks.GetMethod(nameof(OnEntitySpawned_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnElementDeath", hooks.GetMethod(nameof(OnElementDeath_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnElementMalfunction", hooks.GetMethod(nameof(OnElementMalfunction_Postfix), flags)) ? 1 : 0;

            // Prefix/Postfix - this is for ModpackLoader.cs - it tracks round ends. It probably shouldn't be wired this way.
            patchCount += GamePatch.Prefix(harmony, tacticalManager, "NextRound", hooks.GetMethod(nameof(OnNextRound_Prefix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "NextRound", hooks.GetMethod(nameof(OnNextRound_Postfix), flags)) ? 1 : 0;

            _initialized = true;
            SdkLogger.Msg($"[TacticalEventHooks] Initialized with {patchCount} event hooks");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TacticalEventHooks] Failed to initialize: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helper Methods
    // ═══════════════════════════════════════════════════════════════════

    private static string GetName(object obj)
    {
        if (obj == null) return "<null>";
        try
        {
            var gameObj = new GameObj(Il2CppUtils.GetPointer(obj));
            return gameObj.GetName() ?? "<unnamed>";
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("TacticalEventHooks", $"GetName failed for {obj.GetType().Name}: {ex.Message}");
            return "<unknown>";
        }
    }

    private static void FireLuaEvent(string eventName, Dictionary<string, object> data)
    {
        try
        {
            LuaScriptEngine.Instance?.FireEventWithTable(eventName, data);
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("TacticalEventHooks", $"Lua event '{eventName}' failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Harmony Postfix Patches
    // ═══════════════════════════════════════════════════════════════════

    // --- Combat Events ---

    private static void OnActorKilled_Postfix(object __instance, object _target, object _killer, int _killerFaction)
    {
        var targetPtr = Il2CppUtils.GetPointer(_target);
        var killerPtr = Il2CppUtils.GetPointer(_killer);

        OnActorKilled?.Invoke(targetPtr, killerPtr, _killerFaction);

        FireLuaEvent("actor_killed", new Dictionary<string, object>
        {
            ["actor"] = GetName(_target),
            ["actor_ptr"] = targetPtr.ToInt64(),
            ["killer"] = GetName(_killer),
            ["killer_ptr"] = killerPtr.ToInt64(),
            ["faction"] = _killerFaction
        });
    }

    private static void OnDamageReceived_Postfix(object __instance, object _entity, object _attacker, object _skill, object _damageInfo)
    {
        var targetPtr = Il2CppUtils.GetPointer(_entity);
        var attackerPtr = Il2CppUtils.GetPointer(_attacker);
        var skillPtr = Il2CppUtils.GetPointer(_skill);
        var damageInfoPtr = Il2CppUtils.GetPointer(_damageInfo);

        OnDamageReceived?.Invoke(targetPtr, attackerPtr, skillPtr, damageInfoPtr);

        FireLuaEvent("damage_received", new Dictionary<string, object>
        {
            ["target"] = GetName(_entity),
            ["target_ptr"] = targetPtr.ToInt64(),
            ["attacker"] = GetName(_attacker),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["skill"] = GetName(_skill),
            ["skill_ptr"] = skillPtr.ToInt64(),
            ["damage_info_ptr"] = damageInfoPtr.ToInt64()
        });
    }

    private static void OnAttackMissed_Postfix(object __instance, object _attacker, object _entity, object _skill)
    {
        var attackerPtr = Il2CppUtils.GetPointer(_attacker);
        var targetPtr = Il2CppUtils.GetPointer(_entity);
        var skillPtr = Il2CppUtils.GetPointer(_skill);

        OnAttackMissed?.Invoke(attackerPtr, targetPtr, skillPtr);

        FireLuaEvent("attack_missed", new Dictionary<string, object>
        {
            ["attacker"] = GetName(_attacker),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["target"] = GetName(_entity),
            ["target_ptr"] = targetPtr.ToInt64(),
            ["skill"] = GetName(_skill),
            ["skill_ptr"] = skillPtr.ToInt64()
        });
    }

    private static void OnAttackTileStart_Postfix(object __instance, object _actor, object _skill, object _targetTile, float _attackDurationInSec)
    {
        var attackerPtr = Il2CppUtils.GetPointer(_actor);
        var skillPtr = Il2CppUtils.GetPointer(_skill);
        var targetTilePtr = Il2CppUtils.GetPointer(_targetTile);

        OnAttackTileStart?.Invoke(attackerPtr, skillPtr, targetTilePtr, _attackDurationInSec);

        FireLuaEvent("attack_start", new Dictionary<string, object>
        {
            ["attacker"] = GetName(_actor),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["skill"] = GetName(_skill),
            ["skill_ptr"] = skillPtr.ToInt64(),
            ["tile_ptr"] = targetTilePtr.ToInt64(),
            ["attack_duration"] = _attackDurationInSec
        });
    }

    private static void OnBleedingOut_Postfix(object __instance, object _leader, int _remainingRounds)
    {
        var actorPtr = Il2CppUtils.GetPointer(_leader);

        OnBleedingOut?.Invoke(actorPtr, _remainingRounds);

        FireLuaEvent("bleeding_out", new Dictionary<string, object>
        {
            ["actor"] = GetName(_leader),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["remaining_rounds"] = _remainingRounds
        });
    }

    private static void OnStabilized_Postfix(object __instance, object _leader, object _savior)
    {
        var actorPtr = Il2CppUtils.GetPointer(_leader);
        var saviorPtr = Il2CppUtils.GetPointer(_savior);

        OnStabilized?.Invoke(actorPtr, saviorPtr);

        FireLuaEvent("stabilized", new Dictionary<string, object>
        {
            ["actor"] = GetName(_leader),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["savior"] = GetName(_savior),
            ["savior_ptr"] = saviorPtr.ToInt64()
        });
    }

    private static void OnSuppressed_Postfix(object __instance, object _actor)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);

        OnSuppressed?.Invoke(actorPtr);

        FireLuaEvent("suppressed", new Dictionary<string, object>
        {
            ["actor"] = GetName(_actor),
            ["actor_ptr"] = actorPtr.ToInt64()
        });
    }

    private static void OnSuppressionApplied_Postfix(object __instance, object _actor, float _change, object _suppressor)
    {
        var targetPtr = Il2CppUtils.GetPointer(_actor);
        var suppressorPtr = Il2CppUtils.GetPointer(_suppressor);

        OnSuppressionApplied?.Invoke(targetPtr, _change, suppressorPtr);

        FireLuaEvent("suppression_applied", new Dictionary<string, object>
        {
            ["target"] = GetName(_actor),
            ["target_ptr"] = targetPtr.ToInt64(),
            ["attacker"] = GetName(_suppressor),
            ["attacker_ptr"] = suppressorPtr.ToInt64(),
            ["amount"] = _change
        });
    }

    // --- Actor State Events ---

    private static void OnActorStateChanged_Postfix(object __instance, object _actor, int _oldState, int _newState)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);

        OnActorStateChanged?.Invoke(actorPtr, _oldState, _newState);

        FireLuaEvent("actor_state_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(_actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["old_state"] = _oldState,
            ["new_state"] = _newState
        });
    }

    private static void OnMoraleStateChanged_Postfix(object __instance, object _actor, int _moraleState)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);

        OnMoraleStateChanged?.Invoke(actorPtr, _moraleState);

        FireLuaEvent("morale_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(_actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["state"] = _moraleState
        });
    }

    private static void OnHitpointsChanged_Postfix(object __instance, object _entity, float _hitpointsPct, int _animationDurationInMs)
    {
        var entityPtr = Il2CppUtils.GetPointer(_entity);

        OnHitpointsChanged?.Invoke(entityPtr, _hitpointsPct, _animationDurationInMs);

        FireLuaEvent("hp_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(_entity),
            ["actor_ptr"] = entityPtr.ToInt64(),
            ["hitpoints_pct"] = _hitpointsPct,
            ["animation_duration_ms"] = _animationDurationInMs
        });
    }

    private static void OnArmorChanged_Postfix(object __instance, object _entity, float _armorDurability, int _armor, int _animationDurationInMs)
    {
        var entityPtr = Il2CppUtils.GetPointer(_entity);

        OnArmorChanged?.Invoke(entityPtr, _armorDurability, _armor, _animationDurationInMs);

        FireLuaEvent("armor_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(_entity),
            ["actor_ptr"] = entityPtr.ToInt64(),
            ["armor_durability"] = _armorDurability,
            ["armor"] = _armor,
            ["animation_duration_ms"] = _animationDurationInMs
        });
    }

    private static void OnActionPointsChanged_Postfix(object __instance, object _actor, int _oldAP, int _newAP)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);

        OnActionPointsChanged?.Invoke(actorPtr, _oldAP, _newAP);

        FireLuaEvent("ap_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(_actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["old_ap"] = _oldAP,
            ["new_ap"] = _newAP,
            ["delta"] = _newAP - _oldAP
        });
    }

    // --- Visibility Events ---

    private static void OnDiscovered_Postfix(object __instance, object _entity, object _discoverer)
    {
        var discoveredPtr = Il2CppUtils.GetPointer(_entity);
        var discovererPtr = Il2CppUtils.GetPointer(_discoverer);

        OnDiscovered?.Invoke(discoveredPtr, discovererPtr);

        FireLuaEvent("discovered", new Dictionary<string, object>
        {
            ["discovered"] = GetName(_entity),
            ["discovered_ptr"] = discoveredPtr.ToInt64(),
            ["discoverer"] = GetName(_discoverer),
            ["discoverer_ptr"] = discovererPtr.ToInt64()
        });
    }

    private static void OnVisibleToPlayer_Postfix(object __instance, object _actor)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);

        OnVisibleToPlayer?.Invoke(actorPtr);

        FireLuaEvent("visible_to_player", new Dictionary<string, object>
        {
            ["entity"] = GetName(_actor),
            ["entity_ptr"] = actorPtr.ToInt64()
        });
    }

    private static void OnHiddenToPlayer_Postfix(object __instance, object _actor)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);

        OnHiddenToPlayer?.Invoke(actorPtr);

        FireLuaEvent("hidden_from_player", new Dictionary<string, object>
        {
            ["entity"] = GetName(_actor),
            ["entity_ptr"] = actorPtr.ToInt64()
        });
    }

    // --- Movement Events ---

    private static void OnMovementStarted_Postfix(object __instance, object _actor, object _from, object _to, object _action, object _container)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);
        var fromPtr = Il2CppUtils.GetPointer(_from);
        var toPtr = Il2CppUtils.GetPointer(_to);
        var actionPtr = Il2CppUtils.GetPointer(_action);
        var containerPtr = Il2CppUtils.GetPointer(_container);

        OnMovementStarted?.Invoke(actorPtr, fromPtr, toPtr, actionPtr, containerPtr);

        FireLuaEvent("movement_started", new Dictionary<string, object>
        {
            ["actor"] = GetName(_actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["from_ptr"] = fromPtr.ToInt64(),
            ["to_ptr"] = toPtr.ToInt64(),
            ["action_ptr"] = actionPtr.ToInt64(),
            ["container_ptr"] = containerPtr.ToInt64()
        });
    }

    private static void OnMovementFinished_Postfix(object __instance, object _actor, object _to)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);
        var toPtr = Il2CppUtils.GetPointer(_to);

        OnMovementFinished?.Invoke(actorPtr, toPtr);

        FireLuaEvent("move_complete", new Dictionary<string, object>
        {
            ["actor"] = GetName(_actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["tile_ptr"] = toPtr.ToInt64()
        });
    }

    // --- Skill Events ---

    private static void OnSkillUsed_Postfix(object __instance, object _actor, object _skill, object _targetTile)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);
        var skillPtr = Il2CppUtils.GetPointer(_skill);
        var targetTilePtr = Il2CppUtils.GetPointer(_targetTile);

        OnSkillUsed?.Invoke(actorPtr, skillPtr, targetTilePtr);

        // Fire Lua event with Actor and Skill objects directly
        // Usage: on("skill_used", function(actor, skill)
        //            if skill.is_attack and not skill.is_silent then
        //                actor:add_effect("concealment", -3, 1)
        //            end
        //        end)
        try
        {
            LuaScriptEngine.Instance?.FireEventWithActorAndSkill("skill_used", actorPtr.ToInt64(), skillPtr.ToInt64());
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("TacticalEventHooks", $"skill_used event failed: {ex.Message}");
        }
    }

    private static void OnSkillCompleted_Postfix(object __instance, object _skill)
    {
        var skillPtr = Il2CppUtils.GetPointer(_skill);

        OnSkillCompleted?.Invoke(skillPtr);

        FireLuaEvent("skill_complete", new Dictionary<string, object>
        {
            ["skill"] = GetName(_skill),
            ["skill_ptr"] = skillPtr.ToInt64()
        });
    }

    private static void OnSkillAdded_Postfix(object __instance, object _receiver, object _skill, object _source, bool _success)
    {
        var receiverPtr = Il2CppUtils.GetPointer(_receiver);
        var skillPtr = Il2CppUtils.GetPointer(_skill);
        var sourcePtr = Il2CppUtils.GetPointer(_source);

        OnSkillAdded?.Invoke(receiverPtr, skillPtr, sourcePtr, _success);

        FireLuaEvent("skill_added", new Dictionary<string, object>
        {
            ["actor"] = GetName(_receiver),
            ["actor_ptr"] = receiverPtr.ToInt64(),
            ["skill"] = GetName(_skill),
            ["skill_ptr"] = skillPtr.ToInt64(),
            ["source"] = GetName(_source),
            ["source_ptr"] = sourcePtr.ToInt64(),
            ["success"] = _success
        });
    }

    private static void OnOffmapAbilityUsed_Postfix(object __instance, object _offmapAbility, object _targetTile)
    {
        var offmapAbilityPtr = Il2CppUtils.GetPointer(_offmapAbility);
        var targetTilePtr = Il2CppUtils.GetPointer(_targetTile);

        OnOffmapAbilityUsed?.Invoke(offmapAbilityPtr, targetTilePtr);

        FireLuaEvent("offmap_ability_used", new Dictionary<string, object>
        {
            ["ability"] = GetName(_offmapAbility),
            ["ability_ptr"] = offmapAbilityPtr.ToInt64(),
            ["tile_ptr"] = targetTilePtr.ToInt64()
        });
    }

    private static void OnOffmapAbilityCanceled_Postfix(object __instance, object _offmapAbility)
    {
        var offmapAbilityPtr = Il2CppUtils.GetPointer(_offmapAbility);

        OnOffmapAbilityCanceled?.Invoke(offmapAbilityPtr);

        FireLuaEvent("offmap_ability_canceled", new Dictionary<string, object>
        {
            ["ability"] = GetName(_offmapAbility),
            ["ability_ptr"] = offmapAbilityPtr.ToInt64()
        });
    }
    private static void OnOffmapAbilityUpdateUsability_Postfix(object __instance)
    {
        OnOffmapAbilityUpdateUsability?.Invoke();
    }

    // --- Turn/Round Events ---

    private static void OnActiveActorChanged_Postfix(object __instance, object _actor, bool _endTurn)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);
        if (actorPtr == IntPtr.Zero) return;

        OnActiveActorChanged?.Invoke(actorPtr);

        if (_endTurn)
        {
            OnTurnStart?.Invoke(actorPtr);

            var result = GameMethod.Call<TacticalManager>(__instance, x => x.IsPlayerTurn());
            if (result == null) return;

            if ((bool)result)
                OnPlayerTurn?.Invoke();
            else
                OnAITurn?.Invoke();
        }
    }

    private static void OnTurnEnd_Postfix(object __instance, object _actor)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);

        OnTurnEnd?.Invoke(actorPtr);

        if (actorPtr == IntPtr.Zero) return;

        // Get faction info
        int faction = 0;
        string factionName = "";
        try
        {
            var gameObj = new GameObj(actorPtr);
            faction = gameObj.ReadInt(OffsetActorFactionId);
            factionName = TacticalController.GetFactionName((FactionType)faction);
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("TacticalEventHooks", $"OnTurnEnd faction lookup failed: {ex.Message}");
        }

        // Fire existing turn_end event for compatibility
        LuaScriptEngine.Instance?.OnTurnEnd(faction, factionName);
    }

    private static void NextRound_Postfix(object __instance)
    {
        var round = GameMethod.CallInt<TacticalManager>(__instance, x => x.GetRound());

        OnRoundStart?.Invoke(round);

        FireLuaEvent("round_start", new Dictionary<string, object>
        {
            ["round"] = round
        });
    }

    // This is required by the SDK - ModpackLoaderMod.cs - do not touch
    private static void OnNextRound_Prefix(object __instance)
    {
        // Fire round_end BEFORE the round number increments
        int roundNumber = TacticalController.GetCurrentRound();

        OnRoundEnd?.Invoke(roundNumber);

        FireLuaEvent("round_end", new Dictionary<string, object>
        {
            ["round"] = roundNumber
        });
    }

    // This is required by the SDK - ModpackLoaderMod.cs - do not touch
    private static void OnNextRound_Postfix(object __instance)
    {
        // Fire round_start AFTER the round number has incremented
        int roundNumber = TacticalController.GetCurrentRound();

        OnRoundStart?.Invoke(roundNumber);

        FireLuaEvent("round_start", new Dictionary<string, object>
        {
            ["round"] = roundNumber
        });
    }

    // --- Entity Events ---

    private static void OnEntitySpawned_Postfix(object __instance, object _entity)
    {
        var entityPtr = Il2CppUtils.GetPointer(_entity);

        OnEntitySpawned?.Invoke(entityPtr);

        FireLuaEvent("entity_spawned", new Dictionary<string, object>
        {
            ["entity"] = GetName(_entity),
            ["entity_ptr"] = entityPtr.ToInt64()
        });
    }

    private static void OnElementDeath_Postfix(object __instance, object _entity, object _element, object _attacker, object _damageInfo)
    {
        var entityPtr = Il2CppUtils.GetPointer(_entity);
        var elementPtr = Il2CppUtils.GetPointer(_element);
        var attackerPtr = Il2CppUtils.GetPointer(_attacker);
        var damageInfoPtr = Il2CppUtils.GetPointer(_damageInfo);

        OnElementDeath?.Invoke(entityPtr, elementPtr, attackerPtr, damageInfoPtr);

        FireLuaEvent("element_destroyed", new Dictionary<string, object>
        {
            ["entity"] = GetName(_entity),
            ["entity_ptr"] = entityPtr.ToInt64(),
            ["element"] = GetName(_element),
            ["element_ptr"] = elementPtr.ToInt64(),
            ["attacker"] = GetName(_attacker),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["damage_info_ptr"] = damageInfoPtr.ToInt64()
        });
    }

    private static void OnElementMalfunction_Postfix(object __instance, object _element, object _skill)
    {
        var elementPtr = Il2CppUtils.GetPointer(_element);
        var skillPtr = Il2CppUtils.GetPointer(_skill);

        OnElementMalfunction?.Invoke(elementPtr, skillPtr);

        FireLuaEvent("element_malfunction", new Dictionary<string, object>
        {
            ["element"] = GetName(_element),
            ["element_ptr"] = elementPtr.ToInt64(),
            ["skill"] = GetName(_skill),
            ["skill_ptr"] = skillPtr.ToInt64()
        });
    }

    // --- Mission Events ---

    private static void OnObjectiveStateChanged_Postfix(object __instance, object _objective, int _oldState, int _newState)
    {
        var objectivePtr = Il2CppUtils.GetPointer(_objective);

        OnObjectiveStateChanged?.Invoke(objectivePtr, _oldState, _newState);

        FireLuaEvent("objective_changed", new Dictionary<string, object>
        {
            ["objective"] = GetName(_objective),
            ["objective_ptr"] = objectivePtr.ToInt64(),
            ["state"] = _newState
        });
    }
}