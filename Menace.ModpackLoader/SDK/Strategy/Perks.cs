using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for perk and skill management.
/// Provides safe access to perk trees, perk manipulation, and skill inspection.
///
/// Based on reverse engineering findings:
/// - BaseUnitLeader.m_Perks @ +0x48 (List&lt;PerkTemplate&gt;)
/// - UnitLeaderTemplate.PerkTrees @ array of PerkTreeTemplate
/// - PerkTreeTemplate.Perks @ array of Perk (Tier 1-4)
/// - PerkTemplate extends SkillTemplate
/// </summary>
public static class Perks
{
    // Cached types
    private static readonly GameType _perkTemplateType = GameType.Of<Il2CppMenace.Strategy.PerkTemplate>();
    private static readonly GameType _perkTreeTemplateType = GameType.Of<Il2CppMenace.Strategy.PerkTreeTemplate>();
    private static readonly GameType _perkType = GameType.Of<Il2CppMenace.Strategy.Perk>();
    private static readonly GameType _skillTemplateType = GameType.Of<Il2CppMenace.Tactical.Skills.SkillTemplate>();
    private static readonly GameType _unitLeaderType = GameType.Of<Il2CppMenace.Strategy.BaseUnitLeader>();

    private static class Offsets
    {
        // SkillTemplate fields (inherited by PerkTemplate)
        // Title/Description are LocalizedLine/LocalizedMultiLine object pointers — not strings.
        // Do NOT use ResolveStringField. ReadLocalizedText handles extraction.
        internal static readonly Lazy<FieldHandle<PerkTemplate, IntPtr>> Title
            = new(() => GameObj<PerkTemplate>.FieldAt<IntPtr>(0x78, nameof(Title)));

        internal static readonly Lazy<FieldHandle<PerkTemplate, IntPtr>> Description
            = new(() => GameObj<PerkTemplate>.FieldAt<IntPtr>(0x80, nameof(Description)));

        internal static readonly Lazy<FieldHandle<PerkTemplate, int>> ActionPointCost
            = new(() => GameObj<PerkTemplate>.ResolveField(x => x.ActionPointCost));

        internal static readonly Lazy<FieldHandle<PerkTemplate, bool>> IsActive
            = new(() => GameObj<PerkTemplate>.ResolveField(x => x.IsActive));

        // BaseUnitLeader fields
        internal static readonly Lazy<FieldHandle<BaseUnitLeader, IntPtr>> m_Perks
            = new(() => GameObj<BaseUnitLeader>.FieldAt<IntPtr>(0x48, nameof(m_Perks)));

        internal static readonly Lazy<FieldHandle<UnitLeaderTemplate, IntPtr>> PerkTrees
            = new(() => GameObj<UnitLeaderTemplate>.FieldAt<IntPtr>(0x170, nameof(PerkTrees)));

        internal static readonly Lazy<FieldHandle<PerkTreeTemplate, IntPtr>> Perks
            = new(() => GameObj<PerkTreeTemplate>.FieldAt<IntPtr>(0x18, nameof(Perks)));

        internal static readonly Lazy<FieldHandle<Perk, IntPtr>> PerkSkill
            = new(() => GameObj<Perk>.FieldAt<IntPtr>(0x10, "Skill"));

        internal static readonly Lazy<FieldHandle<Perk, int>> PerkTier
            = new(() => GameObj<Perk>.FieldAt<int>(0x18, nameof(Perk.Tier)));
    }

