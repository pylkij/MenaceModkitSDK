#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// SDK for querying loaded modpacks.
/// Provides a clean public API that uses reflection internally to access ModpackLoaderMod state.
/// </summary>
public static class Modpacks
{
    /// <summary>
    /// Information about a loaded modpack.
    /// </summary>
    public class ModpackInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public int LoadOrder { get; set; }
        public int ManifestVersion { get; set; }
        public string DirectoryPath { get; set; }
        public string SecurityStatus { get; set; }
        public int PatchCount { get; set; }
        public int CloneCount { get; set; }
        public int AssetCount { get; set; }
    }

    /// <summary>
    /// Get all loaded modpacks.
    /// </summary>
    public static List<ModpackInfo> GetAllModpacks()
    {
        try
        {
            var loaderMod = GetModpackLoaderInstance();
            if (loaderMod == null)
                return new List<ModpackInfo>();

            var modpacksField = loaderMod.GetType()
                .GetField("_loadedModpacks", BindingFlags.NonPublic | BindingFlags.Instance);

            if (modpacksField == null)
                return new List<ModpackInfo>();

            var modpacks = modpacksField.GetValue(loaderMod);
            if (modpacks == null)
                return new List<ModpackInfo>();

            // modpacks is Dictionary<string, Modpack>
            var dictType = modpacks.GetType();
            var valuesProperty = dictType.GetProperty("Values");
            var values = valuesProperty?.GetValue(modpacks);

            if (values == null)
                return new List<ModpackInfo>();

            var result = new List<ModpackInfo>();

            // Iterate over the collection
            var enumerable = values as System.Collections.IEnumerable;
            if (enumerable == null)
                return result;

            foreach (var modpack in enumerable)
            {
                if (modpack == null)
                    continue;

                var info = ConvertToModpackInfo(modpack);
                if (info != null)
                    result.Add(info);
            }

            return result.OrderBy(m => m.LoadOrder).ToList();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Modpacks.GetAllModpacks", "Failed to query modpacks", ex);
            return new List<ModpackInfo>();
        }
    }

    /// <summary>
    /// Get information about a specific modpack by name.
    /// </summary>
    public static ModpackInfo GetModpack(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        try
        {
            var loaderMod = GetModpackLoaderInstance();
            if (loaderMod == null)
                return null;

            var modpacksField = loaderMod.GetType()
                .GetField("_loadedModpacks", BindingFlags.NonPublic | BindingFlags.Instance);

            if (modpacksField == null)
                return null;

            var modpacks = modpacksField.GetValue(loaderMod);
            if (modpacks == null)
                return null;

            // modpacks is Dictionary<string, Modpack>
            var dictType = modpacks.GetType();
            var tryGetValueMethod = dictType.GetMethod("TryGetValue");

            var parameters = new object[] { name, null };
            var found = (bool)tryGetValueMethod.Invoke(modpacks, parameters);

            if (!found)
                return null;

            var modpack = parameters[1];
            return ConvertToModpackInfo(modpack);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Modpacks.GetModpack", $"Failed to get modpack '{name}'", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the number of loaded modpacks.
    /// </summary>
    public static int GetModpackCount()
    {
        try
        {
            var loaderMod = GetModpackLoaderInstance();
            if (loaderMod == null)
                return 0;

            var modpacksField = loaderMod.GetType()
                .GetField("_loadedModpacks", BindingFlags.NonPublic | BindingFlags.Instance);

            if (modpacksField == null)
                return 0;

            var modpacks = modpacksField.GetValue(loaderMod);
            if (modpacks == null)
                return 0;

            var countProperty = modpacks.GetType().GetProperty("Count");
            if (countProperty == null)
                return 0;

            return (int)countProperty.GetValue(modpacks);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Modpacks.GetModpackCount", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Check if a specific modpack is loaded.
    /// </summary>
    public static bool IsModpackLoaded(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        try
        {
            var loaderMod = GetModpackLoaderInstance();
            if (loaderMod == null)
                return false;

            var modpacksField = loaderMod.GetType()
                .GetField("_loadedModpacks", BindingFlags.NonPublic | BindingFlags.Instance);

            if (modpacksField == null)
                return false;

            var modpacks = modpacksField.GetValue(loaderMod);
            if (modpacks == null)
                return false;

            var containsKeyMethod = modpacks.GetType().GetMethod("ContainsKey");
            return (bool)containsKeyMethod.Invoke(modpacks, new object[] { name });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Modpacks.IsModpackLoaded", $"Failed to check '{name}'", ex);
            return false;
        }
    }

    private static object GetModpackLoaderInstance()
    {
        try
        {
            // Find the ModpackLoaderMod type
            var loaderType = Type.GetType("Menace.ModpackLoader.ModpackLoaderMod, Menace.ModpackLoader");
            if (loaderType == null)
            {
                // Try searching all assemblies
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    loaderType = asm.GetTypes()
                        .FirstOrDefault(t => t.Name == "ModpackLoaderMod" && t.Namespace == "Menace.ModpackLoader");
                    if (loaderType != null)
                        break;
                }
            }

            if (loaderType == null)
                return null;

            // Get the MelonMod instance via MelonBase.RegisteredMelons
            var melonBaseType = Type.GetType("MelonLoader.MelonBase, MelonLoader");
            if (melonBaseType == null)
                return null;

            var registeredMelonsField = melonBaseType.GetField("RegisteredMelons",
                BindingFlags.Public | BindingFlags.Static);

            if (registeredMelonsField == null)
                return null;

            var registeredMelons = registeredMelonsField.GetValue(null);
            if (registeredMelons == null)
                return null;

            // RegisteredMelons is a List<MelonBase>
            var enumerable = registeredMelons as System.Collections.IEnumerable;
            if (enumerable == null)
                return null;

            foreach (var melon in enumerable)
            {
                if (melon != null && melon.GetType() == loaderType)
                    return melon;
            }

            return null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Modpacks.GetModpackLoaderInstance", "Failed", ex);
            return null;
        }
    }

    private static ModpackInfo ConvertToModpackInfo(object modpack)
    {
        if (modpack == null)
            return null;

        try
        {
            var type = modpack.GetType();

            var info = new ModpackInfo();

            // Read properties using reflection
            info.Name = type.GetProperty("Name")?.GetValue(modpack) as string;
            info.Version = type.GetProperty("Version")?.GetValue(modpack) as string;
            info.Author = type.GetProperty("Author")?.GetValue(modpack) as string;
            info.LoadOrder = (int?)type.GetProperty("LoadOrder")?.GetValue(modpack) ?? 0;
            info.ManifestVersion = (int?)type.GetProperty("ManifestVersion")?.GetValue(modpack) ?? 1;
            info.DirectoryPath = type.GetProperty("DirectoryPath")?.GetValue(modpack) as string;
            info.SecurityStatus = type.GetProperty("SecurityStatus")?.GetValue(modpack) as string;

            // Count patches/clones/assets
            var patchesDict = type.GetProperty("Patches")?.GetValue(modpack);
            if (patchesDict != null)
            {
                var countProp = patchesDict.GetType().GetProperty("Count");
                info.PatchCount = (int?)countProp?.GetValue(patchesDict) ?? 0;
            }

            var templatesDict = type.GetProperty("Templates")?.GetValue(modpack);
            if (templatesDict != null && info.PatchCount == 0)
            {
                var countProp = templatesDict.GetType().GetProperty("Count");
                info.PatchCount = (int?)countProp?.GetValue(templatesDict) ?? 0;
            }

            var clonesDict = type.GetProperty("Clones")?.GetValue(modpack);
            if (clonesDict != null)
            {
                var countProp = clonesDict.GetType().GetProperty("Count");
                info.CloneCount = (int?)countProp?.GetValue(clonesDict) ?? 0;
            }

            var assetsDict = type.GetProperty("Assets")?.GetValue(modpack);
            if (assetsDict != null)
            {
                var countProp = assetsDict.GetType().GetProperty("Count");
                info.AssetCount = (int?)countProp?.GetValue(assetsDict) ?? 0;
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Modpacks.ConvertToModpackInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Register console commands for modpack inspection.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("modpacks.list", "", "List all loaded modpacks", _ =>
        {
            var modpacks = GetAllModpacks();
            if (modpacks.Count == 0)
                return "No modpacks loaded";

            var lines = new List<string>();
            lines.Add($"Loaded Modpacks ({modpacks.Count}):");
            lines.Add("");

            foreach (var mp in modpacks)
            {
                lines.Add($"  {mp.Name} v{mp.Version}");
                lines.Add($"    Author: {mp.Author ?? "Unknown"}");
                lines.Add($"    Load Order: {mp.LoadOrder}");
                lines.Add($"    Manifest: V{mp.ManifestVersion}");
                lines.Add($"    Patches: {mp.PatchCount}, Clones: {mp.CloneCount}, Assets: {mp.AssetCount}");
                lines.Add("");
            }

            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("modpacks.info", "<name>", "Get detailed info about a modpack", args =>
        {
            if (args.Length == 0)
                return "Usage: modpacks.info <name>";

            var name = string.Join(" ", args);
            var mp = GetModpack(name);

            if (mp == null)
                return $"Modpack '{name}' not found";

            var lines = new List<string>();
            lines.Add($"=== {mp.Name} ===");
            lines.Add($"Version: {mp.Version}");
            lines.Add($"Author: {mp.Author ?? "Unknown"}");
            lines.Add($"Load Order: {mp.LoadOrder}");
            lines.Add($"Manifest Version: {mp.ManifestVersion}");
            lines.Add($"Security Status: {mp.SecurityStatus ?? "Unreviewed"}");
            lines.Add($"Directory: {mp.DirectoryPath ?? "Unknown"}");
            lines.Add("");
            lines.Add($"Content:");
            lines.Add($"  Patches: {mp.PatchCount}");
            lines.Add($"  Clones: {mp.CloneCount}");
            lines.Add($"  Assets: {mp.AssetCount}");

            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("modpacks.count", "", "Get count of loaded modpacks", _ =>
        {
            return $"{GetModpackCount()} modpack(s) loaded";
        });
    }
}
