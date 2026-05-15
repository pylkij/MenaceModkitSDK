using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
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
    private static class Offsets
    {
        // Item @ 0x30 — List<BaseSkill> m_Skills
        internal static readonly Lazy<FieldHandle<Il2CppMenace.Items.Item, IntPtr>> ItemSkills
            = new(() => GameObj<Il2CppMenace.Items.Item>.FieldAt<IntPtr>(0x30, "m_Skills"));

        // Item @ 0x28 — ItemContainer m_Container (pointer field)
        internal static readonly Lazy<ObjFieldHandle<Il2CppMenace.Items.Item, Il2CppMenace.Items.ItemContainer>> ItemContainer
            = new(() => GameObj<Il2CppMenace.Items.Item>.ResolveObjField(x => x.m_Container));

        // ItemContainer @ 0x20 — ItemsModularVehicle m_ModularVehicle (pointer field)
        internal static readonly Lazy<FieldHandle<Il2CppMenace.Items.ItemContainer, IntPtr>> ContainerModularVehicle
            = new(() => GameObj<Il2CppMenace.Items.ItemContainer>.FieldAt<IntPtr>(0x20, "m_ModularVehicle"));

        // BaseItemTemplate @ 0xA0 — int Rarity (public field, direct offset)
        internal static readonly Lazy<FieldHandle<Il2CppMenace.Items.BaseItemTemplate, int>> TemplateRarity
            = new(() => GameObj<Il2CppMenace.Items.BaseItemTemplate>.ResolveField(x => x.Rarity));

        // StrategyState @ 0x80 — OwnedItems OwnedItems (public readonly field)
        internal static readonly Lazy<ObjFieldHandle<Il2CppMenace.States.StrategyState, Il2CppMenace.Strategy.OwnedItems>> StrategyStateOwnedItems
            = new(() => GameObj<Il2CppMenace.States.StrategyState>.ResolveObjField(x => x.OwnedItems));
    }

    // -------------------------------------------------------------------------
    // Cached types (unchanged from original — GameType.Of<T> is not part of
    // the GameObj<T> migration surface, only the GameObj construction sites are)
    // -------------------------------------------------------------------------
    private static readonly GameType _itemTemplateType = GameType.Of<Il2CppMenace.Items.BaseItemTemplate>();
    private static readonly GameType _weaponTemplateType = GameType.Of<Il2CppMenace.Items.WeaponTemplate>();
    private static readonly GameType _armorTemplateType = GameType.Of<Il2CppMenace.Items.ArmorTemplate>();
    private static readonly GameType _strategyStateType = GameType.Of<Il2CppMenace.States.StrategyState>();
    private static readonly GameType _ownedItemsType = GameType.Of<Il2CppMenace.Strategy.OwnedItems>();

    // -------------------------------------------------------------------------
    // Slot type constants (unchanged)
    // -------------------------------------------------------------------------
    public const int SLOT_WEAPON1 = 0;
    public const int SLOT_WEAPON2 = 1;
    public const int SLOT_ARMOR = 2;
    public const int SLOT_ACCESSORY1 = 3;
    public const int SLOT_ACCESSORY2 = 4;
    public const int SLOT_CONSUMABLE1 = 5;
    public const int SLOT_CONSUMABLE2 = 6;
    public const int SLOT_GRENADE = 7;
    public const int SLOT_VEHICLE_WEAPON = 8;
    public const int SLOT_VEHICLE_ARMOR = 9;
    public const int SLOT_VEHICLE_ACCESSORY = 10;
    public const int SLOT_TYPE_COUNT = 11;

    // -------------------------------------------------------------------------
    // Step B (Option B) — ItemInfo.Pointer changed from IntPtr to GameObj<Item>.
    // ClearInventory and any other callers that stored ItemInfo.Pointer and
    // reconstructed a GameObj from it now use the typed wrapper directly.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Item information structure.
    /// </summary>
    public class ItemInfo
    {
        public string GUID { get; set; }
        public string TemplateName { get; set; }
        public int SlotType { get; set; }
        public string SlotTypeName { get; set; }
        public int TradeValue { get; set; }
        public int RarityTier { get; set; }   // int from BaseItemTemplate.GetRarity()
        public int SkillCount { get; set; }
        public bool IsTemporary { get; set; }
        // Typed wrapper replaces raw IntPtr — no untyped reconstruction needed.
        public GameObj<Il2CppMenace.Items.Item> Item { get; set; }
    }

    /// <summary>
    /// Container information structure.
    /// </summary>
    public class ContainerInfo
    {
        public int TotalItems { get; set; }
        public int[] SlotCounts { get; set; }
        public bool HasModularVehicle { get; set; }
        // Typed wrapper replaces raw IntPtr.
        public GameObj<Il2CppMenace.Items.ItemContainer> Container { get; set; }
    }

    // -------------------------------------------------------------------------
    // Step D — Public method signatures updated to GameObj<T> where type is known.
    // Untyped GameObj overloads kept as [Obsolete] bridges for callers not yet
    // migrated. Remove bridges once all callers are updated (Phase 4.3).
    // -------------------------------------------------------------------------

    /// <summary>
    /// Get the global OwnedItems manager.
    /// Returns GameObj.Null when not on the strategy map.
    /// </summary>
    public static GameObj GetOwnedItems()
    {
        try
        {
            var ss = Il2CppMenace.States.StrategyState.Get();
            if (ss == null)
                return GameObj.Null; // Normal when not on the strategy map.

            var ssObj = GameObj<Il2CppMenace.States.StrategyState>.Wrap(ss.Pointer);

            if (!Offsets.StrategyStateOwnedItems.Value.TryRead(ssObj, out var ownedItemsObj))
            {
                SdkLogger.Warning("GetOwnedItems: OwnedItems field is null");
                return GameObj.Null;
            }

            return ownedItemsObj.Untyped;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"GetOwnedItems failed: {ex.Message}");
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the item container for an entity.
    /// </summary>
    // Step D: typed overload — preferred.
    public static GameObj<Il2CppMenace.Items.ItemContainer> GetContainer(
        GameObj<Il2CppMenace.Tactical.Entity> entity)
    {
        if (entity.Untyped.IsNull) return default;

        try
        {
            var proxy = entity.AsManaged();
            var getItems = typeof(Il2CppMenace.Tactical.Entity)
                .GetMethod("GetItems", BindingFlags.Public | BindingFlags.Instance);
            var container = getItems?.Invoke(proxy, null);
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

    // Step D: untyped bridge for callers not yet migrated.
    [Obsolete("Use GameObj<Entity> overload")]
    public static GameObj GetContainer(GameObj entity)
    {
        if (entity.IsNull) return GameObj.Null;
        if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(entity, out var typed))
            return GameObj.Null;
        var result = GetContainer(typed);
        return result.Untyped.IsNull ? GameObj.Null : result.Untyped;
    }

    /// <summary>
    /// Get all items in a container.
    /// </summary>
    // Step D: typed overload.
    public static List<ItemInfo> GetAllItems(
        GameObj<Il2CppMenace.Items.ItemContainer> container)
    {
        var result = new List<ItemInfo>();
        if (container.Untyped.IsNull) return result;

        try
        {
            var proxy = container.AsManaged();
            // ItemContainer.GetAllItems() returns List<Item> — confirmed from dump.
            var items = proxy.GetAllItems();
            if (items == null) return result;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                // Step A: was new GameObj(((Il2CppObjectBase)item).Pointer)
                var itemObj = GameObj<Il2CppMenace.Items.Item>.Wrap(
                    ((Il2CppObjectBase)item).Pointer);
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

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static List<ItemInfo> GetAllItems(GameObj container)
    {
        if (container.IsNull) return new List<ItemInfo>();
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return new List<ItemInfo>();
        return GetAllItems(typed);
    }

    /// <summary>
    /// Get items in a specific slot type.
    /// </summary>
    // Step D: typed overload.
    public static List<ItemInfo> GetItemsInSlot(
        GameObj<Il2CppMenace.Items.ItemContainer> container, int slotType)
    {
        var result = new List<ItemInfo>();
        if (container.Untyped.IsNull || slotType < 0 || slotType >= SLOT_TYPE_COUNT)
            return result;

        try
        {
            var proxy = container.AsManaged();
            // GetAllItemsAtSlot takes ItemSlot enum — cast int to enum.
            var items = proxy.GetAllItemsAtSlotCopy((Il2CppMenace.Items.ItemSlot)slotType);
            if (items == null) return result;

            foreach (var item in items)
            {
                if (item == null) continue;

                var itemObj = GameObj<Il2CppMenace.Items.Item>.Wrap(
                    ((Il2CppObjectBase)item).Pointer);
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

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static List<ItemInfo> GetItemsInSlot(GameObj container, int slotType)
    {
        if (container.IsNull) return new List<ItemInfo>();
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return new List<ItemInfo>();
        return GetItemsInSlot(typed, slotType);
    }

    /// <summary>
    /// Get the item at a specific slot and index.
    /// </summary>
    // Step D: typed overload.
    public static GameObj<Il2CppMenace.Items.Item> GetItemAt(
        GameObj<Il2CppMenace.Items.ItemContainer> container, int slotType, int index)
    {
        if (container.Untyped.IsNull) return default;

        try
        {
            var proxy = container.AsManaged();
            // GetItemAtSlot(ItemSlot) — single-slot overload; index not supported
            // by this overload. Use GetAllItemsAtSlot and index into result.
            var items = proxy.GetAllItemsAtSlotCopy((Il2CppMenace.Items.ItemSlot)slotType);
            if (items == null || index < 0 || index >= items.Length) return default;

            var item = items[index];
            if (item == null) return default;

            // Step A: was new GameObj(((Il2CppObjectBase)item).Pointer)
            return GameObj<Il2CppMenace.Items.Item>.Wrap(
                ((Il2CppObjectBase)item).Pointer);
        }
        catch
        {
            return default;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static GameObj GetItemAt(GameObj container, int slotType, int index)
    {
        if (container.IsNull) return GameObj.Null;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return GameObj.Null;
        var result = GetItemAt(typed, slotType, index);
        return result.Untyped.IsNull ? GameObj.Null : result.Untyped;
    }

    /// <summary>
    /// Get item information.
    /// </summary>
    // Step D: typed overload.
    public static ItemInfo GetItemInfo(GameObj<Il2CppMenace.Items.Item> item)
    {
        if (item.Untyped.IsNull) return null;

        try
        {
            var proxy = item.AsManaged();
            var info = new ItemInfo { Item = item };

            // GUID — Item.GetID() confirmed on Item (not BaseItem).
            info.GUID = proxy.GetID();

            // Template — Item.GetTemplate() returns ItemTemplate (subtype of BaseItemTemplate).
            var template = proxy.GetTemplate();
            if (template != null)
            {
                info.TemplateName = template.name; // Unity name, same as GetName() used before.

                // SlotType — resolve m_SlotType by name on BaseItemTemplate's subtype.
                // BaseItemTemplate does not expose SlotType in the dump; resolution falls
                // through to OffsetCache on the runtime class pointer of the template.
                var templateKlass = IL2CPP.il2cpp_object_get_class(
                    ((Il2CppObjectBase)template).Pointer);
                info.SlotType = item.Untyped.ReadInt(
                    OffsetCache.GetOrResolve(templateKlass, "m_SlotType"));
                info.SlotTypeName = GetSlotTypeName(info.SlotType);

                // TradeValue — BaseItemTemplate.GetTradeValue() is the zero-arg virtual.
                // BaseItem.GetTradeValue(float mult) is a different method; don't use it here.
                info.TradeValue = template.GetTradeValue();

                // Rarity — BaseItemTemplate.GetRarity() returns int directly.
                info.RarityTier = template.GetRarity();
            }

            // IsTemporary — BaseItem.IsTemporary() confirmed on BaseItem.
            // AsManaged() on a GameObj<Item> gives us Item which inherits BaseItem.
            info.IsTemporary = proxy.IsTemporary();

            // SkillCount — m_Skills at 0x30 on Item, type List<BaseSkill>.
            // Not an unmanaged field; read the list pointer then wrap as GameList.
            if (Offsets.ItemSkills.Value.TryRead(item, out var skillsPtr) &&
                skillsPtr != IntPtr.Zero)
            {
                var skillsList = new GameList(skillsPtr);
                info.SkillCount = skillsList.Count;
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemInfo", "Failed", ex);
            return null;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<Item> overload")]
    public static ItemInfo GetItemInfo(GameObj item)
    {
        if (item.IsNull) return null;
        if (!GameObj<Il2CppMenace.Items.Item>.TryWrap(item, out var typed)) return null;
        return GetItemInfo(typed);
    }

    /// <summary>
    /// Get container information.
    /// </summary>
    // Step D: typed overload.
    public static ContainerInfo GetContainerInfo(
        GameObj<Il2CppMenace.Items.ItemContainer> container)
    {
        if (container.Untyped.IsNull) return null;

        try
        {
            var proxy = container.AsManaged();
            var info = new ContainerInfo
            {
                Container = container,
                SlotCounts = new int[SLOT_TYPE_COUNT]
            };

            for (int slot = 0; slot < SLOT_TYPE_COUNT; slot++)
            {
                // GetItemSlotCount(ItemSlot) — cast int to enum.
                info.SlotCounts[slot] = proxy.GetItemSlotCount(
                    (Il2CppMenace.Items.ItemSlot)slot);
                info.TotalItems += info.SlotCounts[slot];
            }

            // HasModularVehicle — m_ModularVehicle at +0x20, non-null = has vehicle.
            if (Offsets.ContainerModularVehicle.Value.TryRead(container, out var modVehiclePtr))
                info.HasModularVehicle = modVehiclePtr != IntPtr.Zero;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetContainerInfo", "Failed", ex);
            return null;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static ContainerInfo GetContainerInfo(GameObj container)
    {
        if (container.IsNull) return null;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return null;
        return GetContainerInfo(typed);
    }

    /// <summary>
    /// Find an item by GUID.
    /// </summary>
    public static GameObj FindByGUID(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return GameObj.Null;

        try
        {
            var ownedItems = GetOwnedItems();
            if (ownedItems.IsNull) return GameObj.Null;

            var ownedType = _ownedItemsType?.ManagedType;
            if (ownedType == null) return GameObj.Null;

            var proxy = GetManagedProxy(ownedItems, ownedType);
            if (proxy == null) return GameObj.Null;

            var getByGuidMethod = ownedType.GetMethod("GetItemByGuid",
                BindingFlags.Public | BindingFlags.Instance);
            var item = getByGuidMethod?.Invoke(proxy, new object[] { guid });
            if (item == null) return GameObj.Null;

            // Step A: was new GameObj(((Il2CppObjectBase)item).Pointer)
            return GameObj<Il2CppMenace.Items.Item>.Wrap(
                ((Il2CppObjectBase)item).Pointer).Untyped;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Check if a container has an item with a specific tag.
    /// NOTE: ItemContainer.ContainsTag takes a TagType enum. This method accepts
    /// a string and parses it to TagType. If parsing fails, returns false.
    /// </summary>
    // Step D: typed overload.
    public static bool HasItemWithTag(
        GameObj<Il2CppMenace.Items.ItemContainer> container, string tag)
    {
        if (container.Untyped.IsNull || string.IsNullOrEmpty(tag)) return false;

        try
        {
            if (!Enum.TryParse<Il2CppMenace.Tags.TagType>(tag, true, out var tagType))
            {
                SdkLogger.Warning($"HasItemWithTag: '{tag}' is not a valid TagType");
                return false;
            }
            return container.AsManaged().ContainsTag(tagType);
        }
        catch
        {
            return false;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static bool HasItemWithTag(GameObj container, string tag)
    {
        if (container.IsNull) return false;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return false;
        return HasItemWithTag(typed, tag);
    }

    /// <summary>
    /// Get items with a specific tag.
    /// NOTE: ItemContainer.GetItemsWithTag returns an int (count), not a list.
    /// This method iterates all items and filters by checking the tag on each
    /// item's template (BaseItemTemplate.HasTag). If tag string is not a valid
    /// TagType, returns empty list.
    /// </summary>
    // Step D: typed overload.
    public static List<ItemInfo> GetItemsWithTag(
        GameObj<Il2CppMenace.Items.ItemContainer> container, string tag)
    {
        var result = new List<ItemInfo>();
        if (container.Untyped.IsNull || string.IsNullOrEmpty(tag)) return result;

        if (!Enum.TryParse<Il2CppMenace.Tags.TagType>(tag, true, out var tagType))
        {
            SdkLogger.Warning($"GetItemsWithTag: '{tag}' is not a valid TagType");
            return result;
        }

        try
        {
            var allItems = GetAllItems(container);
            foreach (var itemInfo in allItems)
            {
                if (itemInfo.Item.Untyped.IsNull) continue;

                // HasTag is on BaseItemTemplate, not BaseItem — check via template.
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

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static List<ItemInfo> GetItemsWithTag(GameObj container, string tag)
    {
        if (container.IsNull) return new List<ItemInfo>();
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return new List<ItemInfo>();
        return GetItemsWithTag(typed, tag);
    }

    /// <summary>
    /// Get equipped weapons for an entity.
    /// </summary>
    // Step D: typed overload.
    public static List<ItemInfo> GetEquippedWeapons(
        GameObj<Il2CppMenace.Tactical.Entity> entity)
    {
        var result = new List<ItemInfo>();
        var container = GetContainer(entity);
        if (container.Untyped.IsNull) return result;

        result.AddRange(GetItemsInSlot(container, SLOT_WEAPON1));
        result.AddRange(GetItemsInSlot(container, SLOT_WEAPON2));
        return result;
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<Entity> overload")]
    public static List<ItemInfo> GetEquippedWeapons(GameObj entity)
    {
        if (entity.IsNull) return new List<ItemInfo>();
        if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(entity, out var typed))
            return new List<ItemInfo>();
        return GetEquippedWeapons(typed);
    }

    /// <summary>
    /// Get equipped armor for an entity.
    /// </summary>
    // Step D: typed overload.
    public static ItemInfo GetEquippedArmor(
        GameObj<Il2CppMenace.Tactical.Entity> entity)
    {
        var container = GetContainer(entity);
        if (container.Untyped.IsNull) return null;

        var items = GetItemsInSlot(container, SLOT_ARMOR);
        return items.Count > 0 ? items[0] : null;
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<Entity> overload")]
    public static ItemInfo GetEquippedArmor(GameObj entity)
    {
        if (entity.IsNull) return null;
        if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(entity, out var typed))
            return null;
        return GetEquippedArmor(typed);
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

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static int GetTotalTradeValue(GameObj container)
    {
        if (container.IsNull) return 0;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return 0;
        return GetTotalTradeValue(typed);
    }

    /// <summary>
    /// Remove a specific item from a container.
    /// NOTE: The method on ItemContainer is Remove(Item, bool), not RemoveItem.
    /// The original code searched for "RemoveItem" which never existed and was
    /// silently a no-op. This is now fixed.
    /// </summary>
    // Step D: typed overload.
    public static bool RemoveItem(
        GameObj<Il2CppMenace.Items.ItemContainer> container,
        GameObj<Il2CppMenace.Items.Item> item)
    {
        if (container.Untyped.IsNull || item.Untyped.IsNull) return false;

        try
        {
            // ItemContainer.Remove(Item, bool fireVisualAlterationChangedEvent = true)
            return container.AsManaged().Remove(item.AsManaged());
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.RemoveItem", "Failed", ex);
            return false;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer>/GameObj<Item> overload")]
    public static bool RemoveItem(GameObj container, GameObj item)
    {
        if (container.IsNull || item.IsNull) return false;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typedContainer))
            return false;
        if (!GameObj<Il2CppMenace.Items.Item>.TryWrap(item, out var typedItem))
            return false;
        return RemoveItem(typedContainer, typedItem);
    }

    /// <summary>
    /// Remove item at a specific slot and index.
    /// </summary>
    // Step D: typed overload.
    public static bool RemoveItemAt(
        GameObj<Il2CppMenace.Items.ItemContainer> container, int slotType, int index)
    {
        if (container.Untyped.IsNull || slotType < 0 || slotType >= SLOT_TYPE_COUNT)
            return false;

        try
        {
            // Use Remove(ItemSlot, int index) overload — avoids a round-trip through GetItemAt.
            return container.AsManaged().Remove(
                (Il2CppMenace.Items.ItemSlot)slotType, index);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.RemoveItemAt", "Failed", ex);
            return false;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static bool RemoveItemAt(GameObj container, int slotType, int index)
    {
        if (container.IsNull) return false;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return false;
        return RemoveItemAt(typed, slotType, index);
    }

    /// <summary>
    /// Transfer an item from one container to another.
    /// NOTE: Place(Item, int, bool) requires an index. We pass 0 here to place
    /// at the first available position; the game resolves the actual slot from
    /// the item's template type.
    /// </summary>
    // Step D: typed overload.
    public static bool TransferItem(
        GameObj<Il2CppMenace.Items.ItemContainer> from,
        GameObj<Il2CppMenace.Items.ItemContainer> to,
        GameObj<Il2CppMenace.Items.Item> item)
    {
        if (from.Untyped.IsNull || to.Untyped.IsNull || item.Untyped.IsNull)
            return false;

        try
        {
            var itemProxy = item.AsManaged();

            // Remove from source. Remove returns bool.
            if (!from.AsManaged().Remove(itemProxy))
                return false;

            // Add to destination. Place(Item, int index, bool fireEvents).
            return to.AsManaged().Place(itemProxy, 0);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.TransferItem", "Failed", ex);
            return false;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer>/GameObj<Item> overload")]
    public static bool TransferItem(GameObj from, GameObj to, GameObj item)
    {
        if (from.IsNull || to.IsNull || item.IsNull) return false;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(from, out var typedFrom)) return false;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(to, out var typedTo)) return false;
        if (!GameObj<Il2CppMenace.Items.Item>.TryWrap(item, out var typedItem)) return false;
        return TransferItem(typedFrom, typedTo, typedItem);
    }

    /// <summary>
    /// Clear all items from a container, optionally filtered by slot type.
    /// </summary>
    // Step D: typed overload.
    public static int ClearInventory(
        GameObj<Il2CppMenace.Items.ItemContainer> container, int slotType = -1)
    {
        if (container.Untyped.IsNull) return 0;

        try
        {
            int removedCount = 0;

            if (slotType >= 0 && slotType < SLOT_TYPE_COUNT)
            {
                var items = GetItemsInSlot(container, slotType);
                foreach (var itemInfo in items)
                {
                    // Step B (Option B): ItemInfo.Item is GameObj<Item> — no reconstruction.
                    if (RemoveItem(container, itemInfo.Item))
                        removedCount++;
                }
            }
            else
            {
                var allItems = GetAllItems(container);
                foreach (var itemInfo in allItems)
                {
                    if (RemoveItem(container, itemInfo.Item))
                        removedCount++;
                }
            }

            return removedCount;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.ClearInventory", "Failed", ex);
            return 0;
        }
    }

    // Step D: untyped bridge.
    [Obsolete("Use GameObj<ItemContainer> overload")]
    public static int ClearInventory(GameObj container, int slotType = -1)
    {
        if (container.IsNull) return 0;
        if (!GameObj<Il2CppMenace.Items.ItemContainer>.TryWrap(container, out var typed))
            return 0;
        return ClearInventory(typed, slotType);
    }

    // -------------------------------------------------------------------------
    // Slot type name helper (unchanged)
    // -------------------------------------------------------------------------
    public static string GetSlotTypeName(int slotType) => slotType switch
    {
        0 => "Weapon1",
        1 => "Weapon2",
        2 => "Armor",
        3 => "Accessory1",
        4 => "Accessory2",
        5 => "Consumable1",
        6 => "Consumable2",
        7 => "Grenade",
        8 => "VehicleWeapon",
        9 => "VehicleArmor",
        10 => "VehicleAccessory",
        _ => $"Slot{slotType}"
    };

    // -------------------------------------------------------------------------
    // Item template helpers — FindItemTemplate, GetItemTemplates
    // These work via Unity Resources and don't construct GameObj from pointers
    // returned by game methods, so they use the typed factory directly.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Find an item template by name. Searches WeaponTemplate, ArmorTemplate,
    /// then BaseItemTemplate.
    /// </summary>
    public static GameObj FindItemTemplate(string templateName)
    {
        try
        {
            var typesToSearch = new[]
            {
                _weaponTemplateType?.ManagedType,
                _armorTemplateType?.ManagedType,
                _itemTemplateType?.ManagedType
            };

            foreach (var templateType in typesToSearch)
            {
                if (templateType == null) continue;

                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);
                if (objects == null) continue;

                foreach (var obj in objects)
                {
                    if (obj != null &&
                        obj.name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Step A: was new GameObj(((Il2CppObjectBase)obj).Pointer)
                        return GameObj<Il2CppMenace.Items.BaseItemTemplate>.Wrap(
                            ((Il2CppObjectBase)obj).Pointer).Untyped;
                    }
                }
            }

            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all item template names, optionally filtered.
    /// </summary>
    public static List<string> GetItemTemplates(string filter = null)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var typesToSearch = new[]
            {
                _weaponTemplateType?.ManagedType,
                _armorTemplateType?.ManagedType,
                _itemTemplateType?.ManagedType
            };

            foreach (var templateType in typesToSearch)
            {
                if (templateType == null) continue;

                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);
                if (objects == null) continue;

                foreach (var obj in objects)
                {
                    if (obj == null || string.IsNullOrEmpty(obj.name)) continue;
                    if (filter == null ||
                        obj.name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        result.Add(obj.name);
                }
            }

            var sorted = result.ToList();
            sorted.Sort();
            return sorted;
        }
        catch
        {
            return new List<string>();
        }
    }

    // -------------------------------------------------------------------------
    // High-level commands — GiveItemToActor, SpawnItem
    // These compose the typed API above. GameObj construction sites here that
    // come back from managed-proxy calls use typed wrappers.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Give an item to the selected actor in tactical mode.
    /// </summary>
    public static string GiveItemToActor(string templateName)
    {
        try
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull)
                return "No actor selected. Select a unit first.";

            var template = FindItemTemplate(templateName);
            if (template.IsNull)
                return $"Template '{templateName}' not found. Use 'spawnlist {templateName}' to search.";

            // GetContainer needs typed entity — bridge via UntypedFromPointer_Migrate
            // until TacticalController.GetActiveActor is migrated to return GameObj<Entity>.
            GameObj<Il2CppMenace.Items.ItemContainer> container;
            if (GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actor, out var typedActor))
                container = GetContainer(typedActor);
            else
                return "Actor could not be wrapped as Entity";

            if (container.Untyped.IsNull)
                return "Actor has no item container";

            var templateType = _itemTemplateType?.ManagedType;
            if (templateType == null)
                return "BaseItemTemplate type not found";

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null)
                return "Failed to get template proxy";

            var createItemMethod = templateType.GetMethod("CreateItem",
                BindingFlags.Public | BindingFlags.Instance);
            if (createItemMethod == null)
                return "CreateItem method not found";

            var guid = Guid.NewGuid().ToString();
            var item = createItemMethod.Invoke(templateProxy, new object[] { guid });
            if (item == null)
                return "CreateItem returned null";

            // Place(Item, int index, bool fireEvents) — confirmed signature from dump.
            var itemProxy = item as Il2CppMenace.Items.Item
                ?? (Il2CppMenace.Items.Item)item;
            container.AsManaged().Place(itemProxy, 0);

            return $"Gave {templateName} to {actor.GetName()}";
        }
        catch (Exception ex)
        {
            return $"Give failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Spawn an item by template name and add it to OwnedItems (strategy map only).
    /// </summary>
    public static string SpawnItem(string templateName)
    {
        try
        {
            var template = FindItemTemplate(templateName);
            if (template.IsNull)
                return $"Template '{templateName}' not found. Use 'spawnlist {templateName}' to search.";

            var ownedItems = GetOwnedItems();
            if (ownedItems.IsNull)
            {
                var ssType = _strategyStateType?.ManagedType;
                if (ssType == null)
                    return "Error: StrategyState type not found";

                var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var ss = getMethod?.Invoke(null, null);
                if (ss == null)
                    return "Error: StrategyState.Get() returned null (are you on the strategy map?)";

                var ssObj = GameObj<Il2CppMenace.States.StrategyState>.Wrap(
                    ((Il2CppObjectBase)ss).Pointer);

                if (!Offsets.StrategyStateOwnedItems.Value.TryRead(ssObj, out _))
                    return "Error: StrategyState OwnedItems field is null";

                return "Error: Could not get OwnedItems";
            }

            var ownedType = _ownedItemsType?.ManagedType;
            if (ownedType == null)
                return "OwnedItems type not found";

            var ownedProxy = GetManagedProxy(ownedItems, ownedType);
            if (ownedProxy == null)
                return "Failed to get OwnedItems proxy";

            var addItemMethod = ownedType.GetMethod("AddItem",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { _itemTemplateType.ManagedType, typeof(bool) },
                null);

            if (addItemMethod == null)
            {
                // Fallback: search by parameter shape.
                addItemMethod = ownedType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "AddItem")
                    .FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length == 2 && ps[1].ParameterType == typeof(bool);
                    });
            }

            if (addItemMethod == null)
                return "AddItem method not found on OwnedItems";

            var templateProxy = GetManagedProxy(template, _itemTemplateType.ManagedType);
            if (templateProxy == null)
                return "Failed to get template proxy";

            var item = addItemMethod.Invoke(ownedProxy, new object[] { templateProxy, false });

            if (item != null)
            {
                // Step A: was new GameObj(((Il2CppObjectBase)item).Pointer)
                var itemObj = GameObj<Il2CppMenace.Items.Item>.Wrap(
                    ((Il2CppObjectBase)item).Pointer);
                return $"Spawned: {templateName} (ID: {itemObj.Untyped.GetName()})";
            }

            return $"Spawned: {templateName} (item added to inventory)";
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            return $"Failed to spawn item: {inner.Message}";
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
            if (container.Untyped.IsNull) return "No inventory container";

            var items = GetAllItems(container);
            if (items.Count == 0) return "Inventory empty";

            var lines = new List<string> { $"Inventory ({items.Count} items):" };
            foreach (var item in items)
            {
                var temp = item.IsTemporary ? " [TEMP]" : "";
                lines.Add($"  [{item.SlotTypeName}] {item.TemplateName} (${item.TradeValue}){temp}");
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

        DevConsole.RegisterCommand("slot", "<type>", "List items in slot (0-10 or name)", args =>
        {
            if (args.Length == 0)
                return "Usage: slot <type>\nTypes: 0-10 or Weapon1/Weapon2/Armor/Accessory1/Accessory2/Consumable1/Consumable2/Grenade";

            var actorUntyped = TacticalController.GetActiveActor();
            if (actorUntyped.IsNull) return "No actor selected";

            if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                return "No actor selected";

            int slotType;
            if (!int.TryParse(args[0], out slotType))
            {
                slotType = args[0].ToLower() switch
                {
                    "weapon1" => 0,
                    "weapon2" => 1,
                    "armor" => 2,
                    "accessory1" => 3,
                    "accessory2" => 4,
                    "consumable1" => 5,
                    "consumable2" => 6,
                    "grenade" => 7,
                    _ => -1
                };
            }

            if (slotType < 0 || slotType >= SLOT_TYPE_COUNT) return "Invalid slot type";

            var container = GetContainer(actor);
            if (container.Untyped.IsNull) return "No inventory container";

            var items = GetItemsInSlot(container, slotType);
            if (items.Count == 0) return $"No items in {GetSlotTypeName(slotType)}";

            var lines = new List<string> { $"{GetSlotTypeName(slotType)} ({items.Count} items):" };
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
                var templates = GetItemTemplates(filter);

                if (templates.Count == 0 && filter != null)
                    templates = GetItemTemplates(null)
                        .Where(t => t.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                if (templates.Count == 0)
                    return filter != null ? $"No templates matching '{filter}'" : "No item templates found";

                var lines = new List<string> { $"Item Templates ({templates.Count}):" };
                foreach (var t in templates.Take(50))
                    lines.Add($"  {t}");
                if (templates.Count > 50)
                    lines.Add($"  ... and {templates.Count - 50} more (use filter to narrow down)");
                return string.Join("\n", lines);
            });

        DevConsole.RegisterCommand("spawninfo", "", "Show spawn system debug info", args =>
        {
            var lines = new List<string> { "Spawn System Info:" };

            lines.Add($"  WeaponTemplate type:    {(_weaponTemplateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  ArmorTemplate type:     {(_armorTemplateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  BaseItemTemplate type:  {(_itemTemplateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  StrategyState type:     {(_strategyStateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  OwnedItems type:        {(_ownedItemsType != null ? "Found" : "NOT FOUND")}");

            if (_strategyStateType?.ManagedType != null)
            {
                var getMethod = _strategyStateType.ManagedType
                    .GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var ss = getMethod?.Invoke(null, null);
                lines.Add($"  StrategyState.Get(): {(ss != null ? "Available" : "NULL")}");

                if (ss != null)
                {
                    var ssObj = GameObj<Il2CppMenace.States.StrategyState>.Wrap(
                        ((Il2CppObjectBase)ss).Pointer);
                    var available = Offsets.StrategyStateOwnedItems.Value.TryRead(ssObj, out _);
                    lines.Add($"  StrategyState.m_OwnedItems: {(available ? "Available" : "NULL")}");
                }
            }

            if (_weaponTemplateType?.ManagedType != null)
            {
                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(
                    _weaponTemplateType.ManagedType);
                var weapons = Resources.FindObjectsOfTypeAll(il2cppType);
                lines.Add($"  WeaponTemplate count: {weapons?.Length ?? 0}");
            }

            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("hastag", "<tag>",
            "Check if inventory has item with tag", args =>
            {
                if (args.Length == 0) return "Usage: hastag <tag>";

                var actorUntyped = TacticalController.GetActiveActor();
                if (actorUntyped.IsNull) return "No actor selected";

                if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(actorUntyped, out var actor))
                    return "No actor selected";

                var container = GetContainer(actor);
                if (container.Untyped.IsNull) return "No inventory container";

                var hasTag = HasItemWithTag(container, args[0]);
                if (hasTag)
                {
                    var items = GetItemsWithTag(container, args[0]);
                    return $"Has tag '{args[0]}': Yes ({items.Count} items)";
                }
                return $"Has tag '{args[0]}': No";
            });
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);
}