using Il2CppInterop.Runtime;
using Menace.SDK.Internal;
using System;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// Safe handle for an IL2CPP object. All reads return defaults on failure;
/// all writes return false on failure. Never throws.
/// </summary>
public readonly partial struct GameObj : IEquatable<GameObj>
{
    public IntPtr Pointer { get; }

    public bool IsNull => Pointer == IntPtr.Zero;

    /// <summary>
    /// Checks whether the underlying native Unity object is still alive by reading m_CachedPtr from unmanaged memory.
    /// Returns <see cref="AliveStatus.Alive"/> or <see cref="AliveStatus.Dead"/> when the check is conclusive,
    /// or <see cref="AliveStatus.Unknown"/> if the cached pointer offset is unavailable or the memory read fails.
    /// </summary>
    public AliveStatus CheckAlive()
    {
        if (Pointer == IntPtr.Zero) return AliveStatus.Dead;
        var offset = OffsetCache.ObjectCachedPtrOffset;
        if (offset == 0) return AliveStatus.Unknown;
        try
        {
            var cachedPtr = Marshal.ReadIntPtr(Pointer + (int)offset);
            return cachedPtr != IntPtr.Zero ? AliveStatus.Alive : AliveStatus.Dead;
        }
        catch
        {
            return AliveStatus.Unknown;
        }
    }

    internal GameObj(IntPtr pointer)
    {
        Pointer = pointer;
    }

    // Used internally by the SDK.
    internal static GameObj FromPointer(IntPtr pointer) => new GameObj(pointer);

    // Temporary escape hatch for untyped construction during migration.
    // Every call site outside the SDK assembly must be resolved before Phase 4.3.
    // Search for UntypedFromPointer_Migrate to find remaining sites.
    internal static GameObj UntypedFromPointer_Migrate(IntPtr pointer) => new GameObj(pointer);

    public static GameObj Null => default;

    // --- Field reads by pre-cached offset ---

    public int ReadInt(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return 0;
        try
        {
            return Marshal.ReadInt32(Pointer + (int)offset);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ReadInt", $"Failed at offset {offset}", ex);
            return 0;
        }
    }

    public float ReadFloat(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return 0f;
        try
        {
            var raw = Marshal.ReadInt32(Pointer + (int)offset);
            return BitConverter.Int32BitsToSingle(raw);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ReadFloat", $"Failed at offset {offset}", ex);
            return 0f;
        }
    }

    public bool ReadBool(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return false;
        try
        {
            return Marshal.ReadByte(Pointer + (int)offset) != 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ReadBool", $"Failed at offset {offset}", ex);
            return false;
        }
    }

    public string ReadString(uint offset)
    {
        var ptr = ReadPtr(offset);
        if (ptr == IntPtr.Zero) return null;

        try
        {
            return IL2CPP.Il2CppStringToManaged(ptr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ReadString", $"Failed at offset {offset}", ex);
            return null;
        }
    }

    public GameObj ReadObj(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return GameObj.Null;
        try
        {
            var ptr = ReadPtr(offset);
            return ptr != IntPtr.Zero ? new GameObj(ptr) : GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ReadObj", $"Failed at offset {offset}", ex);
            return GameObj.Null;
        }
    }

    public IntPtr ReadPtr(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return IntPtr.Zero;
        try
        {
            return Marshal.ReadIntPtr(Pointer + (int)offset);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ReadPtr", $"Failed at offset {offset}", ex);
            return IntPtr.Zero;
        }
    }

    // --- Field writes by pre-cached offset ---

    public void WriteInt(uint offset, int value)
    {
        if (Pointer == IntPtr.Zero) throw new GameObjException("WriteInt: pointer is null");
        if (offset == 0) throw new GameObjException("WriteInt: offset is zero");
        Marshal.WriteInt32(Pointer + (int)offset, value);
    }

    public void WriteFloat(uint offset, float value)
    {
        if (Pointer == IntPtr.Zero) throw new GameObjException("WriteFloat: pointer is null");
        if (offset == 0) throw new GameObjException("WriteFloat: offset is zero");
        var intVal = BitConverter.SingleToInt32Bits(value);
        Marshal.WriteInt32(Pointer + (int)offset, intVal);
    }

    public void WritePtr(uint offset, IntPtr value)
    {
        if (Pointer == IntPtr.Zero) throw new GameObjException("WritePtr: pointer is null");
        if (offset == 0) throw new GameObjException("WritePtr: offset is zero");
        Marshal.WriteIntPtr(Pointer + (int)offset, value);
    }

    // --- Type operations ---

    public GameType GetGameType()
    {
        if (Pointer == IntPtr.Zero) return null;

        try
        {
            var klass = IL2CPP.il2cpp_object_get_class(Pointer);
            return GameType.FromPointer(klass);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.GetGameType", "Failed", ex);
            return null;
        }
    }

    public string GetTypeName()
    {
        var gameType = GetGameType();
        return gameType?.FullName ?? "<unknown>";
    }

    /// <summary>
    /// Convert this GameObj to a specific managed IL2CPP proxy type.
    /// Returns null if conversion fails or type doesn't match.
    /// </summary>
    public T As<T>() where T : class
    {
        if (Pointer == IntPtr.Zero) return null;

        try
        {
            var ptrCtor = typeof(T).GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null)
            {
                ModError.WarnInternal("GameObj.As<T>", $"No IntPtr constructor on {typeof(T).Name}");
                return null;
            }

            return (T)ptrCtor.Invoke(new object[] { Pointer });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.As<T>", $"Conversion to {typeof(T).Name} failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the Unity object name (reads the "name" IL2CPP string field
    /// on UnityEngine.Object-derived objects).
    /// </summary>
    public string GetName()
    {
        if (Pointer == IntPtr.Zero) return null;

        try
        {
            var klass = IL2CPP.il2cpp_object_get_class(Pointer);
            if (klass == IntPtr.Zero) return null;

            // Try m_Name field first (some Unity objects)
            var nameField = OffsetCache.FindField(klass, "m_Name");
            if (nameField != IntPtr.Zero)
            {
                var offset = IL2CPP.il2cpp_field_get_offset(nameField);
                if (offset != 0)
                {
                    var strPtr = Marshal.ReadIntPtr(Pointer + (int)offset);
                    if (strPtr != IntPtr.Zero)
                        return IL2CPP.Il2CppStringToManaged(strPtr);
                }
            }

            // Fallback: use "name" property via managed type (UnityEngine.Object.name)
            var gameType = GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType != null)
            {
                var nameProp = managedType.GetProperty("name",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (nameProp != null)
                {
                    var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
                    if (ptrCtor != null)
                    {
                        var proxy = ptrCtor.Invoke(new object[] { Pointer });
                        var name = nameProp.GetValue(proxy);
                        if (name != null)
                            return name.ToString();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    // --- Equality ---

    public bool Equals(GameObj other) => Pointer == other.Pointer;
    public override bool Equals(object obj) => obj is GameObj other && Equals(other);
    public override int GetHashCode() => Pointer.GetHashCode();
    public static bool operator ==(GameObj left, GameObj right) => left.Pointer == right.Pointer;
    public static bool operator !=(GameObj left, GameObj right) => left.Pointer != right.Pointer;

    public override string ToString()
    {
        if (Pointer == IntPtr.Zero) return "GameObj.Null";
        var name = GetName();
        var typeName = GetTypeName();
        return name != null
            ? $"{typeName} '{name}' @ 0x{Pointer:X}"
            : $"{typeName} @ 0x{Pointer:X}";
    }

    // --- Internal helpers ---

    private uint ResolveFieldOffset(string fieldName)
    {
        if (Pointer == IntPtr.Zero || string.IsNullOrEmpty(fieldName))
            return 0;

        try
        {
            var klass = IL2CPP.il2cpp_object_get_class(Pointer);
            if (klass == IntPtr.Zero) return 0;
            return OffsetCache.GetOrResolve(klass, fieldName);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj", $"Failed to resolve offset for '{fieldName}'", ex);
            return 0;
        }
    }
}

/// <summary>
/// Indicates whether a native game object is considered alive, dead, or in an
/// indeterminate state when the required offset is unavailable.
/// </summary>
public enum AliveStatus
{
    Alive,
    Dead,
    Unknown  // offset unavailable — native validity cannot be confirmed
}