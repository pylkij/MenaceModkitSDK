using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Playables;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for roster and unit management.
/// Provides safe access to hired units, squaddies, perks, and unit status.
///
/// Based on reverse engineering findings:
/// - Roster via StrategyState @ +0x70
/// - BaseUnitLeader.Perks @ +0x48
/// - BaseUnitLeader.Skills @ +0x38
/// - Squaddie structure with NameSeed, Gender, HomePlanet
/// </summary>
public static class Roster
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in ResolveHandles, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // StrategyState
    private static ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.Roster> _hRoster;
    private static ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.Squaddies> _hSquaddies;

    // Roster
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.BaseUnitLeader>> _hHiredLeaders;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.UnitLeaderTemplate>> _hHirableLeaders;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.BaseUnitLeader>> _hDismissedLeaders;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.BaseUnitLeader>> _hUnburiedLeaders;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.BaseUnitLeader>> _hBuriedLeaders;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Strategy.UnitLeaderTemplate, Il2CppMenace.Strategy.BaseUnitLeader>> _hTempLeaders;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppMenace.Strategy.UnitLeaderTemplate> _hDummySquadLeaderTemplate;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Roster, Il2CppMenace.Strategy.UnitLeaderTemplate> _hDummyPilotTemplate;

    // BaseUnitLeader
    private static ObjFieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, Il2CppMenace.Strategy.UnitLeaderTemplate> _hLeaderTemplate;
    private static ObjFieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.PerkTemplate>> _hPerks;
    private static ObjFieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, Il2CppSystem.Collections.Generic.List<int>> _hSquaddieIds;
    private static FieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, int> _hUnavailableOperations;
    private static FieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, int> _hUnavailableMissions;

    // UnitLeaderTemplate
    private static ObjFieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, Il2CppMenace.Tools.LocalizedLine> _hUnitTitle;
    private static FieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, int> _hHiringCosts;
    private static FieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, int> _hRarity;
    private static FieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, int> _hMinCampaignProgress;

    // Squaddie
    private static StringFieldHandle<Il2CppMenace.Strategy.Squaddie> _hSquaddieName;
    private static StringFieldHandle<Il2CppMenace.Strategy.Squaddie> _hSquaddieNickname;

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
            // StrategyState
            _hRoster = GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.Roster);
            _hSquaddies = GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.Squaddies);

            // Roster
            _hHiredLeaders = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_HiredLeaders);
            _hHirableLeaders = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_HirableLeaders);
            _hDismissedLeaders = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_DismissedLeaders);
            _hUnburiedLeaders = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_UnburiedLeaders);
            _hBuriedLeaders = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_BuriedLeaders);
            _hTempLeaders = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_TempLeaders);
            _hDummySquadLeaderTemplate = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_DummySquadLeaderTemplate);
            _hDummyPilotTemplate = GameObj<Il2CppMenace.Strategy.Roster>.ResolveObjField(x => x.m_DummyPilotTemplate);

            // BaseUnitLeader
            _hLeaderTemplate = GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.ResolveObjField(x => x.LeaderTemplate);
            _hPerks = GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.ResolveObjField(x => x.m_Perks);
            _hSquaddieIds = GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.ResolveObjField(x => x.m_SquaddieIds);
            _hUnavailableOperations = GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.FieldAt<int>(0x68, "m_UnavailableDuration.Operations");
            _hUnavailableMissions = GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.FieldAt<int>(0x6C, "m_UnavailableDuration.Missions");

            // UnitLeaderTemplate
            _hUnitTitle = GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.ResolveObjField(x => x.UnitTitle);
            _hHiringCosts = GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.ResolveField(x => x.HiringCosts);
            _hRarity = GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.ResolveField(x => x.Rarity);
            _hMinCampaignProgress = GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.ResolveField(x => x.MinCampaignProgress);

            // Squaddie
            _hSquaddieName = GameObj<Il2CppMenace.Strategy.Squaddie>.ResolveStringField(x => x.Name);
            _hSquaddieNickname = GameObj<Il2CppMenace.Strategy.Squaddie>.ResolveStringField(x => x.Nickname);

            // GameTypes
            _perkTemplateType = GameType.Of<Il2CppMenace.Strategy.PerkTemplate>();
            _unitLeaderTemplateType = GameType.Of<Il2CppMenace.Strategy.UnitLeaderTemplate>();
            _rosterType = GameType.Of<Il2CppMenace.Strategy.Roster>();
            _unitLeaderType = GameType.Of<Il2CppMenace.Strategy.BaseUnitLeader>();
            _squaddieType = GameType.Of<Il2CppMenace.Strategy.Squaddie>();
            _strategyStateType = GameType.Of<Il2CppMenace.States.StrategyState>();

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.ResolveHandles: Field handle resolution failed", ex);
        }
    }

    // Declaration — no initializer
    private static GameType _perkTemplateType;
    private static GameType _unitLeaderTemplateType;
    private static GameType _rosterType;
    private static GameType _unitLeaderType;
    private static GameType _squaddieType;
    private static GameType _strategyStateType;

    // Leader status constants
    public const int STATUS_HIRED = 0;
    public const int STATUS_AVAILABLE = 1;
    public const int STATUS_DEAD = 2;
    public const int STATUS_DISMISSED = 3;
    public const int STATUS_AWAITING_BURIAL = 4;

    /// <summary>
    /// Unit leader information structure.
    /// </summary>
    public class UnitLeaderInfo
    {
        public string TemplateId { get; set; }
        public string Nickname { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; }
        public int Rank { get; set; }
        public string RankName { get; set; }
        public int PerkCount { get; set; }
        public float HealthPercent { get; set; }
        public bool IsDeployable { get; set; }
        public bool IsUnavailable { get; set; }
        public int SquaddieCount { get; set; }
        public int DeployCost { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Squaddie information structure.
    /// </summary>
    public class SquaddieInfo
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Gender { get; set; }
        public string HomePlanet { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Unit leader template information structure.
    /// </summary>
    public class UnitLeaderTemplateInfo
    {
        public string TemplateId { get; set; }
        public string DisplayName { get; set; }
        public int HiringCost { get; set; }
        public int Rarity { get; set; }
        public int MinCampaignProgress { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the current roster instance.
    /// </summary>
    public static GameObj<Il2CppMenace.Strategy.Roster> GetRoster()
    {
        try
        {
            var ss = StrategyState.Get();
            if (ss == null) return default;

            var ssObj = GameObj<StrategyState>.Wrap(ss.Pointer);
            return _hRoster.Read(ssObj);
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetRoster: Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Get all hired unit leaders.
    /// </summary>
    public static List<UnitLeaderInfo> GetHiredLeaders()
    {
        var result = new List<UnitLeaderInfo>();

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.CheckAlive() != AliveStatus.Alive) return result;

            if (!_hHiredLeaders.TryRead(roster, out var hiredListObj)) return result;

            var hiredList = hiredListObj.AsManaged();
            if (hiredList == null) return result;

            foreach (var leader in hiredList)
            {
                if (leader == null) continue;

                var info = GetLeaderInfo(GameObj<BaseUnitLeader>.Wrap(leader.Pointer));
                if (info != null)
                {
                    info.Status = STATUS_HIRED;
                    info.StatusName = "Hired";
                    result.Add(info);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetHiredLeaders: Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a unit leader.
    /// </summary>
    public static UnitLeaderInfo GetLeaderInfo(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var proxy = leader.AsManaged();
            if (proxy == null) return null;

            var info = new UnitLeaderInfo { Pointer = leader.Untyped.Pointer };

            // Template name
            if (_hLeaderTemplate.TryRead(leader, out var templateObj))
            {
                var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer);
                if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id))
                    info.TemplateId = id;
            }

            // Nickname, rank, perk count, health, status flags
            info.Nickname = Il2CppUtils.ToManagedString(proxy.GetNickname());
            info.Rank = (int)proxy.GetRank();
            info.PerkCount = proxy.GetPerkCount();
            info.HealthPercent = proxy.GetHitpointsPct();
            info.IsDeployable = proxy.IsDeployable();
            info.IsUnavailable = proxy.IsUnavailable();

            var rankTemplate = proxy.GetRankTemplate();
            if (rankTemplate != null)
                info.RankName = GameObj<UnitRankTemplate>.Wrap(rankTemplate.Pointer).Untyped.GetName();

            // Squaddie count
            // Note: m_Squaddies does not exist on BaseUnitLeader — actual field is m_SquaddieIds (List<int>)
            // SquaddieCount is populated separately via GetSquaddieCount; left as 0 here.

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetLeaderInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get total hired unit count.
    /// </summary>
    public static int GetHiredCount()
    {
        return GetHiredLeaders().Count;
    }

    /// <summary>
    /// Get available (deployable) unit count.
    /// </summary>
    public static int GetAvailableCount()
    {
        var leaders = GetHiredLeaders();
        int count = 0;
        foreach (var leader in leaders)
        {
            if (leader.IsDeployable)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Find a unit leader by nickname.
    /// </summary>
    [Obsolete("Use FindByNicknameTyped or migrate caller to GameObj<BaseUnitLeader>")]
    public static GameObj FindByNickname(string nickname)
    => FindByNicknameTyped(nickname).Untyped;

    public static GameObj<BaseUnitLeader> FindByNicknameTyped(string nickname)
    {
        try
        {
            var leaders = GetHiredLeaders();

            if (leaders.Count == 0)
            {
                SdkLogger.Warning($"[Roster.FindByNickname] No hired leaders found");
                return default;
            }

            foreach (var leader in leaders)
            {
                var leaderNickname = leader?.Nickname;
                if (string.IsNullOrEmpty(leaderNickname))
                    continue;

                if (leaderNickname.Contains(nickname, StringComparison.OrdinalIgnoreCase))
                    return GameObj<BaseUnitLeader>.Wrap(leader.Pointer);
            }

            var availableNicknames = string.Join(", ", leaders
                .Where(l => !string.IsNullOrEmpty(l?.Nickname))
                .Select(l => l.Nickname));
            SdkLogger.Warning($"[Roster.FindByNickname] '{nickname}' not found. Available: {availableNicknames}");

            return default;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Roster.FindByNickname] Exception: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Get perks for a unit leader.
    /// </summary>
    public static List<string> GetPerks(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<string>();
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        try
        {
            if (!_hPerks.TryRead(leader, out var perkListObj)) return result;

            var perks = perkListObj.AsManaged();
            if (perks == null) return result;

            int i = 0;
            foreach (var perk in perks)
            {
                if (perk == null) { i++; continue; }
                result.Add(GameObj<PerkTemplate>.Wrap(perk.Pointer).Untyped.GetName() ?? $"Perk {i}");
                i++;
            }

            return result;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetPerks: Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get status name from status code.
    /// </summary>
    public static string GetStatusName(int status)
    {
        return status switch
        {
            0 => "Hired",
            1 => "Available",
            2 => "Dead",
            3 => "Dismissed",
            4 => "Awaiting Burial",
            _ => $"Status {status}"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Roster Manipulation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all hirable unit leader templates.
    /// </summary>
    public static List<UnitLeaderTemplateInfo> GetHirableLeaders()
    {
        var result = new List<UnitLeaderTemplateInfo>();

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.CheckAlive() != AliveStatus.Alive) return result;

            if (!_hHirableLeaders.TryRead(roster, out var hirableListObj)) return result;

            var hirableList = hirableListObj.AsManaged();
            if (hirableList == null) return result;

            foreach (var template in hirableList)
            {
                if (template == null) continue;

                var info = GetTemplateInfo(GameObj<UnitLeaderTemplate>.Wrap(template.Pointer));
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetHirableLeaders: Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a unit leader template.
    /// </summary>
    public static UnitLeaderTemplateInfo GetTemplateInfo(GameObj<UnitLeaderTemplate> template)
    {
        if (template.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var info = new UnitLeaderTemplateInfo { Pointer = template.Untyped.Pointer };

            var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(template.Untyped.Pointer);
            if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id))
                info.TemplateId = id;

            // Get title (localized)
            if (_hUnitTitle.TryRead(template, out var titleObj))
            {
                var titleProxy = titleObj.AsManaged();
                if (titleProxy != null)
                    info.DisplayName = Il2CppUtils.ToManagedString(titleProxy.ToString()) ?? info.TemplateId;
            }

            info.HiringCost = _hHiringCosts.Read(template);
            info.Rarity = _hRarity.Read(template);
            info.MinCampaignProgress = _hMinCampaignProgress.Read(template);

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetTemplateInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Add a unit leader template to the hirable pool.
    /// </summary>
    public static bool AddHirableLeader(GameObj<UnitLeaderTemplate> template)
    {
        if (template.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.IsNull) return false;

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return false;

            var proxy = roster.AsManaged();
            if (proxy == null) return false;

            var templateType = _unitLeaderTemplateType.ManagedType;
            if (templateType == null) return false;

            var method = rosterType.GetMethod("AddHirableLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            var templateProxy = template.AsManaged();
            if (templateProxy == null) return false;

            method.Invoke(proxy, new[] { templateProxy });
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.AddHirableLeader: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Hire a unit leader from a template.
    /// </summary>
    public static GameObj<BaseUnitLeader> HireLeader(GameObj<UnitLeaderTemplate> template)
    {
        if (template.Untyped.CheckAlive() != AliveStatus.Alive) return default;

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.IsNull) return default;

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return default;

            var proxy = roster.AsManaged();
            if (proxy == null) return default;

            var templateType = _unitLeaderTemplateType.ManagedType;
            if (templateType == null) return default;

            var method = rosterType.GetMethod("HireLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return default;

            var templateProxy = template.AsManaged();
            if (templateProxy == null) return default;

            var result = method.Invoke(proxy, new[] { templateProxy });
            if (result == null) return default;

            return GameObj<BaseUnitLeader>.Wrap(((Il2CppObjectBase)result).Pointer);
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.HireLeader: Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Dismiss a hired unit leader.
    /// </summary>
    public static bool DismissLeader(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return default;

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.IsNull) return false;

            var rosterType = _rosterType?.ManagedType;
            var leaderType = _unitLeaderType?.ManagedType;
            if (rosterType == null || leaderType == null) return false;

            var rosterProxy = roster.AsManaged();
            var leaderProxy = leader.AsManaged();
            if (rosterProxy == null || leaderProxy == null) return false;

            var method = rosterType.GetMethod("TryDismissLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(rosterProxy, new[] { leaderProxy });
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.DismissLeader: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Find a hirable leader template by name.
    /// </summary>
    public static GameObj<UnitLeaderTemplate> FindHirableByTemplateId(string templateId)
    {
        try
        {
            var hirables = GetHirableLeaders();
            foreach (var h in hirables)
            {
                if (h.TemplateId == templateId)
                    return GameObj<UnitLeaderTemplate>.Wrap(h.Pointer);
            }
            return default;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.FindHirableByTemplateId: Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Find a hired leader by template name.
    /// </summary>
    public static GameObj<BaseUnitLeader> FindByTemplateId(string templateId)
    {
        try
        {
            var leaders = GetHiredLeaders();
            foreach (var l in leaders)
            {
                var leaderObj = GameObj<BaseUnitLeader>.Wrap(l.Pointer);
                if (!_hLeaderTemplate.TryRead(leaderObj, out var templateObj)) continue;
                var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer);
                if (!Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id)) continue;
                if (id == templateId)
                    return leaderObj;
            }
            return default;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.FindByTemplateId: Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Get the leader's template object.
    /// </summary>
    public static GameObj<UnitLeaderTemplate> GetLeaderTemplate(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return default;

        try
        {
            if (_hLeaderTemplate.TryRead(leader, out var template))
                return template;
            return default;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetLeaderTemplate: Failed", ex);
            return default;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Squaddie Management (Strategic Layer)
    // ═══════════════════════════════════════════════════════════════════

    public static List<SquaddieInfo> GetSquaddies(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<SquaddieInfo>();
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var ss = StrategyState.Get();
            if (ss == null) return result;

            var ssObj = GameObj<StrategyState>.Wrap(ss.Pointer);
            if (!_hSquaddies.TryRead(ssObj, out var squaddiesManager)) return result;
            if (squaddiesManager.Untyped.IsNull) return result;

            if (!_hSquaddieIds.TryRead(leader, out var squaddieIdsObj)) return result;

            var idList = squaddieIdsObj.AsManaged();
            if (idList == null) return result;

            var managerProxy = squaddiesManager.AsManaged();
            if (managerProxy == null) return result;

            foreach (var id in idList)
            {
                var squaddie = managerProxy.GetById(id);
                if (squaddie == null) continue;

                var info = GetSquaddieInfo(GameObj<Squaddie>.Wrap(squaddie.Pointer));
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetSquaddies: Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a squaddie.
    /// </summary>
    public static SquaddieInfo GetSquaddieInfo(GameObj<Squaddie> squaddie)
    {
        if (squaddie.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var info = new SquaddieInfo { Pointer = squaddie.Untyped.Pointer };

            if (_hSquaddieName.TryRead(squaddie, out var name))
                info.FirstName = name;

            if (_hSquaddieNickname.TryRead(squaddie, out var nickname))
                info.LastName = nickname;

            info.FullName = $"{info.FirstName} {info.LastName}".Trim();

            var proxy = squaddie.AsManaged();
            if (proxy != null)
                info.HomePlanet = proxy.GetHomePlanetName();

            return info;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetSquaddieInfo: Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Add a squaddie to a squad leader.
    /// </summary>
    public static bool AddSquaddie(GameObj<BaseUnitLeader> leader, GameObj<Squaddie> squaddie)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        if (squaddie.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var leaderProxy = leader.AsManaged();
            if (leaderProxy == null) return false;

            var squaddieProxy = squaddie.AsManaged();
            if (squaddieProxy == null) return false;

            return leaderProxy.TryAddSquaddie(squaddieProxy.GetId());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.AddSquaddie: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove a squaddie from a squad leader.
    /// </summary>
    public static bool RemoveSquaddie(GameObj<BaseUnitLeader> leader, GameObj<Squaddie> squaddie)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        if (squaddie.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var leaderProxy = leader.AsManaged();
            if (leaderProxy == null) return false;

            var squaddieProxy = squaddie.AsManaged();
            if (squaddieProxy == null) return false;

            return leaderProxy.TryRemoveSquaddie(squaddieProxy.GetId());
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.RemoveSquaddie: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get squaddie count for a leader.
    /// </summary>
    public static int GetSquaddieCount(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return 0;

        try
        {
            return GetSquaddies(leader).Count;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.GetSquaddieCount: Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Add a perk to a leader.
    /// </summary>
    public static bool AddPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perk)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        if (perk.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var leaderProxy = leader.AsManaged();
            if (leaderProxy == null) return false;

            var perkProxy = perk.AsManaged();
            if (perkProxy == null) return false;

            leaderProxy.AddPerk(perkProxy, true);
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.AddPerk: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove a perk from a leader.
    /// </summary>
    public static bool RemovePerk(GameObj<BaseUnitLeader> leader, string perkName)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;
        if (string.IsNullOrEmpty(perkName)) return false;

        try
        {
            if (!_hPerks.TryRead(leader, out var perkListObj)) return false;

            var perks = perkListObj.AsManaged();
            if (perks == null) return false;

            for (int i = 0; i < perks.Count; i++)
            {
                var perk = perks[i];
                if (perk == null) continue;

                var name = GameObj<PerkTemplate>.Wrap(perk.Pointer).Untyped.GetName();
                if (name?.Contains(perkName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    perks.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.RemovePerk: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Find a perk template by name.
    /// </summary>
    public static PerkTemplate FindPerk(string perkName)
    {
        if (string.IsNullOrEmpty(perkName)) return null;
        return GameQuery.FindByName<PerkTemplate>(perkName);
    }

    /// <summary>
    /// Heal a leader to full health.
    /// </summary>
    public static bool HealLeader(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var proxy = leader.AsManaged();
            if (proxy == null) return false;

            proxy.SetHealthStatus((byte)0);
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.HealLeader: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set a leader's availability status.
    /// </summary>
    public static bool SetLeaderAvailable(GameObj<BaseUnitLeader> leader, bool available)
    {
        if (leader.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            // Clear or set unavailability by writing directly to m_UnavailableDuration fields.
            // IsUnavailable() checks this struct; zero duration = available.
            _hUnavailableOperations.Write(leader, available ? 0 : 1);
            _hUnavailableMissions.Write(leader, 0);
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Roster.SetLeaderAvailable: Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Register console commands for Roster SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // roster - List all hired units
        DevConsole.RegisterCommand("roster", "", "List all hired units", args =>
        {
            var leaders = GetHiredLeaders();
            if (leaders.Count == 0)
                return "No hired units";

            var lines = new List<string> { $"Hired Units ({leaders.Count}):" };
            foreach (var l in leaders)
            {
                var status = l.IsDeployable ? "Ready" : (l.IsUnavailable ? "Unavailable" : "Busy");
                var squaddies = l.SquaddieCount > 0 ? $" (+{l.SquaddieCount} squaddies)" : "";
                lines.Add($"  {l.Nickname} - {l.RankName} ({l.PerkCount} perks) [{status}]{squaddies}");
            }
            return string.Join("\n", lines);
        });

        // unit <nickname> - Show unit info
        DevConsole.RegisterCommand("unit", "<nickname>", "Show unit information", args =>
        {
            if (args.Length == 0)
                return "Usage: unit <nickname>";

            var nickname = string.Join(" ", args);
            var leader = FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Unit '{nickname}' not found";

            var info = GetLeaderInfo(leader);
            if (info == null)
                return "Could not get unit info";

            var perks = GetPerks(leader);

            return $"Unit: {info.Nickname}\n" +
                   $"Template: {info.TemplateId}\n" +
                   $"Rank: {info.RankName} (Rank {info.Rank})\n" +
                   $"Health: {info.HealthPercent:P0}\n" +
                   $"Deploy Cost: {info.DeployCost}\n" +
                   $"Deployable: {info.IsDeployable}, Unavailable: {info.IsUnavailable}\n" +
                   $"Squaddies: {info.SquaddieCount}\n" +
                   $"Perks ({info.PerkCount}): {string.Join(", ", perks)}";
        });

        // available - Show available units count
        DevConsole.RegisterCommand("available", "", "Show available units count", args =>
        {
            var total = GetHiredCount();
            var available = GetAvailableCount();
            return $"Available: {available}/{total} units ready for deployment";
        });

        // hirable - List hirable leaders
        DevConsole.RegisterCommand("hirable", "", "List available leaders for hire", args =>
        {
            var hirables = GetHirableLeaders();
            if (hirables.Count == 0)
                return "No leaders available for hire";

            var lines = new List<string> { $"Available for Hire ({hirables.Count}):" };
            foreach (var h in hirables)
            {
                var name = !string.IsNullOrEmpty(h.DisplayName) ? h.DisplayName : h.TemplateId;
                var rarity = h.Rarity > 0 ? $" (Rarity: {h.Rarity}%)" : "";
                lines.Add($"  {name}{rarity}");
            }
            return string.Join("\n", lines);
        });

        // hire <template> - Hire a leader
        DevConsole.RegisterCommand("hire", "<template>", "Hire a leader by template ID", args =>
        {
            if (args.Length == 0)
                return "Usage: hire <template>";

            var templateId = string.Join(" ", args);
            var template = FindHirableByTemplateId(templateId);
            if (template.Untyped.CheckAlive() != AliveStatus.Alive)
                return $"Template '{templateId}' not found in hire pool";

            var hired = HireLeader(template);
            if (hired.Untyped.CheckAlive() != AliveStatus.Alive)
                return "Failed to hire leader";

            var info = GetLeaderInfo(hired);
            return $"Hired: {info?.Nickname ?? "Unknown"}";
        });

        // dismiss <nickname> - Dismiss a leader
        DevConsole.RegisterCommand("dismiss", "<nickname>", "Dismiss a hired leader", args =>
        {
            if (args.Length == 0)
                return "Usage: dismiss <nickname>";

            var nickname = string.Join(" ", args);
            var leader = FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var info = GetLeaderInfo(leader);
            if (DismissLeader(leader))
                return $"Dismissed: {info?.Nickname ?? nickname}";
            else
                return "Failed to dismiss leader";
        });

        // squaddies <nickname> - List squaddies for a leader
        DevConsole.RegisterCommand("squaddies", "<nickname>", "List squaddies for a leader", args =>
        {
            if (args.Length == 0)
                return "Usage: squaddies <nickname>";

            var nickname = string.Join(" ", args);
            var leader = FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var squaddies = GetSquaddies(leader);
            if (squaddies.Count == 0)
                return $"{nickname} has no squaddies";

            var lines = new List<string> { $"{nickname}'s Squaddies ({squaddies.Count}):" };
            foreach (var s in squaddies)
            {
                var homeInfo = !string.IsNullOrEmpty(s.HomePlanet) ? $" (from {s.HomePlanet})" : "";
                lines.Add($"  {s.FullName}{homeInfo}");
            }
            return string.Join("\n", lines);
        });

        // healleader <nickname> - Heal a leader to full
        DevConsole.RegisterCommand("healleader", "<nickname>", "Heal a leader to full health", args =>
        {
            if (args.Length == 0)
                return "Usage: healleader <nickname>";

            var nickname = string.Join(" ", args);
            var leader = FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var infoBefore = GetLeaderInfo(leader);
            if (HealLeader(leader))
            {
                var infoAfter = GetLeaderInfo(leader);
                return $"Healed {nickname}: {infoBefore?.HealthPercent:P0} -> {infoAfter?.HealthPercent:P0}";
            }
            return "Failed to heal leader";
        });

        // addperk <nickname> <perk> - Add a perk to a leader
        DevConsole.RegisterCommand("addperk", "<nickname> <perk>", "Add a perk to a leader", args =>
        {
            if (args.Length < 2)
                return "Usage: addperk <nickname> <perk_name>";

            var nickname = args[0];
            var perkName = string.Join(" ", args.Skip(1));

            var leader = FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            var perk = FindPerk(perkName);
            if (perk == null)
                return $"Perk '{perkName}' not found";

            if (AddPerk(leader, GameObj<PerkTemplate>.Wrap(perk.Pointer)))
                return $"Added perk '{perkName}' to {nickname}";
            return "Failed to add perk";
        });

        // removeperk <nickname> <perk> - Remove a perk from a leader
        DevConsole.RegisterCommand("removeperk", "<nickname> <perk>", "Remove a perk from a leader", args =>
        {
            if (args.Length < 2)
                return "Usage: removeperk <nickname> <perk_name>";

            var nickname = args[0];
            var perkName = string.Join(" ", args.Skip(1));

            var leader = FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            if (RemovePerk(leader, perkName))
                return $"Removed perk '{perkName}' from {nickname}";
            return "Failed to remove perk (perk not found?)";
        });

        // setavailable <nickname> <true/false> - Set leader availability
        DevConsole.RegisterCommand("setavailable", "<nickname> <true/false>", "Set leader availability", args =>
        {
            if (args.Length < 2)
                return "Usage: setavailable <nickname> <true/false>";

            var nickname = args[0];
            var leader = FindByNicknameTyped(nickname);
            if (leader.Untyped.IsNull)
                return $"Leader '{nickname}' not found";

            if (!bool.TryParse(args[1], out var available))
                return "Second argument must be 'true' or 'false'";

            if (SetLeaderAvailable(leader, available))
                return $"Set {nickname} availability to {available}";
            return "Failed to set availability";
        });
    }
}
