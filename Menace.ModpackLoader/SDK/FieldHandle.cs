using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// A typed handle for reading and writing an unmanaged field on a native Unity object at a fixed memory offset.
/// Obtain instances via the SDK's field-handle factory rather than constructing directly.
/// <see cref="Read"/> and <see cref="Write"/> throw <see cref="GameObjException"/> if the object is not alive or the offset is zero;
/// use <see cref="TryRead"/> for a non-throwing read path.
/// </summary>
public readonly struct FieldHandle<T, TVal>
    where T : Il2CppObjectBase
    where TVal : unmanaged
{
    private readonly uint _offset;
    private readonly string _fieldName;

    internal FieldHandle(uint offset, string fieldName)
    {
        _offset = offset;
        _fieldName = fieldName;
    }

    public TVal Read(GameObj<T> obj)
    {
        var status = obj.Untyped.CheckAlive();
        if (status != AliveStatus.Alive)
            throw new GameObjException(
                $"FieldHandle.Read '{_fieldName}': object status is {status}");
        if (_offset == 0)
            throw new GameObjException(
                $"FieldHandle.Read '{_fieldName}': offset is zero");

        return MarshalRead(obj.Untyped.Pointer + (int)_offset);
    }

    public bool TryRead(GameObj<T> obj, out TVal result)
    {
        result = default;
        if (obj.Untyped.CheckAlive() != AliveStatus.Alive || _offset == 0)
            return false;

        result = MarshalRead(obj.Untyped.Pointer + (int)_offset);
        return true;
    }

    public void Write(GameObj<T> obj, TVal value)
    {
        var status = obj.Untyped.CheckAlive();
        if (status != AliveStatus.Alive)
            throw new GameObjException(
                $"FieldHandle.Write '{_fieldName}': object status is {status}");
        if (_offset == 0)
            throw new GameObjException(
                $"FieldHandle.Write '{_fieldName}': offset is zero");

        MarshalWrite(obj.Untyped.Pointer + (int)_offset, value);
    }

    private static unsafe TVal MarshalRead(IntPtr address)
        => *(TVal*)address.ToPointer();

    private static unsafe void MarshalWrite(IntPtr address, TVal value)
        => *(TVal*)address.ToPointer() = value;
}

/// <summary>
/// A typed handle for reading and writing a reference-type field (a nested <see cref="Il2CppObjectBase"/> pointer)
/// on a native Unity object at a fixed memory offset. Analogous to <see cref="FieldHandle{T,TVal}"/> but for object references rather than unmanaged values.
/// <see cref="Read"/> throws <see cref="GameObjException"/> if the object is not alive, the offset is zero, or the field pointer is null;
/// use <see cref="TryRead"/> if the field may legitimately be unset.
/// </summary>
public readonly struct ObjFieldHandle<T, TObj>
    where T : Il2CppObjectBase
    where TObj : Il2CppObjectBase
{
    private readonly uint _offset;
    private readonly string _fieldName;

    internal ObjFieldHandle(uint offset, string fieldName)
    {
        _offset = offset;
        _fieldName = fieldName;
    }

    public GameObj<TObj> Read(GameObj<T> obj)
    {
        var status = obj.Untyped.CheckAlive();
        if (status != AliveStatus.Alive)
            throw new GameObjException(
                $"ObjFieldHandle.Read '{_fieldName}': object status is {status}");
        if (_offset == 0)
            throw new GameObjException(
                $"ObjFieldHandle.Read '{_fieldName}': offset is zero");

        var ptr = Marshal.ReadIntPtr(obj.Untyped.Pointer + (int)_offset);
        if (ptr == IntPtr.Zero)
            throw new GameObjException(
                $"ObjFieldHandle.Read '{_fieldName}': field pointer is null. " +
                $"Use TryRead if this field may be unset.");

        return GameObj<TObj>.Wrap(GameObj.FromPointer(ptr));
    }

    public bool TryRead(GameObj<T> obj, out GameObj<TObj> result)
    {
        result = default;
        if (obj.Untyped.CheckAlive() != AliveStatus.Alive || _offset == 0)
            return false;

        var ptr = Marshal.ReadIntPtr(obj.Untyped.Pointer + (int)_offset);
        if (ptr == IntPtr.Zero) return false;

        return GameObj<TObj>.TryWrap(GameObj.FromPointer(ptr), out result);
    }

    public void Write(GameObj<T> obj, GameObj<TObj> value)
    {
        var status = obj.Untyped.CheckAlive();
        if (status != AliveStatus.Alive)
            throw new GameObjException(
                $"ObjFieldHandle.Write '{_fieldName}': object status is {status}");
        if (_offset == 0)
            throw new GameObjException(
                $"ObjFieldHandle.Write '{_fieldName}': offset is zero");

        Marshal.WriteIntPtr(obj.Untyped.Pointer + (int)_offset, value.Untyped.Pointer);
    }
}

/// <summary>
/// A typed handle for reading an IL2CPP string field on a native Unity object at a fixed memory offset.
/// Similar to <see cref="ObjFieldHandle{T,TObj}"/> but converts the native IL2CPP string pointer to a managed
/// <see cref="string"/> via <c>IL2CPP.Il2CppStringToManaged</c>. Write is not supported.
/// <see cref="Read"/> throws <see cref="GameObjException"/> if the object is not alive, the offset is zero, or the field pointer is null;
/// use <see cref="TryRead"/> if the field may legitimately be unset.
/// </summary>
public readonly struct StringFieldHandle<T>
    where T : Il2CppObjectBase
{
    private readonly uint _offset;
    private readonly string _fieldName;

    internal StringFieldHandle(uint offset, string fieldName)
    {
        _offset = offset;
        _fieldName = fieldName;
    }

    public string Read(GameObj<T> obj)
    {
        var status = obj.Untyped.CheckAlive();
        if (status != AliveStatus.Alive)
            throw new GameObjException(
                $"StringFieldHandle.Read '{_fieldName}': object status is {status}");
        if (_offset == 0)
            throw new GameObjException(
                $"StringFieldHandle.Read '{_fieldName}': offset is zero");

        var ptr = Marshal.ReadIntPtr(obj.Untyped.Pointer + (int)_offset);
        if (ptr == IntPtr.Zero)
            throw new GameObjException(
                $"StringFieldHandle.Read '{_fieldName}': field pointer is null. " +
                $"Use TryRead if this field may be unset.");

        return IL2CPP.Il2CppStringToManaged(ptr);
    }

    public bool TryRead(GameObj<T> obj, out string result)
    {
        result = null;
        if (obj.Untyped.CheckAlive() != AliveStatus.Alive || _offset == 0)
            return false;

        var ptr = Marshal.ReadIntPtr(obj.Untyped.Pointer + (int)_offset);
        if (ptr == IntPtr.Zero) return false;

        result = IL2CPP.Il2CppStringToManaged(ptr);
        return result != null;
    }
}