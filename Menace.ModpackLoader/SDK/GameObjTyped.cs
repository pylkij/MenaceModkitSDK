using System;
using System.Linq.Expressions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

using Menace.SDK.Internal;

namespace Menace.SDK;

public readonly struct GameObj<T> where T : Il2CppObjectBase
{
    public GameObj Untyped { get; }

    // Lazy to avoid TypeInitializationException if T is accessed before IL2CPP registration.
    private static readonly Lazy<Func<IntPtr, T>> _ctor = new(BuildConstructor);

    private static Func<IntPtr, T> BuildConstructor()
    {
        var ctor = typeof(T).GetConstructor(new[] { typeof(IntPtr) })
            ?? throw new GameObjException(
                $"GameObj<{typeof(T).Name}>: no IntPtr constructor found. " +
                $"Is {typeof(T).Name} a valid Il2Cpp type?");

        var param = Expression.Parameter(typeof(IntPtr));
        return Expression.Lambda<Func<IntPtr, T>>(
            Expression.New(ctor, param), param
        ).Compile();
    }

    private GameObj(GameObj untyped)
    {
        Untyped = untyped;
    }

    // --- Factories ---

    // TODO: add type validation to Wrap and TryWrap via il2cpp_class_is_assignable_from.
    // Until this is implemented, Wrap is a trust-the-caller assertion, not a verified cast.

    public static GameObj<T> Wrap(GameObj raw)
    {
        if (raw.IsNull)
            throw new GameObjException(
                $"GameObj<{typeof(T).Name}>.Wrap: raw pointer is null");
        return new GameObj<T>(raw);
    }

    public static GameObj<T> Wrap(IntPtr ptr) => Wrap(GameObj.FromPointer(ptr));

    public static bool TryWrap(GameObj raw, out GameObj<T> result)
    {
        result = default;
        if (raw.IsNull) return false;
        result = new GameObj<T>(raw);
        return true;
    }

    // --- Managed proxy ---

    public T AsManaged() => _ctor.Value(Untyped.Pointer);

    // --- Field resolution (call only after IL2CPP class registration) ---

    public static FieldHandle<T, TVal> ResolveField<TVal>(
        Expression<Func<T, TVal>> selector) where TVal : unmanaged
    {
        var fieldName = ExtractFieldName(selector);
        var offset = ResolveOffset(fieldName);
        return new FieldHandle<T, TVal>(offset, fieldName);
    }

    public static ObjFieldHandle<T, TObj> ResolveObjField<TObj>(
        Expression<Func<T, TObj>> selector) where TObj : Il2CppObjectBase
    {
        var fieldName = ExtractFieldName(selector);
        var offset = ResolveOffset(fieldName);
        return new ObjFieldHandle<T, TObj>(offset, fieldName);
    }

    public static StringFieldHandle<T> ResolveStringField(
        Expression<Func<T, string>> selector)
    {
        var fieldName = ExtractFieldName(selector);
        var offset = ResolveOffset(fieldName);
        return new StringFieldHandle<T>(offset, fieldName);
    }

    // Escape hatch for offsets resolved outside the expression system.
    public static FieldHandle<T, TVal> FieldAt<TVal>(uint offset, string name = "unknown")
        where TVal : unmanaged
        => new FieldHandle<T, TVal>(offset, name);

    // --- Equality ---

    public bool Equals(GameObj<T> other) => Untyped == other.Untyped;
    public override bool Equals(object obj) => obj is GameObj<T> other && Equals(other);
    public override int GetHashCode() => Untyped.GetHashCode();
    public static bool operator ==(GameObj<T> a, GameObj<T> b) => a.Untyped == b.Untyped;
    public static bool operator !=(GameObj<T> a, GameObj<T> b) => a.Untyped != b.Untyped;

    // --- Internal helpers ---

    private static string ExtractFieldName<TVal>(Expression<Func<T, TVal>> selector)
    {
        if (selector.Body is MemberExpression member)
            return member.Member.Name;
        throw new GameObjException(
            $"ResolveField: selector must be a direct member access (x => x.FieldName). " +
            $"Chains, method calls, and computed expressions are not supported.");
    }

    private static uint ResolveOffset(string fieldName)
    {
        var klass = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (klass == IntPtr.Zero)
            throw new GameObjException(
                $"GameObj<{typeof(T).Name}>: native class pointer is zero. " +
                $"ResolveField was called before IL2CPP class registration is complete.");

        var offset = OffsetCache.GetOrResolve(klass, fieldName);
        if (offset == 0)
            throw new GameObjException(
                $"GameObj<{typeof(T).Name}>.ResolveField: " +
                $"field '{fieldName}' not found or has zero offset.");

        return offset;
    }
}