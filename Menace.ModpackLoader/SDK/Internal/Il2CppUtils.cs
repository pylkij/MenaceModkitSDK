using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace Menace.SDK.Internal;

/// <summary>
/// Utility methods for IL2CPP interop.
/// </summary>
internal static class Il2CppUtils
{
    /// <summary>
    /// Safely convert a reflection result to a .NET string.
    /// Handles .NET strings, IL2CPP strings, and Menace.Tools localized string objects
    /// (BaseLocalizedString, LocalizedLine, LocalizedMultiLine).
    /// Use this when calling Invoke() or GetValue() on IL2CPP objects
    /// that might return localized strings.
    /// </summary>
    internal static string ToManagedString(object value)
    {
        if (value == null) return null;
        if (value is string s) return s;

        if (value is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj)
        {
            try
            {
                var ptr = il2cppObj.Pointer;
                if (ptr == IntPtr.Zero) return null;

                var klass = IL2CPP.il2cpp_object_get_class(ptr);
                if (klass != IntPtr.Zero)
                {
                    var classNamePtr = IL2CPP.il2cpp_class_get_name(klass);
                    var className = classNamePtr != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(classNamePtr)
                        : null;

                    // Verified: LocalizedLine and LocalizedMultiLine both inherit
                    // BaseLocalizedString (Menace.Tools) — dump.cs.
                    // m_DefaultTranslation is defined on BaseLocalizedString at 0x38 — dump.cs.
                    // Checking the base class name covers all subclasses.
                    if (className == "BaseLocalizedString"
                        || className == "LocalizedLine"
                        || className == "LocalizedMultiLine")
                    {
                        var offset = OffsetCache.GetOrResolve(klass, "m_DefaultTranslation");
                        if (offset != 0)
                        {
                            var strPtr = Marshal.ReadIntPtr(ptr + (int)offset);
                            if (strPtr != IntPtr.Zero)
                                return IL2CPP.Il2CppStringToManaged(strPtr);
                        }
                        return null;
                    }

                    if (className == "String")
                        return IL2CPP.Il2CppStringToManaged(ptr);
                }

                return IL2CPP.Il2CppStringToManaged(ptr);
            }
            catch
            {
                return value.ToString();
            }
        }

        return value.ToString();
    }

    /// <summary>
    /// Create a managed IL2CPP proxy object from a pointer and managed type.
    /// Returns null if the pointer is invalid or construction fails.
    /// </summary>
    internal static object GetManagedProxy(IntPtr pointer, Type managedType)
    {
        if (pointer == IntPtr.Zero || managedType == null) return null;

        try
        {
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            return ptrCtor?.Invoke(new object[] { pointer });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create a managed IL2CPP proxy object from a GameObj and managed type.
    /// Returns null if the pointer is invalid or construction fails.
    /// </summary>
    internal static object GetManagedProxy(GameObj obj, Type managedType)
    {
        if (obj.IsNull || managedType == null) return null;
        return GetManagedProxy(obj.Pointer, managedType);
    }

    /// <summary>
    /// Extract the IL2CPP pointer from a managed proxy object.
    /// Returns IntPtr.Zero if the object is null or not an IL2CPP proxy.
    /// </summary>
    internal static IntPtr GetPointer(object obj)
    {
        if (obj == null) return IntPtr.Zero;
        if (obj is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj)
            return il2cppObj.Pointer;
        return IntPtr.Zero;
    }
}