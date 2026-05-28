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
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // SkillTemplate fields (inherited by PerkTemplate)
    // Title/Description are LocalizedLine/LocalizedMultiLine object references — not strings.
    // Use ObjFieldHandle and call GetRawDefaultTranslation() on the result.
    private static ObjFieldHandle<PerkTemplate, LocalizedLine> _hTitle;
    private static ObjFieldHandle<PerkTemplate, LocalizedMultiLine> _hDescription;
    private static FieldHandle<PerkTemplate, int> _hActionPointCost;
    private static FieldHandle<PerkTemplate, bool> _hIsActive;

    // BaseUnitLeader fields
    private static ObjFieldHandle<BaseUnitLeader, UnitLeaderTemplate> _hLeaderTemplate;
    private static ObjFieldHandle<BaseUnitLeader, Il2CppSystem.Collections.Generic.List<PerkTemplate>> _hLeaderPerks;

    // UnitLeaderTemplate fields
    private static ObjFieldHandle<UnitLeaderTemplate, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PerkTreeTemplate>> _hPerkTrees;

    // PerkTreeTemplate fields
    private static ObjFieldHandle<PerkTreeTemplate, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Perk>> _hTreePerks;

    // Perk fields
    private static ObjFieldHandle<Perk, PerkTemplate> _hPerkSkill;
    private static FieldHandle<Perk, int> _hPerkTier;

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
            _hTitle = GameObj<PerkTemplate>.ResolveObjField(x => x.Title);
            _hDescription = GameObj<PerkTemplate>.ResolveObjField(x => x.Description);
            _hActionPointCost = GameObj<PerkTemplate>.ResolveField(x => x.ActionPointCost);
            _hIsActive = GameObj<PerkTemplate>.ResolveField(x => x.IsActive);

            _hLeaderTemplate = GameObj<BaseUnitLeader>.ResolveObjField(x => x.LeaderTemplate);
            _hLeaderPerks = GameObj<BaseUnitLeader>.ResolveObjField(x => x.m_Perks);

            _hPerkTrees = GameObj<UnitLeaderTemplate>.ResolveObjField(x => x.PerkTrees);

            _hTreePerks = GameObj<PerkTreeTemplate>.ResolveObjField(x => x.Perks);

            _hPerkSkill = GameObj<Perk>.ResolveObjField(x => x.Skill);
            _hPerkTier = GameObj<Perk>.ResolveField(x => x.Tier);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.ResolveHandles: Field handle resolution failed", ex);
        }
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
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        try
        {
            if (!_hLeaderPerks.TryRead(leader, out var perksObj) || perksObj.Untyped.IsNull) return result;
            if (perksObj.Untyped.CheckAlive() != AliveStatus.Alive) return result;

            var perks = perksObj.AsManaged();
            if (perks == null) return result;

            for (int i = 0; i < perks.Count; i++)
            {
                var perk = perks[i];
                if (perk == null) continue;

                var perkObj = GameObj<PerkTemplate>.Wrap(perk.Pointer);
                var info = GetPerkInfo(perkObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.GetLeaderPerks: Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get detailed information about a perk template.
    /// </summary>
    public static PerkInfo GetPerkInfo(GameObj<PerkTemplate> perkTemplate)
    {
        if (perkTemplate.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var info = new PerkInfo
            {
                Pointer = perkTemplate.Untyped.Pointer,
                Name = perkTemplate.Untyped.GetName()
            };

            if (_hTitle.TryRead(perkTemplate, out var titleObj) && !titleObj.Untyped.IsNull)
                info.Title = titleObj.AsManaged().GetRawDefaultTranslation() ?? info.Name;

            if (_hDescription.TryRead(perkTemplate, out var descObj) && !descObj.Untyped.IsNull)
                info.Description = descObj.AsManaged().GetRawDefaultTranslation();

            info.ActionPointCost = _hActionPointCost.Read(perkTemplate);
            info.IsActive = _hIsActive.Read(perkTemplate);

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.GetPerkInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get perk trees available to a unit leader from their template.
    /// </summary>
    public static List<PerkTreeInfo> GetPerkTrees(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<PerkTreeInfo>();
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        try
        {
            if (!_hLeaderTemplate.TryRead(leader, out var template) || template.Untyped.IsNull) return result;
            if (template.Untyped.CheckAlive() != AliveStatus.Alive) return result;

            if (!_hPerkTrees.TryRead(template, out var perkTreesObj) || perkTreesObj.Untyped.IsNull) return result;

            var perkTreesArray = perkTreesObj.AsManaged();
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
            SdkLogger.Error("Perks.GetPerkTrees: Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a perk tree.
    /// Uses pure reflection for IL2CPP compatibility.
    /// </summary>
    public static PerkTreeInfo GetPerkTreeInfo(GameObj<PerkTreeTemplate> perkTree)
    {
        if (perkTree.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var info = new PerkTreeInfo
            {
                Pointer = perkTree.Untyped.Pointer,
                Name = perkTree.Untyped.GetName()
            };

            if (!_hTreePerks.TryRead(perkTree, out var perksObj) || perksObj.Untyped.IsNull) return info;

            var perksArray = perksObj.AsManaged();
            info.PerkCount = perksArray.Length;

            for (int i = 0; i < perksArray.Length; i++)
            {
                var perk = perksArray[i];
                if (perk == null) continue;

                var perkObj = GameObj<Perk>.Wrap(perk.Pointer);
                if (perkObj.Untyped.CheckAlive() != AliveStatus.Alive) continue;

                if (!_hPerkSkill.TryRead(perkObj, out var skillObj) || skillObj.Untyped.IsNull) continue;

                var perkInfo = GetPerkInfo(skillObj);
                if (perkInfo == null) continue;

                perkInfo.Tier = _hPerkTier.Read(perkObj);
                info.Perks.Add(perkInfo);
            }

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.GetPerkTreeInfo: Failed", ex);
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
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            return leader.AsManaged().CanBePromoted();
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.CanBePromoted: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a leader can be demoted (has perks to remove).
    /// </summary>
    public static bool CanBeDemoted(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            return leader.AsManaged().CanBeDemoted();
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.CanBeDemoted: Failed", ex);
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
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        if (perkTemplate.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            leader.AsManaged().AddPerk(perkTemplate.AsManaged(), spendPromotionPoints);
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.AddPerk: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove the last perk from a unit leader.
    /// </summary>
    public static bool RemoveLastPerk(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            return leader.AsManaged().TryRemoveLastPerk();
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.RemoveLastPerk: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a leader has a specific perk.
    /// </summary>
    public static bool HasPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perkTemplate)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        if (perkTemplate.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            return leader.AsManaged().HasPerk(perkTemplate.AsManaged());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.HasPerk: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the last perk added to a leader.
    /// </summary>
    public static GameObj<PerkTemplate> GetLastPerk(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return default;

        try
        {
            var result = leader.AsManaged().GetLastPerk();
            if (result == null) return default;

            return GameObj<PerkTemplate>.Wrap(result.Pointer);
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Perks.GetLastPerk: Failed", ex);
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
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive || string.IsNullOrEmpty(perkName)) return default;

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
            SdkLogger.Error("Perks.FindPerkByName: Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Get available perks (not yet learned) for a leader.
    /// </summary>
    public static List<PerkInfo> GetAvailablePerks(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<PerkInfo>();
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return result;

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
            SdkLogger.Error("Perks.GetAvailablePerks: Failed", ex);
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
