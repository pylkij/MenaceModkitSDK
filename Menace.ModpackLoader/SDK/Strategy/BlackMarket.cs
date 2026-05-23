using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for the BlackMarket shop system.
/// Provides safe access to purchasable items, item stacks, and shop management.
///
/// Based on reverse engineering findings:
/// - BlackMarket.Stacks @ +0x10 (List of BlackMarketItemStack)
/// - BlackMarketItemStack.Template @ +0x10
/// - BlackMarketItemStack.OperationsRemaining @ +0x18
/// - BlackMarketItemStack.Items @ +0x20
/// - BlackMarketItemStack.Type @ +0x28
/// - StrategyState.BlackMarket @ +0x88 (field, not property)
/// - StrategyConfig.BlackMarket @ +0x198 (BlackMarketConfig sub-object)
///   - BlackMarketConfig.Items @ +0x78 (BaseItemTemplate[] item pool)
///   - BlackMarketConfig.MinItems/MaxItems @ +0x80
///   - BlackMarketConfig.OperationsTimeout @ +0xac (range)
/// </summary>
public static class BlackMarket
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // StrategyState fields
    private static ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.BlackMarket> _hSSBlackMarket;

    // StrategyConfig fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.StrategyConfig, Il2CppMenace.BlackMarketConfig> _hConfigBMConfig;

    // BlackMarket fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.BlackMarket, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>> _hBMItemStacks;

    // BlackMarketItemStack fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack, Il2CppMenace.Items.BaseItemTemplate> _hStackTemplate;
    private static FieldHandle<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack, int> _hStackTimeout;
    private static ObjFieldHandle<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack, Il2CppSystem.Collections.Generic.List<Il2CppMenace.Items.BaseItem>> _hStackInstances;
    private static FieldHandle<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack, Il2CppMenace.Strategy.BlackMarketStackType> _hStackType;

    // BlackMarketConfig fields
    private static ObjFieldHandle<Il2CppMenace.BlackMarketConfig, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppMenace.Items.BaseItemTemplate>> _hBMCBaseItems;
    private static FieldHandle<Il2CppMenace.BlackMarketConfig, UnityEngine.Vector2Int> _hBMCRegularItemCount;
    private static FieldHandle<Il2CppMenace.BlackMarketConfig, UnityEngine.Vector2Int> _hBMCItemTimeout;

    // BaseItemTemplate fields
    private static FieldHandle<Il2CppMenace.Items.BaseItemTemplate, int> _hTemplateRarity;
    private static FieldHandle<Il2CppMenace.Items.BaseItemTemplate, int> _hTemplateTradeValue;

    // BaseItem fields
    private static ObjFieldHandle<Il2CppMenace.Items.BaseItem, Il2CppMenace.Items.BaseItemTemplate> _hItemTemplate;

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
            _hSSBlackMarket = GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.BlackMarket);

            _hConfigBMConfig = GameObj<Il2CppMenace.Strategy.StrategyConfig>.ResolveObjField(x => x.BlackMarketConfig);

            _hBMItemStacks = GameObj<Il2CppMenace.Strategy.BlackMarket>.ResolveObjField(x => x.m_ItemStacks);

            _hStackTemplate = GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>.ResolveObjField(x => x.m_ItemTemplate);
            _hStackTimeout = GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>.ResolveField(x => x.m_RemainingTimeout);
            _hStackInstances = GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>.ResolveObjField(x => x.m_Instances);
            _hStackType = GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>.ResolveField(x => x.m_StackType);

            _hBMCBaseItems = GameObj<Il2CppMenace.BlackMarketConfig>.ResolveObjField(x => x.BaseItems);
            _hBMCRegularItemCount = GameObj<Il2CppMenace.BlackMarketConfig>.ResolveField(x => x.RegularItemCount);
            _hBMCItemTimeout = GameObj<Il2CppMenace.BlackMarketConfig>.ResolveField(x => x.ItemTimeout);

            _hTemplateRarity = GameObj<Il2CppMenace.Items.BaseItemTemplate>.ResolveField(x => x.Rarity);
            _hTemplateTradeValue = GameObj<Il2CppMenace.Items.BaseItemTemplate>.ResolveField(x => x.TradeValue);

            _hItemTemplate = GameObj<Il2CppMenace.Items.BaseItem>.ResolveObjField(x => x.m_Template);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.ResolveHandles", "Field handle resolution failed", ex);
        }
    }

    /// <summary>
    /// Stack type enumeration matching game's BlackMarketStackType.
    /// </summary>
    public enum StackType
    {
        /// <summary>No stack type assigned.</summary>
        None = 0,
        /// <summary>Permanent base item that never expires.</summary>
        Base = 1,
        /// <summary>Regular generated shop item.</summary>
        Regular = 2,
        /// <summary>Item generated for a specific tag requirement.</summary>
        Tagged = 3,
        /// <summary>Special offer item (IsSpecialOffer returns true when type == SpecialOffer).</summary>
        SpecialOffer = 4
    }

    /// <summary>
    /// BlackMarket information structure containing shop state and configuration.
    /// </summary>
    public class BlackMarketInfo
    {
        /// <summary>Number of item stacks currently available in the shop.</summary>
        public int StackCount { get; set; }
        /// <summary>Total number of individual items across all stacks.</summary>
        public int TotalItemCount { get; set; }
        /// <summary>Minimum and maximum regular items generated per FillUp (x = min, y = max).</summary>
        public Vector2Int RegularItemCount { get; set; }
        /// <summary>Minimum and maximum operations before item removal (x = min, y = max).</summary>
        public Vector2Int ItemTimeout { get; set; }
        /// <summary>Number of templates available in the base item pool.</summary>
        public int ItemPoolSize { get; set; }
        /// <summary>Current campaign progress (0.0–1.0) affecting item generation.</summary>
        public float CampaignProgress { get; set; }
        /// <summary>Pointer to the BlackMarket instance.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Item stack information representing a purchasable item entry in the shop.
    /// </summary>
    public class ItemStackInfo
    {
        /// <summary>Stable ID of the item template (from DataTemplate.m_ID).</summary>
        public string TemplateID { get; set; }
        /// <summary>Operations remaining before this stack is removed.</summary>
        public int RemainingTimeout { get; set; }
        /// <summary>Number of item instances available in this stack.</summary>
        public int ItemCount { get; set; }
        /// <summary>Category type of this stack.</summary>
        public StackType Type { get; set; }
        /// <summary>Rarity (0–100) of the item template.</summary>
        public int Rarity { get; set; }
        /// <summary>Base trade value of the item template.</summary>
        public int TradeValue { get; set; }
        /// <summary>Whether this stack will expire (determined by CanTimeout()).</summary>
        public bool CanTimeout { get; set; }
        /// <summary>Pointer to the BlackMarketItemStack instance.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Individual item information for items within a stack.
    /// </summary>
    public class ItemInfo
    {
        /// <summary>Stable ID of the item template (from DataTemplate.m_ID).</summary>
        public string TemplateID { get; set; }
        /// <summary>Rarity (0–100) of the item template.</summary>
        public int Rarity { get; set; }
        /// <summary>Base trade value of the item template.</summary>
        public int TradeValue { get; set; }
        /// <summary>Pointer to the BaseItem instance.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the BlackMarket instance from StrategyState.
    /// </summary>
    /// <returns>GameObj representing the BlackMarket, or GameObj.Null if unavailable.</returns>
    public static GameObj GetBlackMarket()
    {
        try
        {
            var ss = Il2CppMenace.States.StrategyState.Get();
            if (ss == null) return GameObj.Null;

            var ssObj = GameObj<Il2CppMenace.States.StrategyState>.Wrap(ss.Pointer);
            if (!_hSSBlackMarket.TryRead(ssObj, out var bmObj)) return GameObj.Null;
            return bmObj.Untyped;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetBlackMarket", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get detailed information about the BlackMarket state.
    /// </summary>
    /// <returns>BlackMarketInfo containing shop state, or null if unavailable.</returns>
    public static BlackMarketInfo GetBlackMarketInfo()
    {
        var bm = GetBlackMarket();
        if (bm.IsNull) return null;

        try
        {
            var info = new BlackMarketInfo { Pointer = bm.Pointer };

            var stacks = GetStacksList(bm);
            info.StackCount = stacks.Count;
            foreach (var stack in stacks)
            {
                if (_hStackInstances.TryRead(stack, out var instancesObj))
                    info.TotalItemCount += instancesObj.AsManaged()?.Count ?? 0;
            }

            var bmConfig = GetBlackMarketConfig();
            if (!bmConfig.IsNull)
            {
                var bmConfigTyped = GameObj<Il2CppMenace.BlackMarketConfig>.Wrap(bmConfig.Pointer);

                if (_hBMCRegularItemCount.TryRead(bmConfigTyped, out var regularItemCount))
                    info.RegularItemCount = regularItemCount;

                if (_hBMCItemTimeout.TryRead(bmConfigTyped, out var itemTimeout))
                    info.ItemTimeout = itemTimeout;

                if (_hBMCBaseItems.TryRead(bmConfigTyped, out var baseItems))
                    info.ItemPoolSize = baseItems.AsManaged()?.Length ?? 0;
            }

            info.CampaignProgress = Il2CppMenace.States.StrategyState.Get()?.GetCampaignProgress() ?? 0f;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetBlackMarketInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get all available item stacks in the BlackMarket.
    /// </summary>
    /// <returns>List of ItemStackInfo for each available stack.</returns>
    public static List<ItemStackInfo> GetAvailableStacks()
    {
        var result = new List<ItemStackInfo>();

        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return result;

            foreach (var stack in GetStacksList(bm))
            {
                var stackInfo = GetItemStackInfoInternal(stack);
                if (stackInfo != null)
                    result.Add(stackInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetAvailableStacks", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a specific item stack by index.
    /// </summary>
    /// <param name="index">Index of the stack in the Stacks list.</param>
    /// <returns>ItemStackInfo for the stack, or null if invalid.</returns>
    public static ItemStackInfo GetStackInfo(int index)
    {
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return null;

            var stacks = GetStacksList(bm);
            if (index < 0 || index >= stacks.Count) return null;

            return GetItemStackInfoInternal(stacks[index]);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetStackInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the GameObj for a stack by index.
    /// </summary>
    /// <param name="index">Index of the stack in the Stacks list.</param>
    /// <returns>GameObj representing the stack, or GameObj.Null if invalid.</returns>
    public static GameObj GetStackAt(int index)
    {
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return GameObj.Null;

            var stacks = GetStacksList(bm);
            if (index < 0 || index >= stacks.Count) return GameObj.Null;

            return stacks[index].Untyped;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all items within a specific stack.
    /// </summary>
    /// <param name="stack">The stack GameObj to inspect.</param>
    /// <returns>List of ItemInfo for items in the stack.</returns>
    public static List<ItemInfo> GetItemsInStack(GameObj stack)
    {
        var result = new List<ItemInfo>();
        if (stack.IsNull) return result;

        try
        {
            var stackTyped = GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>.Wrap(stack.Pointer);
            if (!_hStackInstances.TryRead(stackTyped, out var instancesObj)) return result;

            var instances = instancesObj.AsManaged();
            if (instances == null) return result;

            foreach (var item in instances)
            {
                if (item == null) continue;
                var itemInfo = GetItemInfoInternal(new GameObj(item.Pointer));
                if (itemInfo != null)
                    result.Add(itemInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetItemsInStack", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Find a stack by template ID without building full ItemStackInfo objects.
    /// </summary>
    /// <param name="templateId">The stable m_ID of the item template to find.</param>
    /// <returns>ItemStackInfo for the matching stack, or null if not found.</returns>
    public static ItemStackInfo FindStackByTemplateId(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return null;

        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return null;

            foreach (var stack in GetStacksList(bm))
            {
                if (!_hStackTemplate.TryRead(stack, out var templateObj)) continue;

                var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer);
                if (!Templates._hDataTemplateId.TryRead(dataTemplateObj, out var id)) continue;

                if (string.Equals(id, templateId, StringComparison.OrdinalIgnoreCase))
                    return GetItemStackInfoInternal(stack);
            }

            return null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.FindStackByTemplateId", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if the BlackMarket contains a specific item template.
    /// </summary>
    /// <param name="templateId">The stable m_ID of the item template to check.</param>
    /// <returns>True if the template is available for purchase.</returns>
    public static bool HasTemplate(string templateId)
    {
        return FindStackByTemplateId(templateId) != null;
    }

    /// <summary>
    /// Get the total number of available stacks.
    /// </summary>
    /// <returns>Number of stacks in the BlackMarket.</returns>
    public static int GetStackCount()
    {
        var bm = GetBlackMarket();
        if (bm.IsNull) return 0;
        return GetStacksList(bm).Count;
    }

    /// <summary>
    /// Get stacks that are expiring soon (1 operation remaining).
    /// </summary>
    /// <returns>List of ItemStackInfo for stacks about to expire.</returns>
    public static List<ItemStackInfo> GetExpiringStacks()
    {
        var result = new List<ItemStackInfo>();
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return result;

            foreach (var stack in GetStacksList(bm))
            {
                if (!_hStackTimeout.TryRead(stack, out var timeout)) continue;
                if (timeout > 1) continue;

                var stackTyped = stack.AsManaged();
                if (stackTyped == null || !stackTyped.CanTimeout()) continue;

                var stackInfo = GetItemStackInfoInternal(stack);
                if (stackInfo != null)
                    result.Add(stackInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetExpiringStacks", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get stacks that never expire.
    /// </summary>
    /// <returns>List of ItemStackInfo for stacks that cannot timeout.</returns>
    public static List<ItemStackInfo> GetPermanentStacks()
    {
        var result = new List<ItemStackInfo>();
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return result;

            foreach (var stack in GetStacksList(bm))
            {
                var stackManaged = stack.AsManaged();
                if (stackManaged == null || stackManaged.CanTimeout()) continue;

                var stackInfo = GetItemStackInfoInternal(stack);
                if (stackInfo != null)
                    result.Add(stackInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetPermanentStacks", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get stacks of a specific type.
    /// </summary>
    /// <param name="type">Stack type to filter by.</param>
    /// <returns>List of ItemStackInfo matching the type.</returns>
    public static List<ItemStackInfo> GetStacksByType(StackType type)
    {
        var result = new List<ItemStackInfo>();
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return result;

            foreach (var stack in GetStacksList(bm))
            {
                if (!_hStackType.TryRead(stack, out var stackType)) continue;
                if ((StackType)stackType != type) continue;

                var stackInfo = GetItemStackInfoInternal(stack);
                if (stackInfo != null)
                    result.Add(stackInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetStacksByType", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get the total trade value of all items in the BlackMarket.
    /// </summary>
    /// <returns>Sum of trade values for all available items.</returns>
    public static int GetTotalTradeValue()
    {
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return 0;

            int total = 0;
            foreach (var stack in GetStacksList(bm))
            {
                if (!_hStackTemplate.TryRead(stack, out var templateObj)) continue;
                if (!_hTemplateTradeValue.TryRead(templateObj, out var tradeValue)) continue;
                if (!_hStackInstances.TryRead(stack, out var instancesObj)) continue;

                total += tradeValue * (instancesObj.AsManaged()?.Count ?? 0);
            }

            return total;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetTotalTradeValue", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Stock an item in the BlackMarket by template ID.
    /// </summary>
    public static string StockItemInBlackMarket(string templateId)
    {
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull)
                return "BlackMarket not available. Are you on the strategy map?";

            var template = Inventory.FindItemTemplate(templateId);
            if (template.IsNull)
                return $"Template '{templateId}' not found";

            var templateManaged = template.As<Il2CppMenace.Items.BaseItemTemplate>();
            if (templateManaged == null)
                return "Failed to get template proxy";

            var guid = Il2CppMenace.Items.BaseItemTemplate.CreateGuid();
            var item = templateManaged.CreateItem(guid);
            if (item == null)
                return "CreateItem returned null";

            var bmManaged = GameObj<Il2CppMenace.Strategy.BlackMarket>.Wrap(bm.Pointer).AsManaged();
            if (bmManaged == null)
                return "Failed to get BlackMarket proxy";

            bmManaged.AddItem(item, 99);

            return $"Stocked '{templateId}' in BlackMarket";
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.StockItemInBlackMarket", "Failed", ex);
            return $"Failed to stock item: {ex.Message}";
        }
    }

    /// <summary>
    /// Register console commands for BlackMarket SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // blackmarket - Show BlackMarket overview
        DevConsole.RegisterCommand("blackmarket", "", "Show BlackMarket overview", args =>
        {
            var info = GetBlackMarketInfo();
            if (info == null)
                return "BlackMarket not available (strategy layer not active?)";

            return $"BlackMarket Status:\n" +
                   $"  Stacks: {info.StackCount} ({info.TotalItemCount} total items)\n" +
                   $"  Config: {info.RegularItemCount.x}-{info.RegularItemCount.y} items, " +
                   $"{info.ItemTimeout.x}-{info.ItemTimeout.y} ops timeout\n" +
                   $"  Item Pool: {info.ItemPoolSize} templates\n" +
                   $"  Campaign Progress: {info.CampaignProgress:P0}";
        });

        // bmdebug - Debug StrategyState/BlackMarket access
        DevConsole.RegisterCommand("bmdebug", "", "Debug StrategyState/BlackMarket access", args =>
        {
            var lines = new List<string> { "BlackMarket Debug:" };

            try
            {
                var ss = Il2CppMenace.States.StrategyState.Get();
                lines.Add($"  StrategyState.Get(): {(ss != null ? "EXISTS" : "NULL")}");

                var bm = GetBlackMarket();
                lines.Add($"  BlackMarket: {(bm.IsNull ? "NULL" : $"0x{bm.Pointer:X}")}");

                if (!bm.IsNull)
                {
                    var stacks = GetStacksList(bm);
                    lines.Add($"  Stacks: {stacks.Count}");
                }

                var config = Il2CppMenace.Strategy.StrategyConfig.Current;
                lines.Add($"  StrategyConfig.Current: {(config != null ? "EXISTS" : "NULL")}");

                var bmConfig = GetBlackMarketConfig();
                lines.Add($"  BlackMarketConfig: {(bmConfig.IsNull ? "NULL" : $"0x{bmConfig.Pointer:X}")}");

                lines.Add($"  Handles resolved: {_handlesResolved}");
            }
            catch (Exception ex)
            {
                lines.Add($"  Error: {ex.Message}");
            }

            return string.Join("\n", lines);
        });

        // bmitems - List all BlackMarket items
        DevConsole.RegisterCommand("bmitems", "", "List all BlackMarket items", args =>
        {
            var stacks = GetAvailableStacks();
            if (stacks.Count == 0)
                return "No items in BlackMarket";

            var lines = new List<string> { $"BlackMarket Items ({stacks.Count} stacks):" };
            for (int i = 0; i < stacks.Count; i++)
            {
                var s = stacks[i];
                var expiry = s.CanTimeout ? $" ({s.RemainingTimeout} ops)" : " [PERM]";
                var typeTag = s.Type != StackType.Regular ? $" [{s.Type}]" : "";
                lines.Add($"  {i}. {s.TemplateID} x{s.ItemCount} - ${s.TradeValue}{expiry}{typeTag}");
            }
            return string.Join("\n", lines);
        });

        // bmstack <index> - Show stack details
        DevConsole.RegisterCommand("bmstack", "<index>", "Show BlackMarket stack details", args =>
        {
            if (args.Length == 0)
                return "Usage: bmstack <index>";

            if (!int.TryParse(args[0], out int index))
                return "Invalid index";

            var stack = GetStackInfo(index);
            if (stack == null)
                return $"Stack {index} not found";

            var expiryInfo = stack.CanTimeout
                ? $"{stack.RemainingTimeout} operations remaining"
                : "Never expires";

            return $"Stack {index}: {stack.TemplateID}\n" +
                   $"  Type: {stack.Type}\n" +
                   $"  Items: {stack.ItemCount}\n" +
                   $"  Trade Value: ${stack.TradeValue} each\n" +
                   $"  Rarity: {stack.Rarity}\n" +
                   $"  Expiry: {expiryInfo}";
        });

        // bmexpiring - List items expiring soon
        DevConsole.RegisterCommand("bmexpiring", "", "List BlackMarket items expiring soon", args =>
        {
            var expiring = GetExpiringStacks();
            if (expiring.Count == 0)
                return "No items expiring soon";

            var lines = new List<string> { $"Expiring Items ({expiring.Count}):" };
            foreach (var s in expiring)
                lines.Add($"  {s.TemplateID} x{s.ItemCount} - ${s.TradeValue} ({s.RemainingTimeout} op left)");

            lines.Add("\nThese items will be removed after the next operation!");
            return string.Join("\n", lines);
        });

        // bmpermanent - List permanent items
        DevConsole.RegisterCommand("bmpermanent", "", "List permanent BlackMarket items", args =>
        {
            var permanent = GetPermanentStacks();
            if (permanent.Count == 0)
                return "No permanent items in BlackMarket";

            var lines = new List<string> { $"Permanent Items ({permanent.Count}):" };
            foreach (var s in permanent)
                lines.Add($"  {s.TemplateID} x{s.ItemCount} - ${s.TradeValue}");

            return string.Join("\n", lines);
        });

        // bmfind <id> - Search for item by template ID
        DevConsole.RegisterCommand("bmfind", "<id>", "Search for BlackMarket item by template ID", args =>
        {
            if (args.Length == 0)
                return "Usage: bmfind <template_id>";

            var searchTerm = string.Join(" ", args).ToLowerInvariant();
            var stacks = GetAvailableStacks();
            var matches = stacks.Where(s =>
                s.TemplateID != null &&
                s.TemplateID.ToLowerInvariant().Contains(searchTerm)).ToList();

            if (matches.Count == 0)
                return $"No items matching '{searchTerm}' found";

            var lines = new List<string> { $"Found {matches.Count} matching items:" };
            foreach (var s in matches)
            {
                var expiry = s.CanTimeout ? $" ({s.RemainingTimeout} ops)" : " [PERM]";
                lines.Add($"  {s.TemplateID} x{s.ItemCount} - ${s.TradeValue}{expiry}");
            }
            return string.Join("\n", lines);
        });

        // bmstock <template_id> - Add item to BlackMarket for testing
        DevConsole.RegisterCommand("bmstock", "<template_id>", "Stock an item in BlackMarket (for testing)", args =>
        {
            if (args.Length == 0)
                return "Usage: bmstock <template_id>\nExample: bmstock weapon.laser_smg";

            return StockItemInBlackMarket(args[0]);
        });

        // bmvalue - Show total BlackMarket value
        DevConsole.RegisterCommand("bmvalue", "", "Show total BlackMarket trade value", args =>
        {
            var total = GetTotalTradeValue();
            var bm = GetBlackMarket();
            var stackCount = bm.IsNull ? 0 : GetStacksList(bm).Count;

            return $"Total BlackMarket Value: ${total}\n" +
                   $"Stacks: {stackCount}";
        });

        // bmbytype <type> - Filter by stack type
        DevConsole.RegisterCommand("bmbytype", "<type>", "Filter BlackMarket by type (None/Base/Regular/Tagged/SpecialOffer)", args =>
        {
            if (args.Length == 0)
                return "Usage: bmbytype <type>\nTypes: None, Base, Regular, Tagged, SpecialOffer (or 0-4)";

            StackType type;
            if (int.TryParse(args[0], out int typeInt))
            {
                type = (StackType)typeInt;
            }
            else if (!Enum.TryParse(args[0], ignoreCase: true, out type))
            {
                return "Invalid type. Use: None, Base, Regular, Tagged, SpecialOffer (or 0-4)";
            }

            if ((int)type < 0 || (int)type > 4)
                return "Invalid type. Use: None, Base, Regular, Tagged, SpecialOffer (or 0-4)";

            var stacks = GetStacksByType(type);
            if (stacks.Count == 0)
                return $"No {type} items in BlackMarket";

            var lines = new List<string> { $"{type} Items ({stacks.Count}):" };
            foreach (var s in stacks)
            {
                var expiry = s.CanTimeout ? $" ({s.RemainingTimeout} ops)" : "";
                lines.Add($"  {s.TemplateID} x{s.ItemCount} - ${s.TradeValue}{expiry}");
            }
            return string.Join("\n", lines);
        });
    }

    // --- Internal helpers ---

    // Returns raw typed stack wrappers — no info building, no reflection
    private static List<GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>> GetStacksList(GameObj bm)
    {
        var result = new List<GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>>();
        if (bm.IsNull) return result;

        try
        {
            var bmTyped = GameObj<Il2CppMenace.Strategy.BlackMarket>.Wrap(bm.Pointer);
            if (!_hBMItemStacks.TryRead(bmTyped, out var stacksObj)) return result;

            var stacks = stacksObj.AsManaged();
            if (stacks == null) return result;

            foreach (var stack in stacks)
            {
                if (stack == null) continue;
                result.Add(GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>.Wrap(stack.Pointer));
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetStacksList", "Failed", ex);
            return result;
        }
    }

    // Builds ItemStackInfo from a pre-wrapped stack — called only when full info is needed
    private static ItemStackInfo GetItemStackInfoInternal(GameObj<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack> stack)
    {
        try
        {
            var info = new ItemStackInfo { Pointer = stack.Untyped.Pointer };

            if (_hStackType.TryRead(stack, out var stackType))
                info.Type = (StackType)stackType;

            if (_hStackTimeout.TryRead(stack, out var timeout))
                info.RemainingTimeout = timeout;

            if (_hStackInstances.TryRead(stack, out var instancesObj))
                info.ItemCount = instancesObj.AsManaged()?.Count ?? 0;

            if (_hStackTemplate.TryRead(stack, out var templateObj))
            {
                if (_hTemplateRarity.TryRead(templateObj, out var rarity))
                    info.Rarity = rarity;

                if (_hTemplateTradeValue.TryRead(templateObj, out var tradeValue))
                    info.TradeValue = tradeValue;

                var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer);
                if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var templateId))
                    info.TemplateID = templateId;
            }

            info.CanTimeout = GameMethod.CallBool<Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack>(
                stack.AsManaged(), x => x.CanTimeout());

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetItemStackInfoInternal", "Failed", ex);
            return null;
        }
    }

    // Gets the BlackMarketConfig GameObj via StrategyConfig.Current
    private static GameObj GetBlackMarketConfig()
    {
        try
        {
            var config = Il2CppMenace.Strategy.StrategyConfig.Current;
            if (config == null) return GameObj.Null;

            var configObj = GameObj<Il2CppMenace.Strategy.StrategyConfig>.Wrap(config.Pointer);
            if (!_hConfigBMConfig.TryRead(configObj, out var bmConfig)) return GameObj.Null;
            return bmConfig.Untyped;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetBlackMarketConfig", "Failed", ex);
            return GameObj.Null;
        }
    }

    private static ItemInfo GetItemInfoInternal(GameObj item)
    {
        if (item.IsNull) return null;

        try
        {
            var info = new ItemInfo { Pointer = item.Pointer };

            var itemTyped = GameObj<Il2CppMenace.Items.BaseItem>.Wrap(item.Pointer);

            if (_hItemTemplate.TryRead(itemTyped, out var templateObj))
            {
                if (_hTemplateRarity.TryRead(templateObj, out var rarity))
                    info.Rarity = rarity;

                if (_hTemplateTradeValue.TryRead(templateObj, out var tradeValue))
                    info.TradeValue = tradeValue;

                var dataTemplateObj = GameObj<Il2CppMenace.Tools.DataTemplate>.Wrap(templateObj.Untyped.Pointer);
                if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var templateId))
                    info.TemplateID = templateId;
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetItemInfoInternal", "Failed", ex);
            return null;
        }
    }
}
