using System;
using System.Collections.Generic;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for vehicle operations.
/// Provides safe access to vehicle health, armor, modular equipment, and twin-fire detection.
/// </summary>
public static class Vehicle
{
    // ═══════════════════════════════════════════════════════════════════
    //  Field Handles — resolved once in OnSceneLoaded, never at call site
    // ═══════════════════════════════════════════════════════════════════

    // Vehicle fields
    private static FieldHandle<Il2CppMenace.Strategy.Vehicle, float> _hHitpointsPct;
    private static FieldHandle<Il2CppMenace.Strategy.Vehicle, float> _hArmorDurabilityPct;
    private static ObjFieldHandle<Il2CppMenace.Strategy.Vehicle, Il2CppMenace.Tactical.EntityTemplate> _hEntityTemplate;

    // ItemContainer fields
    private static ObjFieldHandle<Il2CppMenace.Items.ItemContainer, Il2CppMenace.Strategy.ItemsModularVehicle> _hModularVehicle;

    // ItemsModularVehicle fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.ItemsModularVehicle, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppMenace.Strategy.ItemsModularVehicle.Slot>> _hSlots;
    private static FieldHandle<Il2CppMenace.Strategy.ItemsModularVehicle, bool> _hIsTwinFire;

    // ItemsModularVehicle.Slot fields
    private static ObjFieldHandle<Il2CppMenace.Strategy.ItemsModularVehicle.Slot, Il2CppMenace.Strategy.ModularVehicleSlot> _hSlotData;
    private static ObjFieldHandle<Il2CppMenace.Strategy.ItemsModularVehicle.Slot, Il2CppMenace.Items.Item> _hMountedWeapon;

    // ModularVehicleSlot fields (template data)
    private static FieldHandle<Il2CppMenace.Strategy.ModularVehicleSlot, Il2CppMenace.Strategy.ModularVehicleSlotType> _hSlotType;

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
            _hHitpointsPct = GameObj<Il2CppMenace.Strategy.Vehicle>.ResolveField(x => x.m_HitpointsPct);
            _hArmorDurabilityPct = GameObj<Il2CppMenace.Strategy.Vehicle>.ResolveField(x => x.m_ArmorDurabilityPct);
            _hEntityTemplate = GameObj<Il2CppMenace.Strategy.Vehicle>.ResolveObjField(x => x.EntityTemplate);

            _hModularVehicle = GameObj<Il2CppMenace.Items.ItemContainer>.ResolveObjField(x => x.m_ModularVehicle);

            _hSlots = GameObj<Il2CppMenace.Strategy.ItemsModularVehicle>.ResolveObjField(x => x.Slots);
            _hIsTwinFire = GameObj<Il2CppMenace.Strategy.ItemsModularVehicle>.ResolveField(x => x.IsTwinFire);

            _hSlotData = GameObj<Il2CppMenace.Strategy.ItemsModularVehicle.Slot>.ResolveObjField(x => x.Data);
            _hMountedWeapon = GameObj<Il2CppMenace.Strategy.ItemsModularVehicle.Slot>.ResolveObjField(x => x.MountedWeapon);

            _hSlotType = GameObj<Il2CppMenace.Strategy.ModularVehicleSlot>.ResolveField(x => x.SlotType);

