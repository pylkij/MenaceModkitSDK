using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;

using Il2CppMenace.Strategy;

namespace Menace.SDK;

/// <summary>
/// Harmony hooks for strategy layer events that fire Lua callbacks.
///
/// Hooks various strategy classes (Roster, StoryFaction, Squaddies, Operation, etc.)
/// to provide a comprehensive event system for both C# plugins and Lua scripts.
///
/// C# Usage:
///   StrategyEventHooks.OnLeaderHired += (leader) => { ... };
///   StrategyEventHooks.OnFactionTrustChanged += (faction, delta) => { ... };
///
/// Lua Usage:
///   on("leader_hired", function(data) log(data.leader .. " joined the roster") end)
///   on("faction_trust_changed", function(data) log(data.faction .. " trust: " .. data.delta) end)
/// </summary>
public static class StrategyEventHooks
{
    private static bool _initialized;

    // ═══════════════════════════════════════════════════════════════════
    //  C# Events - Subscribe from plugins
    // ═══════════════════════════════════════════════════════════════════

    // Roster Events
    public static event Action<IntPtr> OnLeaderHired;                    // leader
    public static event Action<IntPtr> OnLeaderDismissed;                // leader
    public static event Action<IntPtr> OnLeaderPermadeath;               // leader
    public static event Action<IntPtr, IntPtr> OnLeaderLevelUp;          // leader, perk

    // Faction Events
    public static event Action<IntPtr, int> OnFactionTrustChanged;       // faction, delta
    public static event Action<IntPtr, int> OnFactionStatusChanged;      // faction, newStatus
    public static event Action<IntPtr, IntPtr> OnFactionUpgradeUnlocked; // faction, upgrade

    // Squaddie Events
    public static event Action<int> OnSquaddieKilled;                    // squaddieId
    //public static event Action<int> OnSquaddieAdded;                     // count

    // Operation/Mission Events
    public static event Action<IntPtr> OnOperationStarted;               // operation
    public static event Action<IntPtr> OnOperationFinished;              // operation
    public static event Action<IntPtr, IntPtr> OnMissionStarted;         // operation, mission
    public static event Action<IntPtr, IntPtr, IntPtr> OnMissionFinished; // operation, mission, missionResult

    // Black Market Events
    public static event Action<IntPtr> OnBlackMarketItemAdded;           // item
    public static event Action OnBlackMarketRestocked;

    // Emotional State Events
    public static event Action<IntPtr, IntPtr, IntPtr, IntPtr> OnTriggerEmotion; // trigger, target, random, mission