    /// <summary>
    /// Perk information structure.
    /// </summary>
    public class PerkInfo
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Tier { get; set; }
        public int ActionPointCost { get; set; }
        public bool IsActive { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Perk tree information structure.
    /// </summary>
    public class PerkTreeInfo
    {
        public string Name { get; set; }
        public int PerkCount { get; set; }
        public List<PerkInfo> Perks { get; set; } = new();
        public IntPtr Pointer { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Perk Queries
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all perks for a unit leader with detailed info.
    /// </summary>
    public static List<PerkInfo> GetLeaderPerks(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<PerkInfo>();
        if (leader.Untyped.IsNull) return result;

        try
        {
            var perksPtr = Offsets.m_Perks.Value.Read(leader);
            if (perksPtr == IntPtr.Zero) return result;

            var perks = new Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.PerkTemplate>(perksPtr);
            if (perks == null) return result;

            var listType = perks.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");
            if (countProp == null || indexer == null) return result;

            int count = (int)countProp.GetValue(perks);
            for (int i = 0; i < count; i++)
            {
                var perk = indexer.Invoke(perks, new object[] { i });
                if (perk == null) continue;

                var perkObj = GameObj<PerkTemplate>.Wrap(((Il2CppObjectBase)perk).Pointer);
                var info = GetPerkInfo(perkObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetLeaderPerks", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get detailed information about a perk template.
    /// </summary>
    public static PerkInfo GetPerkInfo(GameObj<PerkTemplate> perkTemplate)
    {
        if (perkTemplate.Untyped.IsNull) return null;

        try
        {
            var info = new PerkInfo
            {
                Pointer = perkTemplate.Untyped.Pointer,
                Name = perkTemplate.Untyped.GetName()
            };

            var titlePtr = Offsets.Title.Value.Read(perkTemplate);
            if (titlePtr != IntPtr.Zero)
                info.Title = new BaseLocalizedString(titlePtr).GetRawDefaultTranslation() ?? info.Name;

            var descPtr = Offsets.Description.Value.Read(perkTemplate);
            if (descPtr != IntPtr.Zero)
                info.Description = new BaseLocalizedString(descPtr).GetRawDefaultTranslation();

            info.ActionPointCost = Offsets.ActionPointCost.Value.Read(perkTemplate);
            info.IsActive = Offsets.IsActive.Value.Read(perkTemplate);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetPerkInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get perk trees available to a unit leader from their template.
    /// </summary>
    public static List<PerkTreeInfo> GetPerkTrees(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<PerkTreeInfo>();
        if (leader.Untyped.IsNull) return result;

        try
        {
            var leaderProxy = leader.AsManaged();
            if (leaderProxy == null) return result;

            var templatePtr = leaderProxy.LeaderTemplate?.Pointer ?? IntPtr.Zero;
            if (templatePtr == IntPtr.Zero) return result;

            var template = GameObj<UnitLeaderTemplate>.Wrap(templatePtr);

            var perkTreesPtr = Offsets.PerkTrees.Value.Read(template);
            if (perkTreesPtr == IntPtr.Zero) return result;

            // PerkTrees is a PerkTreeTemplate[] — read as Il2CppArrayBase
            var perkTreesArray = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PerkTreeTemplate>(perkTreesPtr);
            for (int i = 0; i < perkTreesArray.Length; i++)
            {
                var treeProxy = perkTreesArray[i];
                if (treeProxy == null) continue;

                var treeObj = GameObj<PerkTreeTemplate>.Wrap(treeProxy.Pointer);
                var treeInfo = GetPerkTreeInfo(treeObj);
                if (treeInfo != null)
                    result.Add(treeInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetPerkTrees", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a perk tree.
    /// Uses pure reflection for IL2CPP compatibility.
    /// </summary>
    public static PerkTreeInfo GetPerkTreeInfo(GameObj<PerkTreeTemplate> perkTree)
    {
        if (perkTree.Untyped.IsNull) return null;

        try
        {
            var info = new PerkTreeInfo
            {
                Pointer = perkTree.Untyped.Pointer,
                Name = perkTree.Untyped.GetName()
            };

            var perksPtr = Offsets.Perks.Value.Read(perkTree);
            if (perksPtr == IntPtr.Zero) return info;

            var perksArray = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Perk>(perksPtr);
            info.PerkCount = perksArray.Length;

            for (int i = 0; i < perksArray.Length; i++)
            {
                var perk = perksArray[i];
                if (perk == null) continue;

                var skillPtr = Offsets.PerkSkill.Value.Read(GameObj<Perk>.Wrap(perk.Pointer));
                if (skillPtr == IntPtr.Zero) continue;

                var perkInfo = GetPerkInfo(GameObj<PerkTemplate>.Wrap(skillPtr));
                if (perkInfo == null) continue;

                perkInfo.Tier = Offsets.PerkTier.Value.Read(GameObj<Perk>.Wrap(perk.Pointer));
                info.Perks.Add(perkInfo);
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetPerkTreeInfo", "Failed", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Perk Manipulation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a leader can be promoted (has room for more perks).
    /// </summary>
    public static bool CanBePromoted(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return false;

        try
        {
            return leader.AsManaged().CanBePromoted();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.CanBePromoted", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a leader can be demoted (has perks to remove).
    /// </summary>
    public static bool CanBeDemoted(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return false;

        try
        {
            return leader.AsManaged().CanBeDemoted();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.CanBeDemoted", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Add a perk to a unit leader.
    /// </summary>
    /// <param name="leader">The leader to add the perk to</param>
    /// <param name="perkTemplate">The perk template to add</param>
    /// <param name="spendPromotionPoints">Whether to spend promotion points (default true)</param>
    public static bool AddPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perkTemplate, bool spendPromotionPoints = true)
    {
        if (leader.Untyped.IsNull || perkTemplate.Untyped.IsNull) return false;

        try
        {
            leader.AsManaged().AddPerk(perkTemplate.AsManaged(), spendPromotionPoints);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.AddPerk", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove the last perk from a unit leader.
    /// </summary>
    public static bool RemoveLastPerk(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return false;

        try
        {
            return leader.AsManaged().TryRemoveLastPerk();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.RemoveLastPerk", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a leader has a specific perk.
    /// </summary>
    public static bool HasPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perkTemplate)
    {
        if (leader.Untyped.IsNull || perkTemplate.Untyped.IsNull) return false;

        try
        {
            return leader.AsManaged().HasPerk(perkTemplate.AsManaged());
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.HasPerk", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the last perk added to a leader.
    /// </summary>
    public static GameObj<PerkTemplate> GetLastPerk(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return default;

        try
        {
            var result = leader.AsManaged().GetLastPerk();
            if (result == null) return default;

            return GameObj<PerkTemplate>.Wrap(result.Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetLastPerk", "Failed", ex);
            return default;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Perk Finding
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find a perk template by name from all perk trees of a leader.
    /// </summary>
    public static GameObj<PerkTemplate> FindPerkByName(GameObj<BaseUnitLeader> leader, string perkName)
    {
        if (leader.Untyped.IsNull || string.IsNullOrEmpty(perkName)) return default;

        try
        {
            var trees = GetPerkTrees(leader);
            var allPerks = new List<string>();

            foreach (var tree in trees)
            {
                foreach (var perk in tree.Perks)
                {
                    allPerks.Add($"{perk.Name ?? "?"}/{perk.Title ?? "?"}");

                    if (perk.Name?.Contains(perkName, StringComparison.OrdinalIgnoreCase) == true ||
                        perk.Title?.Contains(perkName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return GameObj<PerkTemplate>.Wrap(perk.Pointer);
                    }
                }
            }

            if (allPerks.Count > 0)
                SdkLogger.Warning($"[Perks.FindPerkByName] '{perkName}' not found. Available: {string.Join(", ", allPerks.Take(10))}...");
            else
                SdkLogger.Warning($"[Perks.FindPerkByName] No perks found in leader's trees");

            return default;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Perks.FindPerkByName] Exception: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Get available perks (not yet learned) for a leader.
    /// </summary>
    public static List<PerkInfo> GetAvailablePerks(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<PerkInfo>();
        if (leader.Untyped.IsNull) return result;

        try
        {
            var learnedPerks = new HashSet<IntPtr>();
            var learned = GetLeaderPerks(leader);
            foreach (var p in learned)
                learnedPerks.Add(p.Pointer);

            var trees = GetPerkTrees(leader);
            foreach (var tree in trees)
            {
                foreach (var perk in tree.Perks)
                {
                    if (!learnedPerks.Contains(perk.Pointer))
                        result.Add(perk);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetAvailablePerks", "Failed", ex);
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register console commands for Perks SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // perks <nickname> - Show unit's perks
        DevConsole.RegisterCommand("perks", "<nickname>", "Show a unit's learned perks", args =>
        {
            if (args.Length == 0)
                return "Usage: perks <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var perks = GetLeaderPerks(leader);
            if (perks.Count == 0)
                return $"{nickname} has no perks";

            var lines = new List<string> { $"{nickname}'s Perks ({perks.Count}):" };
            foreach (var p in perks)
            {
                var title = !string.IsNullOrEmpty(p.Title) ? p.Title : p.Name;
                var active = p.IsActive ? " [Active]" : "";
                lines.Add($"  {title}{active}");
            }
            return string.Join("\n", lines);
        });

        // perktrees <nickname> - Show available perk trees
        DevConsole.RegisterCommand("perktrees", "<nickname>", "Show a unit's perk trees", args =>
        {
            if (args.Length == 0)
                return "Usage: perktrees <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var trees = GetPerkTrees(leader);
            if (trees.Count == 0)
                return $"{nickname} has no perk trees";

            var lines = new List<string> { $"{nickname}'s Perk Trees ({trees.Count}):" };
            foreach (var tree in trees)
            {
                lines.Add($"  {tree.Name} ({tree.PerkCount} perks):");
                foreach (var perk in tree.Perks)
                {
                    var title = !string.IsNullOrEmpty(perk.Title) ? perk.Title : perk.Name;
                    lines.Add($"    T{perk.Tier}: {title}");
                }
            }
            return string.Join("\n", lines);
        });

        // availableperks <nickname> - Show perks available to learn
        DevConsole.RegisterCommand("availableperks", "<nickname>", "Show perks a unit can still learn", args =>
        {
            if (args.Length == 0)
                return "Usage: availableperks <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var available = GetAvailablePerks(leader);
            if (available.Count == 0)
                return $"{nickname} has learned all available perks";

            var canPromote = CanBePromoted(leader);
            var lines = new List<string> { $"Available Perks ({available.Count}) - Can Promote: {canPromote}" };

            var byTier = new Dictionary<int, List<PerkInfo>>();
            foreach (var p in available)
            {
                if (!byTier.ContainsKey(p.Tier))
                    byTier[p.Tier] = new List<PerkInfo>();
                byTier[p.Tier].Add(p);
            }

            foreach (var tier in byTier.Keys)
            {
                lines.Add($"  Tier {tier}:");
                foreach (var p in byTier[tier])
                {
                    var title = !string.IsNullOrEmpty(p.Title) ? p.Title : p.Name;
                    lines.Add($"    {title}");
                }
            }
            return string.Join("\n", lines);
        });

        // addperk <nickname> <perk> - Add a perk to a unit
        DevConsole.RegisterCommand("addperk", "<nickname> <perk>", "Add a perk to a unit (no cost)", args =>
        {
            if (args.Length < 2)
                return "Usage: addperk <nickname> <perk>";

            var nickname = args[0];
            var perkName = string.Join(" ", args, 1, args.Length - 1);

            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var perk = FindPerkByName(leader, perkName);
            if (perk.Untyped.IsNull)
                return $"Perk '{perkName}' not found in {nickname}'s perk trees";

            if (AddPerk(leader, perk, false))
            {
                var info = GetPerkInfo(perk);
                return $"Added perk '{info?.Title ?? perkName}' to {nickname}";
            }
            return "Failed to add perk";
        });

        // removeperk <nickname> - Remove last perk from a unit
        DevConsole.RegisterCommand("removeperk", "<nickname>", "Remove last perk from a unit", args =>
        {
            if (args.Length == 0)
                return "Usage: removeperk <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            if (!CanBeDemoted(leader))
                return $"{nickname} cannot be demoted (no perks to remove)";

            var lastPerk = GetLastPerk(leader);
            var perkName = lastPerk.Untyped.IsNull ? "unknown" : (GetPerkInfo(lastPerk)?.Title ?? lastPerk.Untyped.GetName());

            if (RemoveLastPerk(leader))
                return $"Removed perk '{perkName}' from {nickname}";
            return "Failed to remove perk";
        });
    }
}
