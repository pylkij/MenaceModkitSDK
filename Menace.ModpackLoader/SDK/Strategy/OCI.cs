using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;

using Il2CppMenace.Strategy;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for Orbital Command Interface (OCI) - Ship Upgrades.
/// Provides access to ship upgrade management, slots, and permanent upgrades.
///
/// Game Model:
///   Ship → Slots (Armament, Electronics, Hull) → Upgrades
///   Each slot can hold upgrades, with levels and amounts.
///   OCI Points are spent to install upgrades.
///
/// Based on reverse engineering:
/// - StrategyState → ShipUpgrades @ ProcessSaveState order
/// - ShipUpgrades.m_SlotOverrides, m_PermanentUpgrades, m_SlotLevels, m_UpgradeAmounts
/// - ShipUpgradeTemplate: OciPointsCosts, UpgradeType, UnlockType, UnlockedByFaction
/// </summary>
public static class OCI
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // ShipUpgrades fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.ShipUpgrades, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppMenace.Strategy.ShipUpgradeTemplate>> _hEquippedUpgrades;
    private static ObjFieldHandle<Il2CppMenace.Strategy.ShipUpgrades, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppMenace.Strategy.ShipUpgradeSlotTemplate>> _hSlotTypes;
    private static ObjFieldHandle<Il2CppMenace.Strategy.ShipUpgrades, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>> _hSpentOciComponentsBySlot;
    private static ObjFieldHandle<Il2CppMenace.Strategy.ShipUpgrades, Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Strategy.ShipUpgradeTemplate, int>> _hUnlockedByGameEffects;

    // ShipUpgradeTemplate fields
    private static FieldHandle<Il2CppMenace.Strategy.ShipUpgradeTemplate, Il2CppMenace.Strategy.ShipUpgradeType> _hUpgradeType;
    private static FieldHandle<Il2CppMenace.Strategy.ShipUpgradeTemplate, int> _hOciPointsCosts;
    private static FieldHandle<Il2CppMenace.Strategy.ShipUpgradeTemplate, Il2CppMenace.Strategy.ShipUpgradeUnlockType> _hUnlockType;
    private static FieldHandle<Il2CppMenace.Strategy.ShipUpgradeTemplate, Il2CppMenace.Strategy.StoryFactionType> _hUnlockedByFaction;

    // ShipUpgradeSlotTemplate fields
    private static FieldHandle<Il2CppMenace.Strategy.ShipUpgradeSlotTemplate, Il2CppMenace.Strategy.ShipUpgradeType> _hSlotUpgradeType;

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
            _hEquippedUpgrades = GameObj<Il2CppMenace.Strategy.ShipUpgrades>.ResolveObjField(x => x.m_EquippedUpgrades);
            _hSlotTypes = GameObj<Il2CppMenace.Strategy.ShipUpgrades>.ResolveObjField(x => x.m_SlotTypes);
            _hSpentOciComponentsBySlot = GameObj<Il2CppMenace.Strategy.ShipUpgrades>.ResolveObjField(x => x.m_SpentOciComponentsBySlot);
            _hUnlockedByGameEffects = GameObj<Il2CppMenace.Strategy.ShipUpgrades>.ResolveObjField(x => x.m_UnlockedByGameEffects);

            _hUpgradeType = GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.ResolveField(x => x.UpgradeType);
            _hOciPointsCosts = GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.ResolveField(x => x.OciPointsCosts);
            _hUnlockType = GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.ResolveField(x => x.UnlockType);
            _hUnlockedByFaction = GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.ResolveField(x => x.UnlockedByFaction);

            _hSlotUpgradeType = GameObj<Il2CppMenace.Strategy.ShipUpgradeSlotTemplate>.ResolveField(x => x.UpgradeType);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.ResolveHandles", "Field handle resolution failed", ex);
        }
    }

    public class UpgradeInfo
    {
        public string TemplateId { get; set; }
        public ShipUpgradeType UpgradeType { get; set; }
        public ShipUpgradeUnlockType UnlockType { get; set; }
        public StoryFactionType UnlockedByFaction { get; set; }
        public int OciPointsCost { get; set; }
        public bool IsInstalled { get; set; }
        public bool IsNew { get; set; }
        public IntPtr Pointer { get; set; }
    }

    public class SlotInfo
    {
        public string TemplateId { get; set; }
        public ShipUpgradeType SlotType { get; set; }
        public UpgradeInfo EquippedUpgrade { get; set; }
        public IntPtr Pointer { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Core Accessors
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the ShipUpgrades instance from StrategyState.
    /// </summary>
    public static GameObj GetShipUpgrades()
    {
        try
        {
            var ss = Il2CppMenace.States.StrategyState.Get();
            if (ss == null) return GameObj.Null;

            var ssObj = new GameObj(((Il2CppObjectBase)ss).Pointer);
            if (ssObj.CheckAlive() != AliveStatus.Alive) return GameObj.Null;

            var suPtr = ssObj.ReadPtr(0xA0);
            if (suPtr == IntPtr.Zero) return GameObj.Null;

            var su = new GameObj(suPtr);
            if (su.CheckAlive() != AliveStatus.Alive) return GameObj.Null;

            return su;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetShipUpgrades", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get current OCI points available.
    /// </summary>
    public static int GetOciComponents()
    {
        try
        {
            var ss = Il2CppMenace.States.StrategyState.Get();
            if (ss == null) return 0;

            return GameMethod.CallInt<Il2CppMenace.States.StrategyState>(ss, x => x.GetVar(StrategyVars.OciComponents));
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetOciComponents", "Failed", ex);
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Upgrade Queries
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all available ship upgrade templates.
    /// </summary>
    public static List<UpgradeInfo> GetAllUpgradeTemplates()
    {
        var result = new List<UpgradeInfo>();

        try
        {
            var templates = Templates.FindAll<Il2CppMenace.Strategy.ShipUpgradeTemplate>();
            foreach (var t in templates)
            {
                var info = GetUpgradeInfo(new GameObj(t.Pointer));
                if (info != null)
                    result.Add(info);
            }
            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetAllUpgradeTemplates", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get installed upgrades (permanent upgrades).
    /// </summary>
    public static List<UpgradeInfo> GetInstalledUpgrades()
    {
        var result = new List<UpgradeInfo>();

        try
        {
            var su = GetShipUpgrades();
            if (su.CheckAlive() != AliveStatus.Alive) return result;

            if (!GameObj<Il2CppMenace.Strategy.ShipUpgrades>.TryWrap(su, out var suTyped)) return result;
            if (!_hEquippedUpgrades.TryRead(suTyped, out var equippedObj)) return result;

            var arr = equippedObj.AsManaged();
            if (arr == null) return result;

            for (int i = 0; i < arr.Length; i++)
            {
                var t = arr[i];
                if (t == null) continue;

                var info = GetUpgradeInfo(new GameObj(t.Pointer));
                if (info != null)
                {
                    info.IsInstalled = true;
                    result.Add(info);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetInstalledUpgrades", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get available upgrades (can be installed).
    /// </summary>
    public static List<UpgradeInfo> GetAvailableUpgrades()
    {
        var result = new List<UpgradeInfo>();

        try
        {
            var templates = Templates.FindAll<Il2CppMenace.Strategy.ShipUpgradeTemplate>();
            foreach (var t in templates)
            {
                if (!t.IsUnlocked()) continue;

                var info = GetUpgradeInfo(new GameObj(t.Pointer));
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetAvailableUpgrades", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get upgrade info from a template.
    /// </summary>
    public static UpgradeInfo GetUpgradeInfo(GameObj template)
    {
        if (template.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            if (!GameObj<Il2CppMenace.Strategy.ShipUpgradeTemplate>.TryWrap(template, out var typed)) return null;

            var info = new UpgradeInfo { Pointer = template.Pointer };

            var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(template.Pointer);
            if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id))
                info.TemplateId = id;

            if (_hUpgradeType.TryRead(typed, out var upgradeType))
                info.UpgradeType = upgradeType;

            if (_hOciPointsCosts.TryRead(typed, out var cost))
                info.OciPointsCost = cost;

            if (_hUnlockType.TryRead(typed, out var unlockType))
                info.UnlockType = unlockType;

            if (_hUnlockedByFaction.TryRead(typed, out var faction))
                info.UnlockedByFaction = faction;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetUpgradeInfo", "Failed", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Slots
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all upgrade slots.
    /// </summary>
    public static List<SlotInfo> GetSlots()
    {
        var result = new List<SlotInfo>();

        try
        {
            var su = GetShipUpgrades();
            if (su.CheckAlive() != AliveStatus.Alive) return result;

            if (!GameObj<Il2CppMenace.Strategy.ShipUpgrades>.TryWrap(su, out var suTyped)) return result;

            if (!_hSlotTypes.TryRead(suTyped, out var slotTypesObj)) return result;
            if (!_hEquippedUpgrades.TryRead(suTyped, out var equippedObj)) return result;

            var slotArr = slotTypesObj.AsManaged();
            var equippedArr = equippedObj.AsManaged();

            if (slotArr == null) return result;

            for (int i = 0; i < slotArr.Length; i++)
            {
                var slotTemplate = slotArr[i];
                if (slotTemplate == null) continue;

                var slotObj = new GameObj(slotTemplate.Pointer);
                var info = GetSlotInfo(slotObj, i, equippedArr);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetSlots", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get slot info.
    /// </summary>
    public static SlotInfo GetSlotInfo(GameObj slot, int slotIndex, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppMenace.Strategy.ShipUpgradeTemplate> equippedArr)
    {
        if (slot.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            if (!GameObj<Il2CppMenace.Strategy.ShipUpgradeSlotTemplate>.TryWrap(slot, out var typed)) return null;

            var info = new SlotInfo { Pointer = slot.Pointer };

            var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(slot.Pointer);
            if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id))
                info.TemplateId = id;

            if (_hSlotUpgradeType.TryRead(typed, out var slotType))
                info.SlotType = slotType;

            if (equippedArr != null && slotIndex < equippedArr.Length)
            {
                var equipped = equippedArr[slotIndex];
                if (equipped != null)
                {
                    var upgradeInfo = GetUpgradeInfo(new GameObj(equipped.Pointer));
                    if (upgradeInfo != null)
                    {
                        upgradeInfo.IsInstalled = true;
                        info.EquippedUpgrade = upgradeInfo;
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetSlotInfo", "Failed", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Upgrade Installation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Install an upgrade.
    /// </summary>
    public static bool TryEquipUpgrade(GameObj upgrade, int paidOciComponents, int slotIdx, bool checkUnlocked = true)
    {
        if (upgrade.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var su = GetShipUpgrades();
            if (su.CheckAlive() != AliveStatus.Alive) return false;

            var suManaged = su.As<Il2CppMenace.Strategy.ShipUpgrades>();
            if (suManaged == null) return false;

            var upgradeManaged = upgrade.As<Il2CppMenace.Strategy.ShipUpgradeTemplate>();
            if (upgradeManaged == null) return false;

            return suManaged.TryEquipUpgrade(upgradeManaged, paidOciComponents, slotIdx, checkUnlocked);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.TryEquipUpgrade", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Uninstall an upgrade.
    /// </summary>
    public static bool TryUnequipUpgrade(GameObj upgrade)
    {
        if (upgrade.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var su = GetShipUpgrades();
            if (su.CheckAlive() != AliveStatus.Alive) return false;

            var suManaged = su.As<Il2CppMenace.Strategy.ShipUpgrades>();
            if (suManaged == null) return false;

            var upgradeManaged = upgrade.As<Il2CppMenace.Strategy.ShipUpgradeTemplate>();
            if (upgradeManaged == null) return false;

            return suManaged.TryUnequipUpgrade(upgradeManaged);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.TryUnequipUpgrade", "Failed", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register console commands for OCI SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // oci - Show OCI status
        DevConsole.RegisterCommand("oci", "", "Show OCI (ship upgrades) status", args =>
        {
            var components = GetOciComponents();
            var installed = GetInstalledUpgrades();
            var available = GetAvailableUpgrades();

            var lines = new List<string>
        {
            $"OCI Components: {components}",
            $"Installed Upgrades ({installed.Count}):"
        };

            foreach (var u in installed)
                lines.Add($"  [{u.UpgradeType}] {u.TemplateId} ({u.OciPointsCost} pts)");

            lines.Add($"Available Upgrades ({available.Count}):");
            foreach (var u in available)
                lines.Add($"  [{u.UpgradeType}] {u.TemplateId} ({u.OciPointsCost} pts)");

            return string.Join("\n", lines);
        });

        // ocislots - Show OCI slots
        DevConsole.RegisterCommand("ocislots", "", "Show OCI upgrade slots", args =>
        {
            var slots = GetSlots();
            if (slots.Count == 0)
                return "No slots found (strategy layer not active?)";

            var lines = new List<string> { $"OCI Slots ({slots.Count}):" };
            foreach (var s in slots)
            {
                var upgrade = s.EquippedUpgrade != null
                    ? $" → {s.EquippedUpgrade.TemplateId}"
                    : " (empty)";
                lines.Add($"  [{s.SlotType}] {s.TemplateId}{upgrade}");
            }
            return string.Join("\n", lines);
        });

        // ociupgrades - List all upgrade templates
        DevConsole.RegisterCommand("ociupgrades", "[type]", "List all OCI upgrade templates", args =>
        {
            var upgrades = GetAllUpgradeTemplates();
            if (upgrades.Count == 0)
                return "No upgrade templates found";

            if (args.Length > 0)
            {
                var typeFilter = args[0].ToLowerInvariant();
                upgrades = upgrades.FindAll(u =>
                    u.UpgradeType.ToString().ToLowerInvariant().Contains(typeFilter));
            }

            var lines = new List<string> { $"OCI Upgrades ({upgrades.Count}):" };
            foreach (var u in upgrades)
            {
                var unlock = u.UnlockType == ShipUpgradeUnlockType.Faction
                    ? $" [req: {u.UnlockedByFaction}]"
                    : u.UnlockType == ShipUpgradeUnlockType.EventOnly
                        ? " [event only]"
                        : "";
                lines.Add($"  [{u.UpgradeType}] {u.TemplateId} ({u.OciPointsCost} pts){unlock}");
            }
            return string.Join("\n", lines);
        });

        // equipoci <id> <slot> <cost> - Equip an upgrade
        DevConsole.RegisterCommand("equipoci", "<id> <slot> <cost>", "Equip an OCI upgrade", args =>
        {
            if (args.Length < 3)
                return "Usage: equipoci <template_id> <slot_index> <paid_oci_components>";

            if (!int.TryParse(args[1], out var slotIdx))
                return "Invalid slot index";

            if (!int.TryParse(args[2], out var cost))
                return "Invalid OCI component cost";

            var template = Templates.FindByID<Il2CppMenace.Strategy.ShipUpgradeTemplate>(args[0]);
            if (template == null)
                return $"Upgrade '{args[0]}' not found";

            if (TryEquipUpgrade(new GameObj(template.Pointer), cost, slotIdx))
                return $"Equipped: {args[0]} in slot {slotIdx}";
            return "Failed to equip upgrade";
        });
    }
}
