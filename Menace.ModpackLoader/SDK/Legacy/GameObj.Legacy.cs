using Il2CppInterop.Runtime;
using Menace.SDK.Internal;
using System;
using System.Runtime.InteropServices;

namespace Menace.SDK;

public readonly partial struct GameObj
{
    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public int ReadInt(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        return offset == 0 ? 0 : ReadInt(offset);
    }

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public float ReadFloat(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        return offset == 0 ? 0f : ReadFloat(offset);
    }

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public bool ReadBool(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
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

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public IntPtr ReadPtr(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        return offset == 0 ? IntPtr.Zero : ReadPtr(offset);
    }

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public string ReadString(string fieldName)
    {
        var ptr = ReadPtr(fieldName);
        if (ptr == IntPtr.Zero) return null;

        try
        {
            return IL2CPP.Il2CppStringToManaged(ptr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ReadString", $"Failed to read '{fieldName}'", ex);
            return null;
        }
    }

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public GameObj ReadObj(string fieldName)
    {
        var ptr = ReadPtr(fieldName);
        return new GameObj(ptr);
    }

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public bool WriteInt(string fieldName, int value)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
        try
        {
            Marshal.WriteInt32(Pointer + (int)offset, value);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.WriteInt", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public bool WriteFloat(string fieldName, float value)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
        try
        {
            var intVal = BitConverter.SingleToInt32Bits(value);
            Marshal.WriteInt32(Pointer + (int)offset, intVal);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.WriteFloat", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    [Obsolete("Resolves offset at runtime per-call. Cache the offset and use the uint overload instead.", false)]
    public bool WritePtr(string fieldName, IntPtr value)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
        try
        {
            Marshal.WriteIntPtr(Pointer + (int)offset, value);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.WritePtr", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Convert this GameObj to its managed IL2CPP proxy type.
    /// Returns null if conversion fails.
    /// </summary>
    [Obsolete("Returns object, bypassing type safety. Use GameObj<T>.Wrap(this).AsManaged() instead.", false)]
    public object ToManaged()
    {
        if (Pointer == IntPtr.Zero) return null;

        try
        {
            var gameType = GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("GameObj.ToManaged", $"No managed type for {gameType?.FullName}");
                return null;
            }

            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null)
            {
                ModError.WarnInternal("GameObj.ToManaged", $"No IntPtr constructor on {managedType.Name}");
                return null;
            }

            return ptrCtor.Invoke(new object[] { Pointer });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameObj.ToManaged", "Conversion failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if the underlying Unity object is still alive (m_CachedPtr != 0).
    /// </summary>
    [Obsolete("Use CheckAlive() instead, which distinguishes Dead from Unknown.", false)]
    public bool IsAlive
    {
        get
        {
            if (Pointer == IntPtr.Zero) return false;

            try
            {
                var offset = OffsetCache.ObjectCachedPtrOffset;
                if (offset == 0) return true; // can't verify, assume alive

                var cachedPtr = Marshal.ReadIntPtr(Pointer + (int)offset);
                return cachedPtr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }
    }
}