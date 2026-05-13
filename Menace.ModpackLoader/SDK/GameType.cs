using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// Wrapper around an IL2CPP class pointer providing safe type system access.
/// Caches lookups to avoid repeated il2cpp_* FFI calls.
/// </summary>
public partial class GameType
{
    // IL2CPP assembly names - use .dll extension (this is what IL2CPP expects)
    private static readonly string[] FallbackAssemblies =
    {
        "Assembly-CSharp.dll",
        "UnityEngine.CoreModule.dll",
        "UnityEngine.UIModule.dll",
        "UnityEngine.UI.dll",
        "Unity.TextMeshPro.dll",
        "mscorlib.dll",
    };

    private static readonly Dictionary<string, GameType> _nameCache = new();
    private static readonly Dictionary<IntPtr, GameType> _ptrCache = new();

    public IntPtr ClassPointer { get; }
    public string FullName { get; }
    public bool IsValid => ClassPointer != IntPtr.Zero;

    private Type _managedType;
    private bool _managedTypeResolved;

    private GameType(IntPtr classPointer, string fullName)
    {
        ClassPointer = classPointer;
        FullName = fullName ?? "";
    }

    public static GameType Of<T>() where T : Il2CppObjectBase
    {
        var il2cppType = Il2CppType.From(typeof(T));
        var ptr = IL2CPP.il2cpp_class_from_type(il2cppType.Pointer);
        return ptr != IntPtr.Zero ? FromPointer(ptr) : null;
    }

    /// <summary>
    /// Get the IL2CppInterop managed proxy Type, if available. May be null.
    /// Only returns types that inherit from Il2CppObjectBase (actual IL2CPP proxies).
    /// </summary>
    internal Type ManagedType
    {
        get
        {
            if (_managedTypeResolved) return _managedType;
            _managedTypeResolved = true;

            if (!IsValid) return null;

            try
            {
                var lastDot = FullName.LastIndexOf('.');
                var ns = lastDot > 0 ? FullName[..lastDot] : "";
                var typeName = lastDot > 0 ? FullName[(lastDot + 1)..] : FullName;

                // IL2CppInterop prefixes namespaces with "Il2Cpp"
                var il2cppFullName = string.IsNullOrEmpty(ns)
                    ? typeName
                    : $"Il2Cpp{ns}.{typeName}";

                // Search all loaded assemblies for the type
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // First try exact full name match with Il2Cpp prefix
                        var candidate = asm.GetType(il2cppFullName);
                        if (IsValidIl2CppProxy(candidate))
                        {
                            _managedType = candidate;
                            return _managedType;
                        }

                        // Try original full name (some types aren't prefixed but are still proxies)
                        candidate = asm.GetType(FullName);
                        if (IsValidIl2CppProxy(candidate))
                        {
                            _managedType = candidate;
                            return _managedType;
                        }
                    }
                    catch
                    {
                        // Skip assemblies that throw on GetType
                    }
                }

                // Fallback: search by type name only in Assembly-CSharp
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (gameAssembly != null)
                {
                    _managedType = gameAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name == typeName && IsValidIl2CppProxy(t));
                }
            }
            catch (Exception ex)
            {
                ModError.ReportInternal("GameType.ManagedType", $"Failed for {FullName}", ex);
            }

            return _managedType;
        }
    }

    /// SDK INTERNALS

    /// <summary>
    /// Create a GameType from an existing IL2CPP class pointer.
    /// </summary>
    internal static GameType FromPointer(IntPtr classPointer)
    {
        if (classPointer == IntPtr.Zero)
            return null;

        if (_ptrCache.TryGetValue(classPointer, out var cached))
            return cached;

        string name;
        try
        {
            var nsPtr = IL2CPP.il2cpp_class_get_namespace(classPointer);
            var nPtr = IL2CPP.il2cpp_class_get_name(classPointer);
            var ns = nsPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(nsPtr) : "";
            var n = nPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(nPtr) : "?";
            name = string.IsNullOrEmpty(ns) ? n : $"{ns}.{n}";
        }
        catch
        {
            name = $"<unknown@0x{classPointer:X}>";
        }

        var gt = new GameType(classPointer, name);
        _ptrCache[classPointer] = gt;
        return gt;
    }

    private static bool IsValidIl2CppProxy(Type type)
    {
        if (type == null)
            return false;

        if (type.IsEnum)
            return true;

        return typeof(Il2CppObjectBase).IsAssignableFrom(type);
    }
}