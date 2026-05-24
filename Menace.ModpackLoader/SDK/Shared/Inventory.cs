using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Items;
using Il2CppMenace.Tags;
using Menace.SDK.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for item and inventory operations.
/// Provides safe access to items, containers, equipment, and trade values.
///
/// Field offsets resolved via GameObj<T> handles — see Offsets class below.
/// Managed-proxy calls (GetAllItems, Place, Remove, etc.) go through AsManaged()
/// rather than raw reflection, matching the post-migration pattern.
/// </summary>
public static class Inventory
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in SceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // Item fields
    private static ObjFieldHandle<Il2CppMenace.Items.Item, Il2CppMenace.Items.ItemContainer> _hItemContainer;

    // ItemContainer fields
    private static ObjFieldHandle<Il2CppMenace.Items.ItemContainer, Il2CppMenace.Strategy.ItemsModularVehicle> _hContainerModularVehicle;

    // BaseItemTemplate fields
    private static FieldHandle<Il2CppMenace.Items.BaseItemTemplate, int> _hTemplateRarity;

    // StrategyState fields
    private static ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.OwnedItems> _hStrategyStateOwnedItems;

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
            _hItemContainer = GameObj<Il2CppMenace.Items.Item>.ResolveObjField(x => x.m_Container);
            _hContainerModularVehicle = GameObj<Il2CppMenace.Items.ItemContainer>.ResolveObjField(x => x.m_ModularVehicle);
            _hTemplateRarity = GameObj<Il2CppMenace.Items.BaseItemTemplate>.ResolveField(x => x.Rarity);
            _hStrategyStateOwnedItems = GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.OwnedItems);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.ResolveHandles", "Field handle resolution failed", ex);
        }
    }

    public class ItemInfo
    {
        public string GUID { get; set; }
        public string TemplateName { get; set; }
        public ItemSlot SlotType { get; set; }
        public int TradeValue { get; set; }
        public int RarityTier { get; set; }
        public int SkillCount { get; set; }
        public bool IsTemporary { get; set; }
        public GameObj<Il2CppMenace.Items.Item> Item { get; set; }
    }

    /// <summary>
    /// Container information structure.
    /// </summary>
    public class ContainerInfo
    {
        public int TotalItems { get; set; }
        public Dictionary<ItemSlot, int> SlotCounts { get; set; }
        public bool HasModularVehicle { get; set; }
        public GameObj<Il2CppMenace.Items.ItemContainer> Container { get; set; }
    }

    /// <summary>
    /// Get the global OwnedItems manager.
    /// Returns GameObj.Null when not on the strategy map.
    /// </summary>
    public static GameObj<Il2CppMenace.Strategy.OwnedItems> GetOwnedItems()
    {
        try
        {
            var ss = Il2CppMenace.States.StrategyState.Get();
            if (ss == null)
                return default;

            var ssObj = GameObj<Il2CppMenace.States.StrategyState>.Wrap(ss.Pointer);
            if (ssObj.Untyped.CheckAlive() != AliveStatus.Alive)
                return default;

            if (!_hStrategyStateOwnedItems.TryRead(ssObj, out var ownedItemsObj))
            {
                SdkLogger.Warning("GetOwnedItems: OwnedItems field is null");
                return default;
            }

            return ownedItemsObj;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetOwnedItems", "Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Get the item container for an entity.
    /// </summary>
    public static GameObj<Il2CppMenace.Items.ItemContainer> GetContainer(
    GameObj<Il2CppMenace.Tactical.Entity> entity)
    {
        if (entity.Untyped.CheckAlive() != AliveStatus.Alive) return default;

        try
        {
            var container = entity.AsManaged().GetItems();
            if (container == null) return default;

            return GameObj<Il2CppMenace.Items.ItemContainer>.Wrap(
                ((Il2CppObjectBase)container).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetContainer", "Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Get all items in a container.
    /// </summary>
    // Step D: typed overload.
    public static List<ItemInfo> GetAllItems(
    GameObj<Il2CppMenace.Items.ItemContainer> container)
    {
        var result = new List<ItemInfo>();
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        try
        {
            var items = container.AsManaged().GetAllItems();
            if (items == null) return result;

            foreach (var item in items)
            {
                if (item == null) continue;

                var itemObj = GameObj<Il2CppMenace.Items.Item>.Wrap(
                    ((Il2CppObjectBase)item).Pointer);
                if (itemObj.Untyped.CheckAlive() != AliveStatus.Alive) continue;

                var info = GetItemInfo(itemObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetAllItems", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get items in a specific slot type.
    /// </summary>
    public static List<ItemInfo> GetItemsInSlot(
    GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType)
    {
        var result = new List<ItemInfo>();
        if (container.Untyped.CheckAlive() != AliveStatus.Alive || slotType == ItemSlot.None)
            return result;

        try
        {
            var items = container.AsManaged().GetAllItemsAtSlotCopy(slotType);
            if (items == null) return result;

            foreach (var item in items)
            {
                if (item == null) continue;

                var itemObj = GameObj<Il2CppMenace.Items.Item>.Wrap(
                    ((Il2CppObjectBase)item).Pointer);
                if (itemObj.Untyped.CheckAlive() != AliveStatus.Alive) continue;

                var info = GetItemInfo(itemObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemsInSlot", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get the item at a specific slot and index.
    /// </summary>
    public static GameObj<Il2CppMenace.Items.Item> GetItemAt(
    GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType, int index)
    {
        if (container.Untyped.CheckAlive() != AliveStatus.Alive || slotType == ItemSlot.None)
            return default;

        try
        {
            var items = container.AsManaged().GetAllItemsAtSlotCopy(slotType);
            if (items == null || index < 0 || index >= items.Length) return default;

            var item = items[index];
            if (item == null) return default;

            var itemObj = GameObj<Il2CppMenace.Items.Item>.Wrap(
                ((Il2CppObjectBase)item).Pointer);
            return itemObj.Untyped.CheckAlive() == AliveStatus.Alive ? itemObj : default;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemAt", "Failed", ex);
            return default;
        }
    }

    /// <summary>
    /// Get item information.
    /// </summary>
    public static ItemInfo GetItemInfo(GameObj<Il2CppMenace.Items.Item> item)
    {
        if (item.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var proxy = item.AsManaged();
            var info = new ItemInfo { Item = item };

            info.GUID = proxy.GetID();

            var template = proxy.GetTemplate();
            if (template != null)
            {
                info.TemplateName = template.name;
                info.SlotType = template.SlotType;
                info.TradeValue = template.GetTradeValue();
                info.RarityTier = template.GetRarity();
            }

            info.IsTemporary = proxy.IsTemporary();
            info.SkillCount = proxy.GetSkills()?.Count ?? 0;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get container information.
    /// </summary>
    public static ContainerInfo GetContainerInfo(
    GameObj<Il2CppMenace.Items.ItemContainer> container)
    {
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        try
        {
            var proxy = container.AsManaged();
            var info = new ContainerInfo
            {
                Container = container,
                SlotCounts = new Dictionary<ItemSlot, int>()
            };

            foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
            {
                if (slot == ItemSlot.None || slot == ItemSlot.All || slot == ItemSlot.COUNT)
                    continue;

                var count = proxy.GetItemSlotCount(slot);
                info.SlotCounts[slot] = count;
                info.TotalItems += count;
            }

            if (_hContainerModularVehicle.TryRead(container, out var modularVehicle))
                info.HasModularVehicle = !modularVehicle.Untyped.IsNull;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetContainerInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if a container has an item with a specific tag.
    /// NOTE: ItemContainer.ContainsTag takes a TagType enum. This method accepts
    /// a string and parses it to TagType. If parsing fails, returns false.
    /// </summary>
    public static bool HasItemWithTag(
    GameObj<Il2CppMenace.Items.ItemContainer> container, TagType tagType)
    {
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            return container.AsManaged().ContainsTag(tagType);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.HasItemWithTag", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get items with a specific tag.
    /// NOTE: ItemContainer.GetItemsWithTag returns an int (count), not a list.
    /// This method iterates all items and filters by checking the tag on each
    /// item's template (BaseItemTemplate.HasTag). If tag string is not a valid
    /// TagType, returns empty list.
    /// </summary>
    public static List<ItemInfo> GetItemsWithTag(
    GameObj<Il2CppMenace.Items.ItemContainer> container, TagType tagType)
    {
        var result = new List<ItemInfo>();
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        try
        {
            var allItems = GetAllItems(container);
            foreach (var itemInfo in allItems)
            {
                if (itemInfo.Item.Untyped.CheckAlive() != AliveStatus.Alive) continue;

                var template = itemInfo.Item.AsManaged().GetTemplate();
                if (template != null && template.HasTag(tagType))
                    result.Add(itemInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemsWithTag", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get equipped weapons for an entity.
    /// </summary>
    public static List<ItemInfo> GetEquippedWeapons(
    GameObj<Il2CppMenace.Tactical.Entity> entity)
    {
        var result = new List<ItemInfo>();
        var container = GetContainer(entity);
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return result;

        result.AddRange(GetItemsInSlot(container, ItemSlot.InfantryWeapon));
        result.AddRange(GetItemsInSlot(container, ItemSlot.InfantrySpecial));
        return result;
    }

    /// <summary>
    /// Get equipped armor for an entity.
    /// </summary>
    public static ItemInfo GetEquippedArmor(
    GameObj<Il2CppMenace.Tactical.Entity> entity)
    {
        var container = GetContainer(entity);
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return null;

        var items = GetItemsInSlot(container, ItemSlot.InfantryArmor);
        return items.Count > 0 ? items[0] : null;
    }

    /// <summary>
    /// Get total trade value of all items in a container.
    /// </summary>
    // Step D: typed overload.
    public static int GetTotalTradeValue(
        GameObj<Il2CppMenace.Items.ItemContainer> container)
    {
        var items = GetAllItems(container);
        int total = 0;
        foreach (var item in items)
            total += item.TradeValue;
        return total;
    }

    /// <summary>
    /// Remove a specific item from a container.
    /// NOTE: The method on ItemContainer is Remove(Item, bool), not RemoveItem.
    /// </summary>
    public static bool RemoveItem(
    GameObj<Il2CppMenace.Items.ItemContainer> container,
    GameObj<Il2CppMenace.Items.Item> item)
    {
        if (container.Untyped.CheckAlive() != AliveStatus.Alive ||
            item.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            return container.AsManaged().Remove(item.AsManaged());
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.RemoveItem", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove item at a specific slot and index.
    /// </summary>
    public static bool RemoveItemAt(
    GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType, int index)
    {
        if (container.Untyped.CheckAlive() != AliveStatus.Alive || slotType == ItemSlot.None)
            return false;

        try
        {
            return container.AsManaged().Remove(slotType, index);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.RemoveItemAt", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Transfer an item from one container to another.
    /// NOTE: Place(Item, int, bool) requires an index. We pass 0 here to place
    /// at the first available position; the game resolves the actual slot from
    /// the item's template type.
    /// </summary>
    public static bool TransferItem(
    GameObj<Il2CppMenace.Items.ItemContainer> from,
    GameObj<Il2CppMenace.Items.ItemContainer> to,
    GameObj<Il2CppMenace.Items.Item> item)
    {
        if (from.Untyped.CheckAlive() != AliveStatus.Alive ||
            to.Untyped.CheckAlive() != AliveStatus.Alive ||
            item.Untyped.CheckAlive() != AliveStatus.Alive) return false;

        try
        {
            var itemProxy = item.AsManaged();

            if (!from.AsManaged().Remove(itemProxy))
                return false;

            return to.AsManaged().Place(itemProxy, 0);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.TransferItem", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Clear all items from a container, optionally filtered by slot type.
    /// </summary>
    public static int ClearInventory(
    GameObj<Il2CppMenace.Items.ItemContainer> container, ItemSlot slotType = ItemSlot.None)
    {
        if (container.Untyped.CheckAlive() != AliveStatus.Alive) return 0;

        try
        {
            int removedCount = 0;

            var items = slotType != ItemSlot.None
                ? GetItemsInSlot(container, slotType)
                : GetAllItems(container);

            foreach (var itemInfo in items)
            {
                if (RemoveItem(container, itemInfo.Item))
                    removedCount++;
            }

            return removedCount;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.ClearInventory", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Give an item to the selected actor in tactical mode.
    /// </summary>
    public static string GiveItemToActor(string templateId)
    {
        try
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull)
                return "No actor selected. Select a unit first.";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actor, out var typedActor))
                return "Actor could not be wrapped as Entity";

            if (typedActor.Untyped.CheckAlive() != AliveStatus.Alive)
                return "Actor is no longer alive";

            if (!Templates.TryGet<Il2CppMenace.Items.ItemTemplate>(templateId, out var template))
                return $"Template '{templateId}' not found";

            var container = GetContainer(typedActor);
            if (container.Untyped.CheckAlive() != AliveStatus.Alive)
                return "Actor has no item container";

            var item = template.CreateItem(Guid.NewGuid().ToString());
            if (item == null)
                return "CreateItem returned null";

            container.AsManaged().Place((Il2CppMenace.Items.Item)item, 0);

            return $"Gave {templateId} to {actor.GetName()}";
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GiveItemToActor", "Failed", ex);
            return $"Give failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Spawn an item by template name and add it to OwnedItems (strategy map only).
    /// </summary>
    public static string SpawnItem(string templateId)
    {
        try
        {
            if (!Templates.TryGet<Il2CppMenace.Items.ItemTemplate>(templateId, out var template))
                return $"Template '{templateId}' not found";

            var ownedItems = GetOwnedItems();
            if (ownedItems.Untyped.CheckAlive() != AliveStatus.Alive)
                return "Error: OwnedItems unavailable — are you on the strategy map?";

            var item = ownedItems.AsManaged().AddItem(template, false);
            if (item == null)
                return "AddItem returned null";

            if (!GameObj<Il2CppMenace.Items.Item>.TryWrap(
                GameObj.FromPointer(((Il2CppObjectBase)item).Pointer), out var itemObj))
                return $"Spawned: {templateId} (item added to inventory)";

            return $"Spawned: {templateId} (ID: {itemObj.Untyped.GetName()})";
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.SpawnItem", "Failed", ex);
            return $"Failed to spawn item: {ex.Message}";
        }
    }

    // -------------------------------------------------------------------------
    // Console commands
    // -------------------------------------------------------------------------

    /// <summary>
    /// Register console commands for Inventory SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("inventory", "", "List inventory for selected actor", args =>
        {
            var actorUntyped = TacticalController.GetActiveActor();
            if (actorUntyped.IsNull) return "No actor selected";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                return "No actor selected";

            var container = GetContainer(actor);
            if (container.Untyped.CheckAlive() != AliveStatus.Alive) return "No inventory container";

            var items = GetAllItems(container);
            if (items.Count == 0) return "Inventory empty";

            var lines = new List<string> { $"Inventory ({items.Count} items):" };
            foreach (var item in items)
            {
                var temp = item.IsTemporary ? " [TEMP]" : "";
                lines.Add($"  [{item.SlotType}] {item.TemplateName} (${item.TradeValue}){temp}");
            }
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("weapons", "", "List equipped weapons for selected actor", args =>
        {
            var actorUntyped = TacticalController.GetActiveActor();
            if (actorUntyped.IsNull) return "No actor selected";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                return "No actor selected";

            var weapons = GetEquippedWeapons(actor);
            if (weapons.Count == 0) return "No weapons equipped";

            var lines = new List<string> { "Equipped Weapons:" };
            foreach (var w in weapons)
                lines.Add($"  {w.TemplateName} (Rarity: {w.RarityTier}) - {w.SkillCount} skills");
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("armor", "", "Show equipped armor for selected actor", args =>
        {
            var actorUntyped = TacticalController.GetActiveActor();
            if (actorUntyped.IsNull) return "No actor selected";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                return "No actor selected";

            var armor = GetEquippedArmor(actor);
            if (armor == null) return "No armor equipped";

            return $"Armor: {armor.TemplateName}\n" +
                   $"Rarity: {armor.RarityTier}\n" +
                   $"Trade Value: ${armor.TradeValue}\n" +
                   $"Skills: {armor.SkillCount}";
        });

        DevConsole.RegisterCommand("slot", "<type>", "List items in slot (name)", args =>
        {
            if (args.Length == 0)
                return $"Usage: slot <type>\nTypes: {string.Join(", ", Enum.GetNames(typeof(ItemSlot)).Where(n => n != nameof(ItemSlot.None) && n != nameof(ItemSlot.All) && n != nameof(ItemSlot.COUNT)))}";

            if (!Enum.TryParse<ItemSlot>(args[0], true, out var slotType) ||
                slotType == ItemSlot.None || slotType == ItemSlot.All || slotType == ItemSlot.COUNT)
                return "Invalid slot type";

            var actorUntyped = TacticalController.GetActiveActor();
            if (actorUntyped.IsNull) return "No actor selected";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                return "No actor selected";

            var container = GetContainer(actor);
            if (container.Untyped.CheckAlive() != AliveStatus.Alive) return "No inventory container";

            var items = GetItemsInSlot(container, slotType);
            if (items.Count == 0) return $"No items in {slotType}";

            var lines = new List<string> { $"{slotType} ({items.Count} items):" };
            foreach (var item in items)
                lines.Add($"  {item.TemplateName} (${item.TradeValue})");
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("itemvalue", "", "Get total trade value of inventory", args =>
        {
            var actorUntyped = TacticalController.GetActiveActor();
            if (actorUntyped.IsNull) return "No actor selected";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                return "No actor selected";

            var container = GetContainer(actor);
            if (container.Untyped.IsNull) return "No inventory container";

            var total = GetTotalTradeValue(container);
            var items = GetAllItems(container);
            return $"Total Trade Value: ${total} ({items.Count} items)";
        });

        DevConsole.RegisterCommand("spawn", "<template>",
            "Spawn an item by template name (strategy map only)", args =>
            {
                if (args.Length == 0)
                    return "Usage: spawn <template_name>\nExample: spawn weapon.laser_smg\n" +
                           "Note: Must be on strategy map, not in tactical combat or menus.";
                return SpawnItem(args[0]);
        });

        DevConsole.RegisterCommand("give", "<template>",
            "Give item to selected actor (tactical mode)", args =>
            {
                if (args.Length == 0)
                    return "Usage: give <template_name>\nExample: give weapon.laser_smg";
                return GiveItemToActor(args[0]);
        });

        DevConsole.RegisterCommand("spawnlist", "[filter]",
        "List item templates (optionally filtered)", args =>
        {
            var filter = args.Length > 0 ? args[0] : null;
            var templates = Templates.FindAll<Il2CppMenace.Items.ItemTemplate>()
                .Where(t => filter == null || t.GetID().Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (templates.Count == 0)
                return filter != null ? $"No templates matching '{filter}'" : "No item templates found";

            var lines = new List<string> { $"Item Templates ({templates.Count}):" };
            foreach (var t in templates.Take(50))
                lines.Add($"  {t.GetID()}");
            if (templates.Count > 50)
                lines.Add($"  ... and {templates.Count - 50} more (use filter to narrow down)");
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("spawninfo", "", "Show spawn system debug info", args =>
        {
            var lines = new List<string> { "Spawn System Info:" };

            lines.Add($"  Handles resolved: {_handlesResolved}");

            var ownedItems = GetOwnedItems();
            lines.Add($"  OwnedItems: {(ownedItems.Untyped.CheckAlive() == AliveStatus.Alive ? "Available" : "Unavailable")}");

            var itemTemplates = Templates.FindAll<Il2CppMenace.Items.ItemTemplate>();
            lines.Add($"  ItemTemplate count: {itemTemplates.Count}");

            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("hastag", "<tag>",
        "Check if inventory has item with tag", args =>
        {
            if (args.Length == 0)
                return $"Usage: hastag <tag>\nTags: {string.Join(", ", Enum.GetNames(typeof(TagType)).Where(n => n != nameof(TagType.Last)))}";

            if (!Enum.TryParse<TagType>(args[0], true, out var tagType))
                return $"'{args[0]}' is not a valid TagType";

            var actorUntyped = TacticalController.GetActiveActor();
            if (actorUntyped.IsNull) return "No actor selected";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                return "No actor selected";

            var container = GetContainer(actor);
            if (container.Untyped.CheckAlive() != AliveStatus.Alive) return "No inventory container";

            if (!HasItemWithTag(container, tagType))
                return $"Has tag '{tagType}': No";

            var items = GetItemsWithTag(container, tagType);
            return $"Has tag '{tagType}': Yes ({items.Count} items)";
        });
    }
}