    // ═══════════════════════════════════════════════════════════════════
    //  Initialization
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize strategy event hooks. Call from ModpackLoaderMod after game assembly is loaded.
    /// </summary>
    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized) return;

        try
        {
            // Cache types
            var rosterType = typeof(Il2CppMenace.Strategy.Roster);
            var storyFactionType = typeof(Il2CppMenace.Strategy.StoryFaction);
            var squaddiesType = typeof(Il2CppMenace.Strategy.Squaddies);
            //var baseGameEffect = typeof(Il2CppMenace.Strategy.BaseGameEffect); // BaseGameEffect causes a CRASH. DO NOT USE
            var baseUnitLeaderType = typeof(Il2CppMenace.Strategy.BaseUnitLeader);
            var blackMarketType = typeof(Il2CppMenace.Strategy.BlackMarket);
            var emotionalStatesType = typeof(Il2CppMenace.Strategy.EmotionalStates);

            var hooks = typeof(StrategyEventHooks);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;

            int patchCount = 0;

            // Roster patches
            patchCount += GamePatch.Postfix(harmony, rosterType, "HireLeader", hooks.GetMethod(nameof(HireLeader_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, rosterType, "TryDismissLeader", hooks.GetMethod(nameof(DismissLeader_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, rosterType, "OnPermanentDeath", hooks.GetMethod(nameof(OnPermanentDeath_Postfix), flags)) ? 1 : 0;

            // Leader patches
            patchCount += GamePatch.Postfix(harmony, baseUnitLeaderType, "AddPerk", hooks.GetMethod(nameof(AddPerk_Postfix), flags)) ? 1 : 0;

            // Faction patches
            patchCount += GamePatch.Postfix(harmony, storyFactionType, "ChangeTrust", hooks.GetMethod(nameof(ChangeTrust_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, storyFactionType, "SetStatus", hooks.GetMethod(nameof(SetStatus_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, storyFactionType, "UnlockUpgrade", hooks.GetMethod(nameof(UnlockUpgrade_Postfix), flags)) ? 1 : 0;

            // Squaddie patches
            patchCount += GamePatch.Postfix(harmony, squaddiesType, "Kill", hooks.GetMethod(nameof(SquaddieKill_Postfix), flags)) ? 1 : 0;
            //patchCount += GamePatch.Postfix(harmony, squaddiesType, "AddAlive", hooks.GetMethod(nameof(SquaddieAddAlive_Postfix), flags)) ? 1 : 0;

            // Operation patches
            //patchCount += GamePatch.Postfix(harmony, baseGameEffect, "OnOperationStarted", hooks.GetMethod(nameof(OnOperationStarted_Postfix), flags)) ? 1 : 0;
            //patchCount += GamePatch.Postfix(harmony, baseGameEffect, "OnOperationFinished", hooks.GetMethod(nameof(OnOperationFinished_Postfix), flags)) ? 1 : 0;
            //patchCount += GamePatch.Postfix(harmony, baseGameEffect, "OnMissionStarted", hooks.GetMethod(nameof(OnMissionStarted_Postfix), flags)) ? 1 : 0;
            //patchCount += GamePatch.Postfix(harmony, baseGameEffect, "OnMissionFinished", hooks.GetMethod(nameof(OnMissionFinished_Postfix), flags)) ? 1 : 0;

            // BlackMarket patches
            patchCount += GamePatch.Postfix(harmony, blackMarketType, "AddItem", hooks.GetMethod(nameof(BlackMarketAddItem_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, blackMarketType, "FillUp", hooks.GetMethod(nameof(BlackMarketFillUp_Postfix), flags)) ? 1 : 0;

            // EmotionalStates patches
            patchCount += GamePatch.Postfix(harmony, emotionalStatesType, "TriggerEmotion", hooks.GetMethod(nameof(TriggerEmotion_Postfix), flags)) ? 1 : 0;

            _initialized = true;
            SdkLogger.Msg($"[StrategyEventHooks] Initialized with {patchCount} event hooks");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[StrategyEventHooks] Failed to initialize: {ex.Message}");
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
            var ptr = Il2CppUtils.GetPointer(obj);
            if (ptr == IntPtr.Zero) return "<null>";

            // Try to get template ID or name
            var gameObj = new GameObj(ptr);

            // For leaders, try to get nickname
            var nicknameMethod = obj.GetType().GetMethod("GetNickname",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (nicknameMethod != null)
            {
                var result = nicknameMethod.Invoke(obj, null);
                if (result != null) return result.ToString();
            }

            // Try GetID for templates
            var idMethod = obj.GetType().GetMethod("GetID",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (idMethod != null)
            {
                var result = idMethod.Invoke(obj, null);
                if (result != null) return result.ToString();
            }

            return gameObj.GetName() ?? "<unnamed>";
        }
        catch
        {
            return "<unknown>";
        }
    }

    private static string GetFactionName(object faction)
    {
        if (faction == null) return "<null>";
        try
        {
            // Try to get the template and its name
            var templateProp = faction.GetType().GetProperty("Template",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (templateProp != null)
            {
                var template = templateProp.GetValue(faction);
                if (template != null)
                {
                    return GetName(template);
                }
            }
            return GetName(faction);
        }
        catch
        {
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
            ModError.WarnInternal("StrategyEventHooks", $"Lua event '{eventName}' failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Harmony Postfix Patches
    // ═══════════════════════════════════════════════════════════════════

    // --- Roster Events ---

    private static void HireLeader_Postfix(object __instance, object __result, object _leaderTemplate)
    {
        if (__result == null) return; // Hire failed

        var leaderPtr = Il2CppUtils.GetPointer(_leaderTemplate);
        OnLeaderHired?.Invoke(leaderPtr);

        FireLuaEvent("leader_hired", new Dictionary<string, object>
        {
            ["leader"] = GetName(__result),
            ["leader_ptr"] = leaderPtr.ToInt64(),
            ["template"] = GetName(_leaderTemplate)
        });
    }

    private static void DismissLeader_Postfix(object __instance, bool __result, object _leader)
    {
        if (!__result) return; // Dismiss failed

        var leaderPtr = Il2CppUtils.GetPointer(_leader);
        OnLeaderDismissed?.Invoke(leaderPtr);

        FireLuaEvent("leader_dismissed", new Dictionary<string, object>
        {
            ["leader"] = GetName(_leader),
            ["leader_ptr"] = leaderPtr.ToInt64()
        });
    }

    private static void OnPermanentDeath_Postfix(object __instance, object _leader)
    {
        var leaderPtr = Il2CppUtils.GetPointer(_leader);
        OnLeaderPermadeath?.Invoke(leaderPtr);

        FireLuaEvent("leader_permadeath", new Dictionary<string, object>
        {
            ["leader"] = GetName(_leader),
            ["leader_ptr"] = leaderPtr.ToInt64()
        });
    }

    private static void AddPerk_Postfix(object __instance, object _perk)
    {
        var leaderPtr = Il2CppUtils.GetPointer(__instance);
        var perkPtr = Il2CppUtils.GetPointer(_perk);

        OnLeaderLevelUp?.Invoke(leaderPtr, perkPtr);

        FireLuaEvent("leader_levelup", new Dictionary<string, object>
        {
            ["leader"] = GetName(__instance),
            ["leader_ptr"] = leaderPtr.ToInt64(),
            ["perk"] = GetName(_perk)
        });
    }

    // --- Faction Events ---

    private static void ChangeTrust_Postfix(object __instance, int _change)
    {
        if (_change == 0) return;

        var factionPtr = Il2CppUtils.GetPointer(__instance);
        OnFactionTrustChanged?.Invoke(factionPtr, _change);

        FireLuaEvent("faction_trust_changed", new Dictionary<string, object>
        {
            ["faction"] = GetFactionName(__instance),
            ["faction_ptr"] = factionPtr.ToInt64(),
            ["delta"] = _change
        });
    }

    private static void SetStatus_Postfix(object __instance, int _status)
    {
        var factionPtr = Il2CppUtils.GetPointer(__instance);
        OnFactionStatusChanged?.Invoke(factionPtr, _status);

        FireLuaEvent("faction_status_changed", new Dictionary<string, object>
        {
            ["faction"] = GetFactionName(__instance),
            ["faction_ptr"] = factionPtr.ToInt64(),
            ["status"] = _status
        });
    }

    private static void UnlockUpgrade_Postfix(object __instance, object _upgrade)
    {
        var factionPtr = Il2CppUtils.GetPointer(__instance);
        var upgradePtr = Il2CppUtils.GetPointer(_upgrade);

        OnFactionUpgradeUnlocked?.Invoke(factionPtr, upgradePtr);

        FireLuaEvent("faction_upgrade_unlocked", new Dictionary<string, object>
        {
            ["faction"] = GetFactionName(__instance),
            ["faction_ptr"] = factionPtr.ToInt64(),
            ["upgrade"] = GetName(_upgrade),
            ["upgrade_ptr"] = upgradePtr.ToInt64()
        });
    }

    // --- Squaddie Events ---

    private static void SquaddieKill_Postfix(object __instance, bool __result, int _squaddieId)
    {
        if (!__result) return; // Kill failed

        OnSquaddieKilled?.Invoke(_squaddieId);

        FireLuaEvent("squaddie_killed", new Dictionary<string, object>
        {
            ["squaddie_id"] = _squaddieId
        });
    }

    /*
    private static void SquaddieAddAlive_Postfix(object __instance, HomePlanetType _homePlanet, object _gender, object _skinColor, string _name, string _nickname)
    {
        int count = GameMethod.CallInt<Squaddies>(__instance, x => x.GetAliveCount());

        OnSquaddieAdded?.Invoke(count);

        FireLuaEvent("squaddie_added", new Dictionary<string, object>
        {
            ["squaddie"] = _name ?? _nickname ?? "<unknown>",
            ["nickname"] = _nickname ?? "<unknown>",
            ["home_planet"] = _homePlanet.ToString(),
            ["alive_count"] = count
        });
    }
    */

    // --- Operation/Mission Events ---

    private static void OnOperationStarted_Postfix(object __instance, object _operation)
    {
        var operationPtr = Il2CppUtils.GetPointer(_operation);

        OnOperationStarted?.Invoke(operationPtr);

        FireLuaEvent("operation_started", new Dictionary<string, object>
        {
            ["operation"] = GetName(_operation),
            ["operation_ptr"] = operationPtr.ToInt64()
        });
    }

    private static void OnOperationFinished_Postfix(object __instance, object _operation)
    {
        var operationPtr = Il2CppUtils.GetPointer(_operation);
        
        OnOperationFinished?.Invoke(operationPtr);

        FireLuaEvent("operation_finished", new Dictionary<string, object>
        {
            ["operation"] = GetName(_operation),
            ["operation_ptr"] = operationPtr.ToInt64()
        });
    }

    private static void OnMissionStarted_Postfix(object __instance, object _operation, object _mission)
    {
        var operationPtr = Il2CppUtils.GetPointer(_operation);
        var missionPtr = Il2CppUtils.GetPointer(_mission);

        OnMissionStarted?.Invoke(operationPtr, missionPtr);

        FireLuaEvent("mission_started", new Dictionary<string, object>
        {
            ["operation"] = GetName(_operation),
            ["operation_ptr"] = operationPtr.ToInt64(),
            ["mission"] = GetName(_mission),
            ["mission_ptr"] = missionPtr.ToInt64()
        });
    }

    private static void OnMissionFinished_Postfix(object __instance, object _operation, object _mission, object _missionResult)
    {
        var operationPtr = Il2CppUtils.GetPointer(_operation);
        var missionPtr = Il2CppUtils.GetPointer(_mission);
        var missionResultPtr = Il2CppUtils.GetPointer(_missionResult);

        OnMissionFinished?.Invoke(operationPtr, missionPtr, missionResultPtr);

        FireLuaEvent("mission_finished", new Dictionary<string, object>
        {
            ["operation"] = GetName(_operation),
            ["operation_ptr"] = operationPtr.ToInt64(),
            ["mission"] = GetName(_mission),
            ["mission_ptr"] = missionPtr.ToInt64(),
            ["mission_result"] = GetName(_missionResult),
            ["mission_result_ptr"] = missionResultPtr.ToInt64()
        });
    }

    // --- Black Market Events ---

    private static void BlackMarketAddItem_Postfix(object __instance, object _item)
    {
        var itemPtr = Il2CppUtils.GetPointer(_item);
        OnBlackMarketItemAdded?.Invoke(itemPtr);

        FireLuaEvent("blackmarket_item_added", new Dictionary<string, object>
        {
            ["item"] = GetName(_item),
            ["item_ptr"] = itemPtr.ToInt64()
        });
    }

    private static void BlackMarketFillUp_Postfix(object __instance)
    {
        OnBlackMarketRestocked?.Invoke();

        FireLuaEvent("blackmarket_restocked", new Dictionary<string, object>());
    }

    // --- Emotion State Events ---

    private static void TriggerEmotion_Postfix(object __instance, object _trigger, object _target, object _random, object _mission)
    {
        var triggerPtr = Il2CppUtils.GetPointer(_trigger);
        var targetPtr = Il2CppUtils.GetPointer(_target);
        var randomPtr = Il2CppUtils.GetPointer(_random);
        var missionPtr = Il2CppUtils.GetPointer(_mission);

        OnTriggerEmotion?.Invoke(triggerPtr, targetPtr, randomPtr, missionPtr);

        FireLuaEvent("emotion_triggered", new Dictionary<string, object>
        {
            ["trigger"] = GetName(_trigger),
            ["trigger_ptr"] = triggerPtr.ToInt64(),
            ["target"] = GetName(_target),
            ["target_ptr"] = targetPtr.ToInt64(),
            ["mission"] = GetName(_mission),
            ["mission_ptr"] = missionPtr.ToInt64()
        });
    }
}
