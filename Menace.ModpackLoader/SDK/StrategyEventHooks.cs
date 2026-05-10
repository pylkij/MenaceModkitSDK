using System;
using System.Collections.Generic;
using System.Reflection;

using Menace.SDK.Internal;

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
    public static event Action<IntPtr> OnLeaderLevelUp;                  // leader

    // Faction Events
    public static event Action<IntPtr, int> OnFactionTrustChanged;       // faction, delta
    public static event Action<IntPtr, int> OnFactionStatusChanged;      // faction, newStatus
    public static event Action<IntPtr, IntPtr> OnFactionUpgradeUnlocked; // faction, upgrade

    // Squaddie Events
    public static event Action<int> OnSquaddieKilled;                    // squaddieId
    public static event Action<int> OnSquaddieAdded;                     // count

    // Operation/Mission Events
    public static event Action<IntPtr> OnMissionEnded;                   // mission
    public static event Action OnOperationFinished;

    // Black Market Events
    public static event Action<IntPtr> OnBlackMarketItemAdded;           // item
    public static event Action OnBlackMarketRestocked;

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
            const string rosterType = "Il2CppMenace.Strategy.Roster";
            const string storyFactionType = "Il2CppMenace.Strategy.StoryFaction";
            const string squaddiesType = "Il2CppMenace.Strategy.Squaddies";
            const string operationType = "Il2CppMenace.Strategy.Operation";
            const string blackMarketType = "Il2CppMenace.Strategy.BlackMarket";
            const string eventManagerType = "Il2CppMenace.Strategy.EventManager";
            const string baseUnitLeaderType = "Il2CppMenace.Strategy.BaseUnitLeader";

            var hooks = typeof(StrategyEventHooks);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;

            int patchCount = 0;

            // Roster patches
            if (rosterType != null)
            {
                patchCount += GamePatch.Postfix(harmony, rosterType, "HireLeader", hooks.GetMethod(nameof(HireLeader_Postfix), flags)) ? 1 : 0;
                patchCount += GamePatch.Postfix(harmony, rosterType, "TryDismissLeader", hooks.GetMethod(nameof(DismissLeader_Postfix), flags)) ? 1 : 0;
                patchCount += GamePatch.Postfix(harmony, rosterType, "OnPermanentDeath", hooks.GetMethod(nameof(OnPermanentDeath_Postfix), flags)) ? 1 : 0;
            }

            // Leader patches
            if (baseUnitLeaderType != null)
            {
                patchCount += GamePatch.Postfix(harmony, baseUnitLeaderType, "AddPerk", hooks.GetMethod(nameof(AddPerk_Postfix), flags)) ? 1 : 0;
            }

            // Faction patches
            if (storyFactionType != null)
            {
                patchCount += GamePatch.Postfix(harmony, storyFactionType, "ChangeTrust", hooks.GetMethod(nameof(ChangeTrust_Postfix), flags)) ? 1 : 0;
                patchCount += GamePatch.Postfix(harmony, storyFactionType, "SetStatus", hooks.GetMethod(nameof(SetStatus_Postfix), flags)) ? 1 : 0;
                patchCount += GamePatch.Postfix(harmony, storyFactionType, "UnlockUpgrade", hooks.GetMethod(nameof(UnlockUpgrade_Postfix), flags)) ? 1 : 0;
            }

            // Squaddie patches
            if (squaddiesType != null)
            {
                patchCount += GamePatch.Postfix(harmony, squaddiesType, "Kill", hooks.GetMethod(nameof(SquaddieKill_Postfix), flags)) ? 1 : 0;
                patchCount += GamePatch.Postfix(harmony, squaddiesType, "AddAlive", hooks.GetMethod(nameof(SquaddieAddAlive_Postfix), flags)) ? 1 : 0;
            }

            // Operation patches
            if (operationType != null)
            {
                patchCount += GamePatch.Postfix(harmony, operationType, "EndMission", hooks.GetMethod(nameof(EndMission_Postfix), flags)) ? 1 : 0;
            }

            // EventManager patches
            if (eventManagerType != null)
            {
                patchCount += GamePatch.Postfix(harmony, eventManagerType, "OnOperationFinished", hooks.GetMethod(nameof(OnOperationFinished_Postfix), flags)) ? 1 : 0;
            }

            // BlackMarket patches
            if (blackMarketType != null)
            {
                patchCount += GamePatch.Postfix(harmony, blackMarketType, "AddItem", hooks.GetMethod(nameof(BlackMarketAddItem_Postfix), flags)) ? 1 : 0;
                patchCount += GamePatch.Postfix(harmony, blackMarketType, "FillUp", hooks.GetMethod(nameof(BlackMarketFillUp_Postfix), flags)) ? 1 : 0;
            }

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

    private static void HireLeader_Postfix(object __instance, object __result, object template)
    {
        if (__result == null) return; // Hire failed

        var leaderPtr = Il2CppUtils.GetPointer(__result);
        OnLeaderHired?.Invoke(leaderPtr);

        FireLuaEvent("leader_hired", new Dictionary<string, object>
        {
            ["leader"] = GetName(__result),
            ["leader_ptr"] = leaderPtr.ToInt64(),
            ["template"] = GetName(template)
        });
    }

    private static void DismissLeader_Postfix(object __instance, bool __result, object leader)
    {
        if (!__result) return; // Dismiss failed

        var leaderPtr = Il2CppUtils.GetPointer(leader);
        OnLeaderDismissed?.Invoke(leaderPtr);

        FireLuaEvent("leader_dismissed", new Dictionary<string, object>
        {
            ["leader"] = GetName(leader),
            ["leader_ptr"] = leaderPtr.ToInt64()
        });
    }

    private static void OnPermanentDeath_Postfix(object __instance, object leader)
    {
        var leaderPtr = Il2CppUtils.GetPointer(leader);
        OnLeaderPermadeath?.Invoke(leaderPtr);

        FireLuaEvent("leader_permadeath", new Dictionary<string, object>
        {
            ["leader"] = GetName(leader),
            ["leader_ptr"] = leaderPtr.ToInt64()
        });
    }

    private static void AddPerk_Postfix(object __instance, object perk)
    {
        var leaderPtr = Il2CppUtils.GetPointer(__instance);
        OnLeaderLevelUp?.Invoke(leaderPtr);

        FireLuaEvent("leader_levelup", new Dictionary<string, object>
        {
            ["leader"] = GetName(__instance),
            ["leader_ptr"] = leaderPtr.ToInt64(),
            ["perk"] = GetName(perk)
        });
    }

    // --- Faction Events ---

    private static void ChangeTrust_Postfix(object __instance, int delta)
    {
        if (delta == 0) return;

        var factionPtr = Il2CppUtils.GetPointer(__instance);
        OnFactionTrustChanged?.Invoke(factionPtr, delta);

        FireLuaEvent("faction_trust_changed", new Dictionary<string, object>
        {
            ["faction"] = GetFactionName(__instance),
            ["faction_ptr"] = factionPtr.ToInt64(),
            ["delta"] = delta
        });
    }

    private static void SetStatus_Postfix(object __instance, int status)
    {
        var factionPtr = Il2CppUtils.GetPointer(__instance);
        OnFactionStatusChanged?.Invoke(factionPtr, status);

        FireLuaEvent("faction_status_changed", new Dictionary<string, object>
        {
            ["faction"] = GetFactionName(__instance),
            ["faction_ptr"] = factionPtr.ToInt64(),
            ["status"] = status
        });
    }

    private static void UnlockUpgrade_Postfix(object __instance, object upgrade)
    {
        var factionPtr = Il2CppUtils.GetPointer(__instance);
        var upgradePtr = Il2CppUtils.GetPointer(upgrade);

        OnFactionUpgradeUnlocked?.Invoke(factionPtr, upgradePtr);

        FireLuaEvent("faction_upgrade_unlocked", new Dictionary<string, object>
        {
            ["faction"] = GetFactionName(__instance),
            ["faction_ptr"] = factionPtr.ToInt64(),
            ["upgrade"] = GetName(upgrade),
            ["upgrade_ptr"] = upgradePtr.ToInt64()
        });
    }

    // --- Squaddie Events ---

    private static void SquaddieKill_Postfix(object __instance, bool __result, int squaddieId)
    {
        if (!__result) return; // Kill failed

        OnSquaddieKilled?.Invoke(squaddieId);

        FireLuaEvent("squaddie_killed", new Dictionary<string, object>
        {
            ["squaddie_id"] = squaddieId
        });
    }

    private static void SquaddieAddAlive_Postfix(object __instance, object squaddie)
    {
        // Get alive count from the Squaddies instance
        int count = 0;
        try
        {
            var countMethod = __instance.GetType().GetMethod("GetAliveCount",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (countMethod != null)
            {
                count = (int)countMethod.Invoke(__instance, null);
            }
        }
        catch { }

        OnSquaddieAdded?.Invoke(count);

        FireLuaEvent("squaddie_added", new Dictionary<string, object>
        {
            ["squaddie"] = GetName(squaddie),
            ["alive_count"] = count
        });
    }

    // --- Operation/Mission Events ---

    private static void EndMission_Postfix(object __instance, object mission)
    {
        var missionPtr = Il2CppUtils.GetPointer(mission);
        OnMissionEnded?.Invoke(missionPtr);

        // Get mission result info if available
        string status = "unknown";
        try
        {
            // mission is MissionState, get result
            var statusProp = mission?.GetType().GetProperty("Status",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (statusProp != null)
            {
                var statusVal = statusProp.GetValue(mission);
                status = statusVal?.ToString() ?? "unknown";
            }
        }
        catch { }

        FireLuaEvent("mission_ended", new Dictionary<string, object>
        {
            ["mission_ptr"] = missionPtr.ToInt64(),
            ["status"] = status
        });
    }

    private static void OnOperationFinished_Postfix(object __instance)
    {
        OnOperationFinished?.Invoke();

        FireLuaEvent("operation_finished", new Dictionary<string, object>());
    }

    // --- Black Market Events ---

    private static void BlackMarketAddItem_Postfix(object __instance, object item)
    {
        var itemPtr = Il2CppUtils.GetPointer(item);
        OnBlackMarketItemAdded?.Invoke(itemPtr);

        FireLuaEvent("blackmarket_item_added", new Dictionary<string, object>
        {
            ["item"] = GetName(item),
            ["item_ptr"] = itemPtr.ToInt64()
        });
    }

    private static void BlackMarketFillUp_Postfix(object __instance)
    {
        OnBlackMarketRestocked?.Invoke();

        FireLuaEvent("blackmarket_restocked", new Dictionary<string, object>());
    }
}
