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
    // Cached types
    private static readonly GameType _perkTemplateType = GameType.Of<Il2CppMenace.Strategy.PerkTemplate>();
    private static readonly GameType _unitLeaderTemplateType = GameType.Of<Il2CppMenace.Strategy.UnitLeaderTemplate>();
    private static readonly GameType _rosterType = GameType.Of<Il2CppMenace.Strategy.Roster>();
    private static readonly GameType _unitLeaderType = GameType.Of<Il2CppMenace.Strategy.BaseUnitLeader>();
    private static readonly GameType _squaddieType = GameType.Of<Il2CppMenace.Strategy.Squaddie>();
    private static readonly GameType _strategyStateType = GameType.Of<Il2CppMenace.States.StrategyState>();

    private static class Offsets
    {
        // StrategyState
        internal static readonly Lazy<ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.Roster>> Roster
            = new(() => GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.Roster));

        internal static readonly Lazy<ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.Squaddies>> Squaddies
            = new(() => GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.Squaddies));

        // Roster
        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.Roster, IntPtr>> HiredLeaders
            = new(() => GameObj<Il2CppMenace.Strategy.Roster>.FieldAt<IntPtr>(0x10, "m_HiredLeaders"));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.Roster, IntPtr>> HirableLeaders
            = new(() => GameObj<Il2CppMenace.Strategy.Roster>.FieldAt<IntPtr>(0x18, "m_HirableLeaders"));

        // BaseUnitLeader
        internal static readonly Lazy<ObjFieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, Il2CppMenace.Strategy.UnitLeaderTemplate>> LeaderTemplate
            = new(() => GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.ResolveObjField(x => x.LeaderTemplate));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, IntPtr>> Perks
            = new(() => GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.FieldAt<IntPtr>(0x48, "m_Perks"));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, IntPtr>> SquaddieIds
            = new(() => GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.FieldAt<IntPtr>(0x60, "m_SquaddieIds"));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, int>> UnavailableOperations
            = new(() => GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.FieldAt<int>(0x68, "m_UnavailableDuration.Operations"));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.BaseUnitLeader, int>> UnavailableMissions
            = new(() => GameObj<Il2CppMenace.Strategy.BaseUnitLeader>.FieldAt<int>(0x6C, "m_UnavailableDuration.Missions"));

        // UnitLeaderTemplate
        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, IntPtr>> UnitTitle
            = new(() => GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.FieldAt<IntPtr>(0x88, "UnitTitle"));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, int>> HiringCosts
            = new(() => GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.ResolveField(x => x.HiringCosts));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, int>> Rarity
            = new(() => GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.ResolveField(x => x.Rarity));

        internal static readonly Lazy<FieldHandle<Il2CppMenace.Strategy.UnitLeaderTemplate, int>> MinCampaignProgress
            = new(() => GameObj<Il2CppMenace.Strategy.UnitLeaderTemplate>.ResolveField(x => x.MinCampaignProgress));

        // Squaddie
        internal static readonly Lazy<StringFieldHandle<Il2CppMenace.Strategy.Squaddie>> Name
            = new(() => GameObj<Il2CppMenace.Strategy.Squaddie>.ResolveStringField(x => x.Name));

        internal static readonly Lazy<StringFieldHandle<Il2CppMenace.Strategy.Squaddie>> Nickname
            = new(() => GameObj<Il2CppMenace.Strategy.Squaddie>.ResolveStringField(x => x.Nickname));
    }

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
        public string TemplateName { get; set; }
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
        public string TemplateName { get; set; }
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
            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return default;

            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var ss = getMethod?.Invoke(null, null);
            if (ss == null) return default;

            var ssObj = GameObj<StrategyState>.Wrap(((Il2CppObjectBase)ss).Pointer);
            return Offsets.Roster.Value.Read(ssObj);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetRoster", "Failed", ex);
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
            if (roster.Untyped.IsNull) return result;

            var hiredListPtr = Offsets.HiredLeaders.Value.Read(roster);
            if (hiredListPtr == IntPtr.Zero) return result;

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return result;

            var listGenericType = typeof(Il2CppSystem.Collections.Generic.List<>);
            var listTyped = listGenericType.MakeGenericType(leaderType);
            var ptrCtor = listTyped.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null) return result;

            var hiredList = ptrCtor.Invoke(new object[] { hiredListPtr });
            if (hiredList == null) return result;

            var countProp = listTyped.GetProperty("Count");
            var indexer = listTyped.GetMethod("get_Item");

            int count = (int)countProp.GetValue(hiredList);
            for (int i = 0; i < count; i++)
            {
                var leader = indexer.Invoke(hiredList, new object[] { i });
                if (leader == null) continue;

                var info = GetLeaderInfo(GameObj<BaseUnitLeader>.Wrap(((Il2CppObjectBase)leader).Pointer));
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
            ModError.ReportInternal("Roster.GetHiredLeaders", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a unit leader.
    /// </summary>
    public static UnitLeaderInfo GetLeaderInfo(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return null;

        try
        {
            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return null;

            var proxy = Il2CppUtils.GetManagedProxy(leader.Untyped.Pointer, leaderType);
            if (proxy == null) return null;

            var info = new UnitLeaderInfo { Pointer = leader.Untyped.Pointer };

            // Get template name using LeaderTemplate field at offset +0x10
            if (Offsets.LeaderTemplate.Value.TryRead(leader, out var templateObj))
                info.TemplateName = templateObj.Untyped.GetName();

            // Get nickname - use ToManagedString to properly handle IL2CPP strings
            var getNicknameMethod = leaderType.GetMethod("GetNickname", BindingFlags.Public | BindingFlags.Instance);
            if (getNicknameMethod != null)
                info.Nickname = Il2CppUtils.ToManagedString(getNicknameMethod.Invoke(proxy, null));

            // Get rank
            var getRankMethod = leaderType.GetMethod("GetRank", BindingFlags.Public | BindingFlags.Instance);
            if (getRankMethod != null)
                info.Rank = (int)getRankMethod.Invoke(proxy, null);

            var getRankTemplateMethod = leaderType.GetMethod("GetRankTemplate", BindingFlags.Public | BindingFlags.Instance);
            var rankTemplate = getRankTemplateMethod?.Invoke(proxy, null);
            if (rankTemplate != null)
            {
                info.RankName = GameObj<UnitRankTemplate>.Wrap(((Il2CppObjectBase)rankTemplate).Pointer).Untyped.GetName();
            }

            // Get perk count
            var getPerkCountMethod = leaderType.GetMethod("GetPerkCount", BindingFlags.Public | BindingFlags.Instance);
            if (getPerkCountMethod != null)
                info.PerkCount = (int)getPerkCountMethod.Invoke(proxy, null);

            // Get health
            var getHealthMethod = leaderType.GetMethod("GetHitpointsPct", BindingFlags.Public | BindingFlags.Instance);
            if (getHealthMethod != null)
                info.HealthPercent = (float)getHealthMethod.Invoke(proxy, null);

            // Get status flags
            var isDeployableMethod = leaderType.GetMethod("IsDeployable", BindingFlags.Public | BindingFlags.Instance);
            if (isDeployableMethod != null)
                info.IsDeployable = (bool)isDeployableMethod.Invoke(proxy, null);

            var isUnavailableMethod = leaderType.GetMethod("IsUnavailable", BindingFlags.Public | BindingFlags.Instance);
            if (isUnavailableMethod != null)
                info.IsUnavailable = (bool)isUnavailableMethod.Invoke(proxy, null);

            // Get deploy cost - GetDeployCosts returns OperationResources, not int
            // For now, skip this as it requires parsing the OperationResources struct
            // TODO: Parse OperationResources to get total deploy cost

            // Get squaddie count (if SquadLeader) using m_Squaddies field
            // Note: m_Squaddies does not exist on BaseUnitLeader — actual field is m_SquaddieIds (List<int>)
            // This reflection call silently returns null; SquaddieCount remains 0. Pre-existing bug, preserved.
            try
            {
                var squaddiesField = proxy.GetType().GetField("m_Squaddies", BindingFlags.NonPublic | BindingFlags.Instance);
                var squaddies = squaddiesField?.GetValue(proxy);
                if (squaddies != null)
                {
                    var countProp = squaddies.GetType().GetProperty("Count");
                    info.SquaddieCount = (int)(countProp?.GetValue(squaddies) ?? 0);
                }
            }
            catch { }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetLeaderInfo", "Failed", ex);
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
        if (leader.Untyped.IsNull) return result;

        try
        {
            var perksPtr = Offsets.Perks.Value.Read(leader);
            if (perksPtr == IntPtr.Zero) return result;

            var perkTemplateType = _perkTemplateType.ManagedType;
            if (perkTemplateType == null) return result;

            var (perks, listType) = GetTypedList(perksPtr, perkTemplateType);
            if (perks == null) return result;

            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(perks);
            for (int i = 0; i < count; i++)
            {
                var perk = indexer.Invoke(perks, new object[] { i });
                if (perk == null) continue;

                var perkObj = GameObj<PerkTemplate>.Wrap(((Il2CppObjectBase)perk).Pointer);
                result.Add(perkObj.Untyped.GetName() ?? $"Perk {i}");
            }

            return result;
        }
        catch
        {
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
            if (roster.Untyped.IsNull) return result;

            var hirableListPtr = Offsets.HirableLeaders.Value.Read(roster);
            if (hirableListPtr == IntPtr.Zero) return result;

            var templateType = _unitLeaderTemplateType.ManagedType;
            if (templateType == null) return result;

            var (hirableList, listType) = GetTypedList(hirableListPtr, templateType);
            if (hirableList == null) return result;

            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(hirableList);
            for (int i = 0; i < count; i++)
            {
                var template = indexer.Invoke(hirableList, new object[] { i });
                if (template == null) continue;

                var templateObj = GameObj<UnitLeaderTemplate>.Wrap(((Il2CppObjectBase)template).Pointer);
                var info = GetTemplateInfo(templateObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetHirableLeaders", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a unit leader template.
    /// </summary>
    public static UnitLeaderTemplateInfo GetTemplateInfo(GameObj<UnitLeaderTemplate> template)
    {
        if (template.Untyped.IsNull) return null;

        try
        {
            var info = new UnitLeaderTemplateInfo
            {
                Pointer = template.Untyped.Pointer,
                TemplateName = template.Untyped.GetName()
            };

            // Get title (localized)
            var titlePtr = Offsets.UnitTitle.Value.Read(template);
            if (titlePtr != IntPtr.Zero)
            {
                var title = GameObj.FromPointer(titlePtr);
                var titleType = title.GetGameType()?.ManagedType;
                var getText = titleType?.GetMethod("ToString",
                                    BindingFlags.Public | BindingFlags.Instance);
                if (getText != null)
                {
                    var proxy = GetManagedProxy(title.Pointer, titleType);
                    info.DisplayName = Il2CppUtils.ToManagedString(getText.Invoke(proxy, null))
                                       ?? info.TemplateName;
                }
            }

            // Get hiring costs
            info.HiringCost = Offsets.HiringCosts.Value.Read(template);

            // Get rarity
            info.Rarity = Offsets.Rarity.Value.Read(template);

            // Get min campaign progress
            info.MinCampaignProgress = Offsets.MinCampaignProgress.Value.Read(template);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetTemplateInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Add a unit leader template to the hirable pool.
    /// </summary>
    public static bool AddHirableLeader(GameObj<UnitLeaderTemplate> template)
    {
        if (template.Untyped.IsNull) return false;

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.IsNull) return false;

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return false;

            var proxy = GetManagedProxy(roster.Untyped.Pointer, rosterType);
            if (proxy == null) return false;

            var templateType = _unitLeaderTemplateType.ManagedType;
            if (templateType == null) return false;

            var method = rosterType.GetMethod("AddHirableLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            var templateProxy = GetManagedProxy(template.Untyped.Pointer, templateType);
            if (templateProxy == null) return false;

            method.Invoke(proxy, new[] { templateProxy });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.AddHirableLeader", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Hire a unit leader from a template.
    /// </summary>
    public static GameObj<BaseUnitLeader> HireLeader(GameObj<UnitLeaderTemplate> template)
    {
        if (template.Untyped.IsNull) return default;

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.IsNull) return default;

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return default;

            var proxy = GetManagedProxy(roster.Untyped.Pointer, rosterType);
            if (proxy == null) return default;

            var templateType = _unitLeaderTemplateType.ManagedType;
            if (templateType == null) return default;

            var method = rosterType.GetMethod("HireLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return default;

            var templateProxy = GetManagedProxy(template.Untyped.Pointer, templateType);
            if (templateProxy == null) return default;

            var result = method.Invoke(proxy, new[] { templateProxy });
            if (result == null) return default;

            return GameObj<BaseUnitLeader>.Wrap(((Il2CppObjectBase)result).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.HireLeader", "Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Dismiss a hired unit leader.
    /// </summary>
    public static bool DismissLeader(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return false;

        try
        {
            var roster = GetRoster();
            if (roster.Untyped.IsNull) return false;

            var rosterType = _rosterType?.ManagedType;
            var leaderType = _unitLeaderType?.ManagedType;
            if (rosterType == null || leaderType == null) return false;

            var rosterProxy = GetManagedProxy(roster.Untyped.Pointer, rosterType);
            var leaderProxy = GetManagedProxy(leader.Untyped.Pointer, leaderType);
            if (rosterProxy == null || leaderProxy == null) return false;

            var method = rosterType.GetMethod("TryDismissLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(rosterProxy, new[] { leaderProxy });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.DismissLeader", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Find a hirable leader template by name.
    /// </summary>
    public static GameObj<UnitLeaderTemplate> FindHirableByName(string templateName)
    {
        try
        {
            var hirables = GetHirableLeaders();
            foreach (var h in hirables)
            {
                if (h.TemplateName?.Contains(templateName, StringComparison.OrdinalIgnoreCase) == true)
                    return GameObj<UnitLeaderTemplate>.Wrap(h.Pointer);
            }
            return default;
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Find a hired leader by template name.
    /// </summary>
    public static GameObj<BaseUnitLeader> FindByTemplateName(string templateName)
    {
        try
        {
            var leaders = GetHiredLeaders();
            foreach (var l in leaders)
            {
                if (l.TemplateName?.Contains(templateName, StringComparison.OrdinalIgnoreCase) == true)
                    return GameObj<BaseUnitLeader>.Wrap(l.Pointer);
            }
            return default;
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Get the leader's template object.
    /// </summary>
    public static GameObj<UnitLeaderTemplate> GetLeaderTemplate(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return default;

        try
        {
            if (Offsets.LeaderTemplate.Value.TryRead(leader, out var template))
                return template;
            return default;
        }
        catch
        {
            return default;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Squaddie Management (Strategic Layer)
    // ═══════════════════════════════════════════════════════════════════

    public static List<SquaddieInfo> GetSquaddies(GameObj<BaseUnitLeader> leader)
    {
        var result = new List<SquaddieInfo>();
        if (leader.Untyped.IsNull) return result;

        try
        {
            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return result;

            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var ss = getMethod?.Invoke(null, null);
            if (ss == null) return result;

            var ssObj = GameObj<StrategyState>.Wrap(((Il2CppObjectBase)ss).Pointer);
            var squaddiesManager = Offsets.Squaddies.Value.Read(ssObj);
            if (squaddiesManager.Untyped.IsNull) return result;

            var squaddiesType = _squaddieType?.ManagedType;
            if (squaddiesType == null) return result;

            var squaddieIdsPtr = Offsets.SquaddieIds.Value.Read(leader);
            if (squaddieIdsPtr == IntPtr.Zero) return result;

            var listGenericType = typeof(Il2CppSystem.Collections.Generic.List<>)
                .MakeGenericType(typeof(int));
            var ptrCtor = listGenericType.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null) return result;

            var idList = ptrCtor.Invoke(new object[] { squaddieIdsPtr });
            if (idList == null) return result;

            var countProp = listGenericType.GetProperty("Count");
            var indexer = listGenericType.GetMethod("get_Item");
            int count = (int)countProp.GetValue(idList);

            var squaddiesManagerType = squaddiesManager.Untyped.GetGameType()?.ManagedType;
            if (squaddiesManagerType == null) return result;

            var getById = squaddiesManagerType.GetMethod("GetById", BindingFlags.Public | BindingFlags.Instance);
            if (getById == null) return result;

            var managerProxy = Il2CppUtils.GetManagedProxy(squaddiesManager.Untyped.Pointer, squaddiesManagerType);
            if (managerProxy == null) return result;

            for (int i = 0; i < count; i++)
            {
                var id = (int)indexer.Invoke(idList, new object[] { i });
                var squaddie = getById.Invoke(managerProxy, new object[] { id });
                if (squaddie == null) continue;

                var squaddieObj = GameObj<Squaddie>.Wrap(((Il2CppObjectBase)squaddie).Pointer);
                var info = GetSquaddieInfo(squaddieObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetSquaddies", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a squaddie.
    /// </summary>
    public static SquaddieInfo GetSquaddieInfo(GameObj<Squaddie> squaddie)
    {
        if (squaddie.Untyped.IsNull) return null;

        try
        {
            var info = new SquaddieInfo { Pointer = squaddie.Untyped.Pointer };

            if (Offsets.Name.Value.TryRead(squaddie, out var name))
                info.FirstName = name;

            if (Offsets.Nickname.Value.TryRead(squaddie, out var nickname))
                info.LastName = nickname;

            info.FullName = $"{info.FirstName} {info.LastName}".Trim();

            var squaddieType = _squaddieType?.ManagedType;
            if (squaddieType != null)
            {
                var proxy = Il2CppUtils.GetManagedProxy(squaddie.Untyped.Pointer, squaddieType);
                if (proxy != null)
                {
                    var getHomePlanetName = squaddieType.GetMethod("GetHomePlanetName",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (getHomePlanetName != null)
                        info.HomePlanet = getHomePlanetName.Invoke(proxy, null) as string;
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetSquaddieInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Add a squaddie to a squad leader.
    /// </summary>
    public static bool AddSquaddie(GameObj<BaseUnitLeader> leader, GameObj<Squaddie> squaddie)
    {
        if (leader.Untyped.IsNull || squaddie.Untyped.IsNull) return false;

        try
        {
            var leaderType = _unitLeaderType?.ManagedType;
            var squaddieType = _squaddieType?.ManagedType;
            if (leaderType == null || squaddieType == null) return false;

            var leaderProxy = Il2CppUtils.GetManagedProxy(leader.Untyped.Pointer, leaderType);
            if (leaderProxy == null) return false;

            var squaddieProxy = Il2CppUtils.GetManagedProxy(squaddie.Untyped.Pointer, squaddieType);
            if (squaddieProxy == null) return false;

            var getId = squaddieType.GetMethod("GetId", BindingFlags.Public | BindingFlags.Instance);
            if (getId == null) return false;

            var squaddieId = (int)getId.Invoke(squaddieProxy, null);

            var method = leaderType.GetMethod("TryAddSquaddie", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(leaderProxy, new object[] { squaddieId });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.AddSquaddie", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove a squaddie from a squad leader.
    /// </summary>
    public static bool RemoveSquaddie(GameObj<BaseUnitLeader> leader, GameObj<Squaddie> squaddie)
    {
        if (leader.Untyped.IsNull || squaddie.Untyped.IsNull) return false;

        try
        {
            var leaderType = _unitLeaderType?.ManagedType;
            var squaddieType = _squaddieType?.ManagedType;
            if (leaderType == null || squaddieType == null) return false;

            var leaderProxy = Il2CppUtils.GetManagedProxy(leader.Untyped.Pointer, leaderType);
            if (leaderProxy == null) return false;

            var squaddieProxy = Il2CppUtils.GetManagedProxy(squaddie.Untyped.Pointer, squaddieType);
            if (squaddieProxy == null) return false;

            var getId = squaddieType.GetMethod("GetId", BindingFlags.Public | BindingFlags.Instance);
            if (getId == null) return false;

            var squaddieId = (int)getId.Invoke(squaddieProxy, null);

            var method = leaderType.GetMethod("TryRemoveSquaddie", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(leaderProxy, new object[] { squaddieId });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.RemoveSquaddie", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get squaddie count for a leader.
    /// </summary>
    public static int GetSquaddieCount(GameObj<BaseUnitLeader> leader)
    {
        if (leader.Untyped.IsNull) return 0;

        try
        {
            var squaddies = GetSquaddies(leader);
            return squaddies.Count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Add a perk to a leader.
    /// </summary>
    public static bool AddPerk(GameObj<BaseUnitLeader> leader, GameObj<PerkTemplate> perk)
    {
        if (leader.Untyped.IsNull || perk.Untyped.IsNull) return false;

        try
        {
            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return false;

            var leaderProxy = Il2CppUtils.GetManagedProxy(leader.Untyped.Pointer, leaderType);
            if (leaderProxy == null) return false;

            var perkTemplateType = _perkTemplateType.ManagedType;
            if (perkTemplateType == null) return false;

            var perkProxy = Il2CppUtils.GetManagedProxy(perk.Untyped.Pointer, perkTemplateType);
            if (perkProxy == null) return false;

            var method = leaderType.GetMethod("AddPerk", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            method.Invoke(leaderProxy, new object[] { perkProxy, true });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.AddPerk", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove a perk from a leader.
    /// </summary>
    public static bool RemovePerk(GameObj<BaseUnitLeader> leader, string perkName)
    {
        if (leader.Untyped.IsNull || string.IsNullOrEmpty(perkName)) return false;

        try
        {
            var perksPtr = Offsets.Perks.Value.Read(leader);
            if (perksPtr == IntPtr.Zero) return false;

            var perkTemplateType = _perkTemplateType.ManagedType;
            if (perkTemplateType == null) return false;

            var (perks, listType) = GetTypedList(perksPtr, perkTemplateType);
            if (perks == null) return false;

            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");
            var removeAtMethod = listType.GetMethod("RemoveAt");

            int count = (int)countProp.GetValue(perks);
            for (int i = 0; i < count; i++)
            {
                var perk = indexer.Invoke(perks, new object[] { i });
                if (perk == null) continue;

                var perkObj = GameObj<PerkTemplate>.Wrap(((Il2CppObjectBase)perk).Pointer);
                var name = perkObj.Untyped.GetName();
                if (name?.Contains(perkName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    removeAtMethod?.Invoke(perks, new object[] { i });
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.RemovePerk", "Failed", ex);
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
        if (leader.Untyped.IsNull) return false;

        try
        {
            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return false;

            var proxy = Il2CppUtils.GetManagedProxy(leader.Untyped.Pointer, leaderType);
            if (proxy == null) return false;

            var method = leaderType.GetMethod("SetHealthStatus", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            method.Invoke(proxy, new object[] { (byte)0 });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.HealLeader", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set a leader's availability status.
    /// </summary>
    public static bool SetLeaderAvailable(GameObj<BaseUnitLeader> leader, bool available)
    {
        if (leader.Untyped.IsNull) return false;

        try
        {
            // Clear or set unavailability by writing directly to m_UnavailableDuration fields.
            // IsUnavailable() checks this struct; zero duration = available.
            var ops = available ? 0 : 1;
            Offsets.UnavailableOperations.Value.Write(leader, ops);
            Offsets.UnavailableMissions.Value.Write(leader, 0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.SetLeaderAvailable", "Failed", ex);
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
                   $"Template: {info.TemplateName}\n" +
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
                var name = !string.IsNullOrEmpty(h.DisplayName) ? h.DisplayName : h.TemplateName;
                var rarity = h.Rarity > 0 ? $" (Rarity: {h.Rarity}%)" : "";
                lines.Add($"  {name}{rarity}");
            }
            return string.Join("\n", lines);
        });

        // hire <template> - Hire a leader
        DevConsole.RegisterCommand("hire", "<template>", "Hire a leader by template name", args =>
        {
            if (args.Length == 0)
                return "Usage: hire <template>";

            var templateName = string.Join(" ", args);
            var template = FindHirableByName(templateName);
            if (template.Untyped.IsNull)
                return $"Template '{templateName}' not found in hire pool";

            var hired = HireLeader(template);
            if (hired.Untyped.IsNull)
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

    // --- Internal helpers ---

    private static object GetManagedProxy(IntPtr pointer, Type managedType)
        => Il2CppUtils.GetManagedProxy(pointer, managedType);

    /// <summary>
    /// Get a typed IL2CPP list from a pointer. Works around GameObj.ToManaged() failing for generic types.
    /// </summary>
    private static (object list, Type listType) GetTypedList(IntPtr listPtr, Type elementType)
    {
        if (listPtr == IntPtr.Zero || elementType == null) return (null, null);

        try
        {
            var listGenericType = typeof(Il2CppSystem.Collections.Generic.List<>);
            var listTyped = listGenericType.MakeGenericType(elementType);
            var ptrCtor = listTyped.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null) return (null, null);

            var list = ptrCtor.Invoke(new object[] { listPtr });
            return (list, listTyped);
        }
        catch
        {
            return (null, null);
        }
    }
}
