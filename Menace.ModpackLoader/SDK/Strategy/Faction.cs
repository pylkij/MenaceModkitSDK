using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Strategy;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for faction management.
/// Provides access to story factions, trust levels, relations, and faction-specific operations.
///
/// Game Model:
///   Campaign → Factions → Operations → Missions
///   Each faction has trust/status, upgrades, and associated operations/planets.
///
/// Based on reverse engineering:
/// - StrategyState.StoryFactions @ offset from Config
/// - StoryFaction.Template, Trust, Status, Upgrades
/// - StoryFactionTemplate.Operations, EnemyAssets, Type
/// </summary>
public static class Faction
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // StrategyState fields
    private static ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.StoryFactions> _hStoryFactions;

    // StoryFactions fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.StoryFactions, Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Strategy.StoryFactionType, Il2CppMenace.Strategy.StoryFaction>> _hFactions;

    // StoryFaction fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.StoryFaction, Il2CppMenace.Strategy.StoryFactionTemplate> _hTemplate;
    private static FieldHandle<Il2CppMenace.Strategy.StoryFaction, int> _hTotalTrust;
    private static FieldHandle<Il2CppMenace.Strategy.StoryFaction, Il2CppMenace.Strategy.StoryFactionStatus> _hStatus;
    private static ObjFieldHandle<Il2CppMenace.Strategy.StoryFaction, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.ShipUpgradeTemplate>> _hUnlockedUpgrades;

    // StoryFactionTemplate fields
    private static FieldHandle<Il2CppMenace.Strategy.StoryFactionTemplate, Il2CppMenace.Strategy.StoryFactionType> _hFactionType;
    private static FieldHandle<Il2CppMenace.Strategy.StoryFactionTemplate, int> _hInitialTotalTrust;
    private static ObjFieldHandle<Il2CppMenace.Strategy.StoryFactionTemplate, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>> _hRequiredTotalTrustForLevel;

    // FactionTemplate fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.FactionTemplate, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppMenace.Strategy.OperationTemplate>> _hOperations;
    private static ObjFieldHandle<Il2CppMenace.Strategy.FactionTemplate, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppMenace.Strategy.EnemyAssetTemplate>> _hEnemyAssets;

    // ShipUpgradeTemplate fields
    private static FieldHandle<Il2CppMenace.Strategy.ShipUpgradeTemplate, Il2CppMenace.Strategy.ShipUpgradeUnlockType> _hUnlockType;
    private static FieldHandle<Il2CppMenace.Strategy.ShipUpgradeTemplate, Il2CppMenace.Strategy.StoryFactionType> _hUnlockedByFaction;

    // ═══════════════════════════════════════════════════════════════════
    //  Initialisation — wire up to GameState.SceneLoaded
    // ═══════════════════════════════════════════════════════════════════

    private static bool _handlesResolved = false;

    internal static void Initialize()
    {
        GameState.SceneLoaded += _ => ResolveHandles();
    }

    private static void ResolveHandles()
    {
        if (_handlesResolved) return;

        try
        {
            _hStoryFactions = GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.StoryFactions);

            _hFactions = GameObj<Il2CppMenace.Strategy.StoryFactions>.ResolveObjField(x => x.m_Factions);

            _hTemplate = GameObj<Il2CppMenace.Strategy.StoryFaction>.ResolveObjField(x => x.m_Template);
            _hTotalTrust = GameObj<Il2CppMenace.Strategy.StoryFaction>.ResolveField(x => x.m_TotalTrust);
            _hStatus = GameObj<Il2CppMenace.Strategy.StoryFaction>.ResolveField(x => x.m_Status);
            _hUnlockedUpgrades = GameObj<Il2CppMenace.Strategy.StoryFaction>.ResolveObjField(x => x.m_UnlockedUpgrades);

            _hFactionType = GameObj<Il2CppMenace.Strategy.StoryFactionTemplate>.ResolveField(x => x.FactionType);
            _hInitialTotalTrust = GameObj<Il2CppMenace.Strategy.StoryFactionTemplate>.ResolveField(x => x.InitialTotalTrust);
            _hRequiredTotalTrustForLevel = GameObj<Il2CppMenace.Strategy.StoryFactionTemplate>.ResolveObjField(x => x.RequiredTotalTrustForLevel);

            _hOperations = GameObj<Il2CppMenace.Strategy.FactionTemplate>.ResolveObjField(x => x.Operations);
            _hEnemyAssets = GameObj<Il2CppMenace.Strategy.FactionTemplate>.ResolveObjField(x => x.EnemyAssets);

            _hUnlockType = GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.ResolveField(x => x.UnlockType);
            _hUnlockedByFaction = GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.ResolveField(x => x.UnlockedByFaction);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.ResolveHandles", "Field handle resolution failed", ex);
        }
    }

    public class FactionInfo
    {
        /// <summary>Stable template ID (m_ID from DataTemplate).</summary>
        public string TemplateId { get; set; }
        /// <summary>Faction type enum.</summary>
        public StoryFactionType FactionType { get; set; }
        /// <summary>Current total trust.</summary>
        public int TotalTrust { get; set; }
        /// <summary>Current trust level (derived from RequiredTotalTrustForLevel).</summary>
        public int TrustLevel { get; set; }
        /// <summary>Faction status.</summary>
        public StoryFactionStatus Status { get; set; }
        /// <summary>Number of unlocked upgrades.</summary>
        public int UnlockedUpgradeCount { get; set; }
        /// <summary>Number of operations this faction can offer.</summary>
        public int OperationCount { get; set; }
        /// <summary>Whether this faction currently has an active operation.</summary>
        public bool HasActiveOperation { get; set; }
        /// <summary>Pointer to StoryFaction instance.</summary>
        public IntPtr Pointer { get; set; }
        /// <summary>Pointer to StoryFactionTemplate.</summary>
        public IntPtr TemplatePointer { get; set; }
    }

    public class UpgradeInfo
    {
        /// <summary>Stable template ID (m_ID from DataTemplate).</summary>
        public string TemplateId { get; set; }
        /// <summary>Unlock type (Faction, EventOnly, etc).</summary>
        public ShipUpgradeUnlockType UnlockType { get; set; }
        /// <summary>Faction required to unlock (relevant when UnlockType is Faction).</summary>
        public StoryFactionType UnlockedByFaction { get; set; }
        /// <summary>Whether this upgrade is unlocked for this faction.</summary>
        public bool IsUnlocked { get; set; }
        /// <summary>Pointer to ShipUpgradeTemplate instance.</summary>
        public IntPtr Pointer { get; set; }
    }

    public static List<GameObj<Il2CppMenace.Strategy.StoryFaction>> GetAllFactions()
    {
        var result = new List<GameObj<Il2CppMenace.Strategy.StoryFaction>>();

        try
        {
            var ss = Il2CppMenace.States.StrategyState.Get();
            if (ss == null) return result;

            for (int i = 0; i <= (int)StoryFactionType.Last; i++)
            {
                var faction = GameMethod.Call<Il2CppMenace.States.StrategyState>(ss, x => x.StoryFactions.GetFaction((StoryFactionType)i));
                if (faction == null) continue;

                if (GameObj<Il2CppMenace.Strategy.StoryFaction>.TryWrap(new GameObj(((Il2CppObjectBase)faction).Pointer), out var typed))
                    result.Add(typed);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetAllFactions", "Failed", ex);
            return result;
        }
    }

    public static FactionInfo GetFactionInfo(GameObj<Il2CppMenace.Strategy.StoryFaction> faction)
    {
        if (faction.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var info = new FactionInfo { Pointer = faction.Untyped.Pointer };

            // Get template
            if (!_hTemplate.TryRead(faction, out var templateObj)) return null;
            info.TemplatePointer = templateObj.Untyped.Pointer;

            var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer);
            if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id))
                info.TemplateId = id;

            if (_hFactionType.TryRead(templateObj, out var factionType))
                info.FactionType = factionType;

            if (_hTotalTrust.TryRead(faction, out var totalTrust))
                info.TotalTrust = totalTrust;

            if (_hRequiredTotalTrustForLevel.TryRead(templateObj, out var trustLevels))
            {
                var levels = trustLevels.AsManaged();
                if (levels != null)
                {
                    int level = 0;
                    for (int i = 0; i < levels.Length; i++)
                    {
                        if (totalTrust >= levels[i]) level = i + 1;
                        else break;
                    }
                    info.TrustLevel = level;
                }
            }

            if (_hStatus.TryRead(faction, out var status))
                info.Status = status;

            if (_hUnlockedUpgrades.TryRead(faction, out var unlockedObj))
            {
                var unlocked = unlockedObj.AsManaged();
                if (unlocked != null)
                    info.UnlockedUpgradeCount = unlocked.Count;
            }

            var factionTemplateObj = GameObj<Il2CppMenace.Strategy.FactionTemplate>.Wrap(templateObj.Untyped.Pointer);
            if (_hOperations.TryRead(factionTemplateObj, out var opsObj))
            {
                var ops = opsObj.AsManaged();
                if (ops != null)
                    info.OperationCount = ops.Length;
            }

            info.HasActiveOperation = HasActiveOperation(faction);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetFactionInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get faction info for all factions.
    /// </summary>
    public static List<FactionInfo> GetAllFactionInfo()
    {
        var result = new List<FactionInfo>();
        var factions = GetAllFactions();
        foreach (var f in factions)
        {
            var info = GetFactionInfo(f);
            if (info != null)
                result.Add(info);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Faction Lookup
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find a faction by faction Type.
    /// </summary>
    public static GameObj<Il2CppMenace.Strategy.StoryFaction> FindByType(StoryFactionType factionType)
    {
        var ss = Il2CppMenace.States.StrategyState.Get();
        if (ss == null) return default;

        var faction = GameMethod.Call<Il2CppMenace.States.StrategyState>(ss, x => x.StoryFactions.GetFaction(factionType));
        if (faction == null) return default;

        GameObj<Il2CppMenace.Strategy.StoryFaction>.TryWrap(new GameObj(((Il2CppObjectBase)faction).Pointer), out var typed);
        return typed;
    }

    /// <summary>
    /// Get factions that have active operations.
    /// </summary>
    public static List<FactionInfo> GetFactionsWithOperations()
    {
        var result = new List<FactionInfo>();
        var factions = GetAllFactionInfo();
        foreach (var f in factions)
        {
            if (f.HasActiveOperation)
                result.Add(f);
        }
        return result;
    }

    /// <summary>
    /// Get factions by status (Known/Unknown).
    /// </summary>
    public static List<FactionInfo> GetFactionsByStatus(StoryFactionStatus status)
    {
        var result = new List<FactionInfo>();
        var factions = GetAllFactionInfo();
        foreach (var f in factions)
        {
            if (f.Status == status)
                result.Add(f);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Trust & Relations
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get trust level with a faction.
    /// </summary>
    public static int GetTrust(GameObj<Il2CppMenace.Strategy.StoryFaction> faction)
    {
        if (!_hTotalTrust.TryRead(faction, out var trust)) return 0;
        return trust;
    }

    /// <summary>
    /// Change trust with a faction.
    /// </summary>
    public static bool ChangeTrust(GameObj<Il2CppMenace.Strategy.StoryFaction> faction, int delta)
    {
        if (faction.Untyped.CheckAlive() != AliveStatus.Alive || delta == 0) return false;

        try
        {
            var managed = faction.AsManaged();
            if (managed == null) return false;

            managed.ChangeTrust(delta);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.ChangeTrust", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set faction status (Known/Unknown).
    /// </summary>
    public static bool SetStatus(GameObj<Il2CppMenace.Strategy.StoryFaction> faction, StoryFactionStatus status)
    {
        if (faction.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var managed = faction.AsManaged();
            if (managed == null) return false;

            managed.SetStatus(status);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.SetStatus", "Failed", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Upgrades
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get upgrades for a faction.
    /// </summary>
    public static List<UpgradeInfo> GetUpgrades(GameObj<Il2CppMenace.Strategy.StoryFaction> faction)
    {
        var result = new List<UpgradeInfo>();
        if (faction.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        try
        {
            if (!_hFactionType.TryRead(GameObj<Il2CppMenace.Strategy.StoryFactionTemplate>.Wrap(
                _hTemplate.Read(faction).Untyped.Pointer), out var factionType)) return result;

            // Build unlocked set from m_UnlockedUpgrades
            var unlockedSet = new HashSet<IntPtr>();
            if (_hUnlockedUpgrades.TryRead(faction, out var unlockedObj))
            {
                var unlockedList = unlockedObj.AsManaged();
                if (unlockedList != null)
                    for (int i = 0; i < unlockedList.Count; i++)
                        if (unlockedList[i] != null)
                            unlockedSet.Add(unlockedList[i].Pointer);
            }

            // All ShipUpgradeTemplates unlocked by this faction
            var allTemplates = Templates.FindAll<Il2CppMenace.Strategy.ShipUpgradeTemplate>();
            foreach (var t in allTemplates)
            {
                var upgradeObj = GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.Wrap(t.Pointer);

                if (!_hUnlockType.TryRead(upgradeObj, out var unlockType)) continue;
                if (unlockType != ShipUpgradeUnlockType.Faction) continue;
                if (!_hUnlockedByFaction.TryRead(upgradeObj, out var unlockedByFaction)) continue;
                if (unlockedByFaction != factionType) continue;

                var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(t.Pointer);
                var info = new UpgradeInfo { Pointer = t.Pointer };

                if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id))
                    info.TemplateId = id;

                info.UnlockType = unlockType;
                info.UnlockedByFaction = unlockedByFaction;
                info.IsUnlocked = unlockedSet.Contains(t.Pointer);

                result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetUpgrades", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Unlock an upgrade for a faction.
    /// </summary>
    public static bool UnlockUpgrade(GameObj<Il2CppMenace.Strategy.StoryFaction> faction, GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate> upgrade)
    {
        if (faction.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        if (upgrade.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var managed = faction.AsManaged();
            if (managed == null) return false;

            var upgradeManaed = upgrade.AsManaged();
            if (upgradeManaed == null) return false;

            managed.UnlockUpgrade(upgradeManaed);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.UnlockUpgrade", "Failed", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Operations
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if faction has an active operation.
    /// </summary>
    public static bool HasActiveOperation(GameObj<Il2CppMenace.Strategy.StoryFaction> faction)
    {
        if (faction.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var currentOp = Operation.GetCurrentOperation();
            if (currentOp.CheckAlive() != AliveStatus.Alive) return false;

            if (!GameObj<Il2CppMenace.Strategy.Operation>.TryWrap(currentOp, out var typed)) return false;
            var opInfo = Operation.GetOperationInfo(typed);
            if (opInfo == null) return false;

            if (!_hTemplate.TryRead(faction, out var templateObj)) return false;
            var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer);
            if (!Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id)) return false;

            // BODGE: EnemyFaction/FriendlyFaction on OperationInfo are not yet confirmed to be
            // template IDs — Operation.cs needs refactoring before this comparison is reliable.
            return opInfo.EnemyFaction == id || opInfo.FriendlyFaction == id;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.HasActiveOperation", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the operation template list for a faction.
    /// </summary>
    public static List<GameObj<Il2CppMenace.Strategy.OperationTemplate>> GetOperationTemplates(GameObj<Il2CppMenace.Strategy.StoryFaction> faction)
    {
        var result = new List<GameObj<Il2CppMenace.Strategy.OperationTemplate>>();
        if (faction.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        try
        {
            if (!_hTemplate.TryRead(faction, out var templateObj)) return result;

            var factionTemplateObj = GameObj<Il2CppMenace.Strategy.FactionTemplate>.Wrap(templateObj.Untyped.Pointer);
            if (!_hOperations.TryRead(factionTemplateObj, out var opsObj)) return result;

            var ops = opsObj.AsManaged();
            if (ops == null) return result;

            for (int i = 0; i < ops.Length; i++)
            {
                if (ops[i] == null) continue;
                if (GameObj<Il2CppMenace.Strategy.OperationTemplate>.TryWrap(new GameObj(ops[i].Pointer), out var typed))
                    result.Add(typed);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetOperationTemplates", "Failed", ex);
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register console commands for Faction SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // factions - List all factions
        DevConsole.RegisterCommand("factions", "", "List all story factions", args =>
        {
            var factions = GetAllFactionInfo();
            if (factions.Count == 0)
                return "No factions found (strategy layer not active?)";

            var lines = new List<string> { $"Story Factions ({factions.Count}):" };
            foreach (var f in factions)
            {
                var ops = f.HasActiveOperation ? " [ACTIVE OP]" : "";
                var upgrades = f.UnlockedUpgradeCount > 0 ? $" [{f.UnlockedUpgradeCount} upgrades unlocked]" : "";
                lines.Add($"  [{f.FactionType}] {f.TemplateId}: Trust {f.TotalTrust} (Lvl {f.TrustLevel}), {f.Status}{upgrades}{ops}");
            }
            return string.Join("\n", lines);
        });

        // faction <type> - Show faction details
        DevConsole.RegisterCommand("faction", "<type>", "Show faction details", args =>
        {
            if (args.Length == 0)
                return "Usage: faction <type> (e.g. Jingwei, Unbent, Dice)";

            if (!Enum.TryParse<StoryFactionType>(args[0], true, out var factionType))
                return $"Unknown faction type '{args[0]}'. Valid: {string.Join(", ", Enum.GetNames(typeof(StoryFactionType)))}";

            var faction = FindByType(factionType);
            if (faction.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Faction '{factionType}' not found";

            var info = GetFactionInfo(faction);
            if (info == null)
                return "Could not get faction info";

            var upgrades = GetUpgrades(faction);
            var unlocked = new List<string>();
            var locked = new List<string>();
            foreach (var u in upgrades)
            {
                if (u.IsUnlocked) unlocked.Add(u.TemplateId);
                else locked.Add(u.TemplateId);
            }

            return $"Faction: {info.TemplateId}\n" +
                   $"Type: {info.FactionType}\n" +
                   $"Status: {info.Status}\n" +
                   $"Trust: {info.TotalTrust} (Level {info.TrustLevel})\n" +
                   $"Operations: {info.OperationCount} available\n" +
                   $"Active Operation: {info.HasActiveOperation}\n" +
                   $"Unlocked ({unlocked.Count}): {string.Join(", ", unlocked)}\n" +
                   $"Locked ({locked.Count}): {string.Join(", ", locked)}";
        });

        // settrust <type> <delta> - Change faction trust
        DevConsole.RegisterCommand("settrust", "<type> <delta>", "Change faction trust", args =>
        {
            if (args.Length < 2)
                return "Usage: settrust <type> <delta>";

            if (!Enum.TryParse<StoryFactionType>(args[0], true, out var factionType))
                return $"Unknown faction type '{args[0]}'";

            if (!int.TryParse(args[1], out int delta))
                return "Invalid delta value";

            var faction = FindByType(factionType);
            if (faction.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Faction '{factionType}' not found";

            var before = GetTrust(faction);
            if (ChangeTrust(faction, delta))
            {
                var after = GetTrust(faction);
                return $"Trust changed: {before} -> {after} (delta: {delta})";
            }
            return "Failed to change trust";
        });

        // factionops - Show factions with active operations
        DevConsole.RegisterCommand("factionops", "", "Show factions with active operations", args =>
        {
            var factions = GetFactionsWithOperations();
            if (factions.Count == 0)
                return "No factions have active operations";

            var lines = new List<string> { $"Factions with Operations ({factions.Count}):" };
            foreach (var f in factions)
                lines.Add($"  [{f.FactionType}] {f.TemplateId}: Trust {f.TotalTrust} (Lvl {f.TrustLevel})");

            return string.Join("\n", lines);
        });
    }
}
