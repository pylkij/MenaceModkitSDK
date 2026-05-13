using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;

namespace Menace.SDK;

public partial class GameType
{
    /// INTERNAL LEGACY - HERE BE DRAGONS
    internal static GameType Find(string fullTypeName, string assembly = "Assembly-CSharp")
    {
        if (string.IsNullOrEmpty(fullTypeName))
            return null;

        assembly = string.IsNullOrWhiteSpace(assembly) ? "Assembly-CSharp" : assembly;
        var cacheKey = $"{assembly}:{fullTypeName}";
        if (_nameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Split namespace and type name
        var lastDot = fullTypeName.LastIndexOf('.');
        var ns = lastDot > 0 ? fullTypeName[..lastDot] : "";
        var typeName = lastDot > 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;

        var ptr = IntPtr.Zero;
        foreach (var probeAssembly in BuildProbeAssemblies(assembly))
        {
            ptr = TryResolveClass(probeAssembly, ns, typeName);
            if (ptr != IntPtr.Zero)
                break;
        }

        // Fallback: if no namespace was provided and we didn't find it,
        // search managed types by short name to discover the full namespace
        if (ptr == IntPtr.Zero && string.IsNullOrEmpty(ns))
        {
            ptr = TryResolveByShortName(typeName);
        }

        var result = ptr != IntPtr.Zero ? FromPointer(ptr) : null;
        if (result.IsValid)
            result = new GameType(ptr, fullTypeName); // ensure we store the requested name

        // Only cache valid results - invalid lookups may succeed later
        // (e.g., short name lookups before templates are loaded)
        if (result.IsValid)
        {
            _nameCache[cacheKey] = result;
            _ptrCache[ptr] = result;
        }

        return result;
    }

    private static IntPtr TryResolveByShortName(string shortName)
    {
        try
        {
            // Search Assembly-CSharp managed proxy for the type
            var gameAssembly = GameState.GameAssembly;
            if (gameAssembly == null)
                return IntPtr.Zero;

            // Don't filter by IsAbstract - template base classes like WeaponTemplate are abstract
            // but we still need to resolve them for FindObjectsOfTypeAll queries
            var managedType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == shortName);

            if (managedType == null)
                return IntPtr.Zero;

            // Extract namespace from the managed type and resolve via IL2CPP
            // The managed proxy has "Il2Cpp" prefix we need to strip for IL2CPP lookup
            var fullName = managedType.FullName ?? "";
            var originalFullName = fullName;
            if (fullName.StartsWith("Il2Cpp"))
                fullName = fullName.Substring(6);

            var dotIdx = fullName.LastIndexOf('.');
            var realNs = dotIdx > 0 ? fullName[..dotIdx] : "";
            var realName = dotIdx > 0 ? fullName[(dotIdx + 1)..] : fullName;

            // Try both with and without .dll extension
            var ptr = TryResolveClass("Assembly-CSharp.dll", realNs, realName);
            if (ptr == IntPtr.Zero)
                ptr = TryResolveClass("Assembly-CSharp", realNs, realName);
            return ptr;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static IntPtr TryResolveClass(string assembly, string ns, string typeName)
    {
        try
        {
            return IL2CPP.GetIl2CppClass(assembly, ns, typeName);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static List<string> BuildProbeAssemblies(string assembly)
    {
        var probes = new List<string>(8);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // IL2CPP expects .dll extension - try that first to avoid warnings
        if (!assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            AddProbe(probes, seen, assembly + ".dll");

        AddProbe(probes, seen, assembly);

        foreach (var fallback in FallbackAssemblies)
            AddProbe(probes, seen, fallback);

        return probes;
    }

    private static void AddProbe(List<string> probes, HashSet<string> seen, string assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly))
            return;

        if (seen.Add(assembly))
            probes.Add(assembly);
    }
}