            _handlesResolved = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error("Vehicle.ResolveHandles: Field handle resolution failed", ex);
        }
    }

    /// <summary>
    /// Vehicle information structure.
    /// </summary>
    public class VehicleInfo
    {
        public string TemplateId { get; set; }
        public float HitpointsPct { get; set; }
        public float ArmorDurabilityPct { get; set; }
        public int BaseHp { get; set; }
        public int MaxHp { get; set; }
        public int Armor { get; set; }
        public int EquippedSlots { get; set; }
        public bool HasTwinFire { get; set; }
        public List<SlotInfo> Slots { get; set; } = new();
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Slot information structure.
    /// </summary>
    public class SlotInfo
    {
        public Il2CppMenace.Strategy.ModularVehicleSlotType SlotType { get; set; }
        public string EquippedItemId { get; set; }
        public bool HasItem { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get vehicle information for an entity.
    /// </summary>
    public static VehicleInfo GetVehicleInfo(GameObj entity)
    {
        if (entity.IsNull) return null;

        if (!GameObj<Il2CppMenace.Strategy.Vehicle>.TryWrap(entity, out var vehicleObj))
            return null;

        var info = new VehicleInfo { Pointer = entity.Pointer };

        if (_hEntityTemplate.TryRead(vehicleObj, out var templateObj))
            if (GameObj<Il2CppMenace.Tools.DataTemplate>.TryWrap(templateObj.Untyped, out var dataTemplateObj))
                if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var templateId))
                    info.TemplateId = templateId;

        if (_hHitpointsPct.TryRead(vehicleObj, out var hpPct))
            info.HitpointsPct = hpPct;

        if (_hArmorDurabilityPct.TryRead(vehicleObj, out var armorPct))
            info.ArmorDurabilityPct = armorPct;

        info.BaseHp = GameMethod.CallInt<Il2CppMenace.Strategy.Vehicle>(vehicleObj, x => x.GetBaseHp());
        info.MaxHp = GameMethod.CallInt<Il2CppMenace.Strategy.Vehicle>(vehicleObj, x => x.GetBaseMaxHp());
        info.Armor = GameMethod.CallInt<Il2CppMenace.Strategy.Vehicle>(vehicleObj, x => x.GetArmor());

        GetModularVehicle(entity, info);

        return info;
    }

    /// <summary>
    /// Reads modular vehicle data into the provided VehicleInfo instance.
    /// </summary>
    private static void GetModularVehicle(GameObj entity, VehicleInfo info)
    {
        if (!GameObj<Il2CppMenace.Tactical.Entity>.TryWrap(entity, out var typedEntity))
            return;

        var container = Inventory.GetContainer(typedEntity);
        if (container.Untyped.IsNull) return;

        if (!_hModularVehicle.TryRead(container, out var modVehicleObj)) return;
        if (modVehicleObj.Untyped.IsNull) return;

        if (_hIsTwinFire.TryRead(modVehicleObj, out var isTwinFire))
            info.HasTwinFire = isTwinFire;

        if (!_hSlots.TryRead(modVehicleObj, out var slotsObj)) return;

        var slots = slotsObj.AsManaged();
        if (slots == null) return;

        foreach (var slot in slots)
        {
            if (slot == null) continue;
            if (!GameObj<Il2CppMenace.Strategy.ItemsModularVehicle.Slot>.TryWrap(GameObj.FromPointer(slot.Pointer), out var slotObj))
                continue;
            var slotInfo = GetSlotInfo(slotObj);
            if (slotInfo == null) continue;
            info.Slots.Add(slotInfo);
            if (slotInfo.HasItem)
                info.EquippedSlots++;
        }
    }

    /// <summary>
    /// Get slot information.
    /// </summary>
    public static SlotInfo GetSlotInfo(GameObj<Il2CppMenace.Strategy.ItemsModularVehicle.Slot> slotObj)
    {
        var info = new SlotInfo { Pointer = slotObj.Untyped.Pointer };

        if (_hSlotData.TryRead(slotObj, out var dataObj))
            if (_hSlotType.TryRead(dataObj, out var slotType))
                info.SlotType = slotType;

        if (_hMountedWeapon.TryRead(slotObj, out var weaponObj))
        {
            info.HasItem = true;
            if (GameObj<Il2CppMenace.Tools.DataTemplate>.TryWrap(weaponObj.Untyped, out var dataTemplateObj))
                if (Templates._hDataTemplateId.TryRead(dataTemplateObj, out var weaponId))
                    info.EquippedItemId = weaponId;
        }

        return info;
    }

    /// <summary>
    /// Check if entity is a vehicle.
    /// </summary>
    public static bool IsVehicle(GameObj entity)
    {
        if (entity.IsNull) return false;
        return GameMethod.CallBool<Il2CppMenace.Tactical.Entity>(entity, x => x.IsVehicle());
    }

    /// <summary>
    /// Fully heals the vehicle and clears all active damage effects.
    /// </summary>
    public static void HealAndClearDamageEffects(GameObj entity)
    {
        if (entity.IsNull) return;
        if (!IsVehicle(entity)) return;
        GameMethod.Call<Il2CppMenace.Strategy.Vehicle>(entity, x => x.HealAndClearDamageEffects());
    }

    /// <summary>
    /// Sets the vehicle's hitpoints as a percentage of maximum.
    /// </summary>
    /// <param name="entity">The vehicle entity.</param>
    /// <param name="value">Percentage value between 0.0 and 1.0.</param>
    public static void SetHitpointsPct(GameObj entity, float value)
    {
        if (entity.IsNull) return;
        if (!IsVehicle(entity)) return;
        GameMethod.Call<Il2CppMenace.Strategy.Vehicle>(entity, x => x.SetHitpointsPct(value));
    }

    /// <summary>
    /// Sets the vehicle's armor durability as a percentage of maximum.
    /// </summary>
    /// <param name="entity">The vehicle entity.</param>
    /// <param name="value">Percentage value between 0.0 and 1.0.</param>
    public static void SetArmorDurabilityPct(GameObj entity, float value)
    {
        if (entity.IsNull) return;
        if (!IsVehicle(entity)) return;
        GameMethod.Call<Il2CppMenace.Strategy.Vehicle>(entity, x => x.SetArmorDurabilityPct(value));
    }

    /// <summary>
    /// Register console commands for Vehicle SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // vehicle - Show vehicle info for selected actor
        DevConsole.RegisterCommand("vehicle", "", "Show vehicle info for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            if (!IsVehicle(actor))
                return "Selected actor is not a vehicle";

            var info = GetVehicleInfo(actor);
            if (info == null)
                return "Could not get vehicle info";

            var lines = new List<string>
            {
                $"Vehicle: {info.TemplateId}",
                $"HP: {info.BaseHp}/{info.MaxHp} ({info.HitpointsPct:P0})",
                $"Armor: {info.Armor} (Durability: {info.ArmorDurabilityPct:P0})",
                $"Equipped Slots: {info.EquippedSlots}",
                $"Twin-Fire: {info.HasTwinFire}"
            };

            if (info.Slots.Count > 0)
            {
                lines.Add("Slots:");
                foreach (var slot in info.Slots)
                {
                    var item = slot.HasItem ? slot.EquippedItemId : "(empty)";
                    lines.Add($"  [{slot.SlotType}] {item}");
                }
            }

            return string.Join("\n", lines);
        });

        // twinfire - Check twin-fire status
        DevConsole.RegisterCommand("twinfire", "", "Check twin-fire status for selected vehicle", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            if (!IsVehicle(actor))
                return "Selected actor is not a vehicle";

            var info = GetVehicleInfo(actor);
            if (info == null)
                return "No modular vehicle data";

            return $"Twin-Fire Active: {info.HasTwinFire}\n" +
                   $"Equipped Slots: {info.EquippedSlots}";
        });
    }
}
