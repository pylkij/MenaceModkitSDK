#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Menace.SDK;
using Menace.SDK.Repl;
using UnityEngine;

namespace Menace.ModpackLoader.Diagnostics;

/// <summary>
/// Comprehensive diagnostics for template loading.
/// Patches DataTemplateLoader to log all GetBaseFolder() and LoadTemplates() calls.
/// Provides console command to test ALL template types and save diagnostic report.
/// </summary>
public static class DataTemplateLoaderDiagnostics
{
    private static readonly List<string> _diagnosticLog = new();
    private static bool _patchesApplied = false;

    // Known template types from the game (verified from ExtractedData directory)
    // Full list: 77 types exist. This is a representative subset for diagnostics.
    private static readonly string[] AllTemplateTypes = new[]
    {
        // Items
        "WeaponTemplate",
        "ArmorTemplate",
        "AccessoryTemplate",
        "CommodityTemplate",
        "ItemTemplate",
        "VoucherTemplate",
        "DossierItemTemplate",

        // Characters & Units
        "UnitLeaderTemplate", // Characters/SquadLeaders (was "CharacterTemplate")
        "UnitRankTemplate",

        // Strategy
        "ArmyTemplate",
        "OperationTemplate",
        "FactionTemplate",
        "PlanetTemplate", // Regions (was "RegionTemplate")
        "BiomeTemplate",
        "EmotionalStateTemplate",

        // Tactical
        "EntityTemplate", // Tactical entities/units (was "EncounterTemplate")
        "SkillTemplate",
        "EnvironmentFeatureTemplate",

        // Perks
        "PerkTreeTemplate", // Was "PerkTree"
        "PerkTemplate", // Individual perks (was "PerkNode")

        // Missions
        "GenericMissionTemplate",
        "MissionDifficultyTemplate",

        // Other important types
        "ConversationTemplate",
        "VideoTemplate",
        "TagTemplate"
    };

    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_patchesApplied)
            return;

        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                SdkLogger.Warning("[TemplateDiagnostics] Assembly-CSharp not found");
                return;
            }

            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                SdkLogger.Warning("[TemplateDiagnostics] DataTemplateLoader not found");
                return;
            }

            // Patch GetBaseFolder to log what paths are returned for each type
            var getBaseFolderMethod = loaderType.GetMethod("GetBaseFolder",
                BindingFlags.Public | BindingFlags.Static);

            if (getBaseFolderMethod != null)
            {
                harmony.Patch(getBaseFolderMethod,
                    postfix: new HarmonyMethod(typeof(DataTemplateLoaderDiagnostics),
                        nameof(GetBaseFolderPostfix)));
                SdkLogger.Msg("[TemplateDiagnostics] Patched GetBaseFolder");
            }

            // Patch LoadTemplates to log results
            var loadTemplatesMethod = loaderType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "LoadTemplates" && m.IsGenericMethodDefinition);

            if (loadTemplatesMethod != null)
            {
                harmony.Patch(loadTemplatesMethod,
                    postfix: new HarmonyMethod(typeof(DataTemplateLoaderDiagnostics),
                        nameof(LoadTemplatesPostfix)));
                SdkLogger.Msg("[TemplateDiagnostics] Patched LoadTemplates");
            }

            _patchesApplied = true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TemplateDiagnostics] Failed to apply patches: {ex.Message}");
        }
    }

    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("debug.test_all_templates", "",
            "Test loading all template types and save diagnostic report", _ =>
        {
            return TestAllTemplateTypes();
        });

        DevConsole.RegisterCommand("debug.template_log", "",
            "Show template loading diagnostic log", _ =>
        {
            if (_diagnosticLog.Count == 0)
                return "No diagnostic log entries yet";

            return string.Join("\n", _diagnosticLog.TakeLast(50));
        });

        DevConsole.RegisterCommand("debug.clear_template_log", "",
            "Clear template diagnostic log", _ =>
        {
            _diagnosticLog.Clear();
            return "Diagnostic log cleared";
        });
    }

    private static void GetBaseFolderPostfix(Type _type, ref string __result)
    {
        try
        {
            var typeName = _type?.Name ?? "null";
            var folder = __result ?? "null";
            var logEntry = $"GetBaseFolder({typeName}) -> '{folder}'";
            _diagnosticLog.Add(logEntry);

            // Also log to console for immediate visibility
            SdkLogger.Msg($"[TemplateDiagnostics] {logEntry}");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TemplateDiagnostics] GetBaseFolderPostfix error: {ex.Message}");
        }
    }

    private static void LoadTemplatesPostfix(Type __0, object __result)
    {
        try
        {
            var typeName = __0?.Name ?? "null";

            // The result is IReadOnlyList<T>, try to get count
            int count = 0;
            if (__result != null)
            {
                var countProp = __result.GetType().GetProperty("Count");
                if (countProp != null)
                    count = (int)countProp.GetValue(__result);
            }

            var logEntry = $"LoadTemplates({typeName}) -> {count} templates";
            _diagnosticLog.Add(logEntry);

            SdkLogger.Msg($"[TemplateDiagnostics] {logEntry}");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TemplateDiagnostics] LoadTemplatesPostfix error: {ex.Message}");
        }
    }

    private static string TestAllTemplateTypes()
    {
        var report = new StringBuilder();
        report.AppendLine("=== TEMPLATE LOADING DIAGNOSTIC REPORT ===");
        report.AppendLine($"Test Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Current Scene: {GameState.CurrentScene}");
        report.AppendLine();

        var results = new Dictionary<string, TemplateTestResult>();

        foreach (var typeName in AllTemplateTypes)
        {
            results[typeName] = TestTemplateType(typeName);
        }

        // Summary
        var succeeded = results.Values.Count(r => r.Success);
        var failed = results.Values.Count(r => !r.Success);
        report.AppendLine($"SUMMARY: {succeeded}/{AllTemplateTypes.Length} types loaded successfully ({failed} failed)");
        report.AppendLine();

        // Successful loads
        report.AppendLine("=== SUCCESSFUL LOADS ===");
        foreach (var (typeName, result) in results.Where(r => r.Value.Success).OrderBy(r => r.Key))
        {
            report.AppendLine($"✓ {typeName}");
            report.AppendLine($"    Path: {result.ResourcePath ?? "null"}");
            report.AppendLine($"    Count: {result.Count}");
            if (!string.IsNullOrEmpty(result.SampleNames))
                report.AppendLine($"    Samples: {result.SampleNames}");
        }
        report.AppendLine();

        // Failed loads
        report.AppendLine("=== FAILED LOADS ===");
        foreach (var (typeName, result) in results.Where(r => !r.Value.Success).OrderBy(r => r.Key))
        {
            report.AppendLine($"✗ {typeName}");
            report.AppendLine($"    Path: {result.ResourcePath ?? "null"}");
            report.AppendLine($"    Error: {result.Error ?? "No templates found"}");
        }
        report.AppendLine();

        // Recent diagnostic log
        report.AppendLine("=== RECENT DIAGNOSTIC LOG ===");
        foreach (var entry in _diagnosticLog.TakeLast(100))
        {
            report.AppendLine($"  {entry}");
        }

        // Save to file
        try
        {
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, "template_diagnostic.log");
            File.WriteAllText(logFile, report.ToString());
            report.AppendLine();
            report.AppendLine($"Full report saved to: {logFile}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"Warning: Could not save log file: {ex.Message}");
        }

        return report.ToString();
    }

    private static TemplateTestResult TestTemplateType(string typeName)
    {
        var result = new TemplateTestResult { TypeName = typeName };

        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                result.Error = "Assembly-CSharp not found";
                return result;
            }

            // Find the template type
            var templateType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName && !t.IsAbstract);

            if (templateType == null)
            {
                result.Error = $"Type '{typeName}' not found in Assembly-CSharp";
                return result;
            }

            // Try to get the resource path via GetBaseFolder
            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType != null)
            {
                var getBaseFolderMethod = loaderType.GetMethod("GetBaseFolder",
                    BindingFlags.Public | BindingFlags.Static);

                if (getBaseFolderMethod != null)
                {
                    result.ResourcePath = (string)getBaseFolderMethod.Invoke(null, new object[] { templateType });
                }
            }

            // Try to load templates
            var il2cppType = Il2CppType.From(templateType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects == null || objects.Length == 0)
            {
                result.Error = "No instances found via Resources.FindObjectsOfTypeAll";
                result.Count = 0;
                return result;
            }

            result.Count = objects.Length;

            // Get sample names (first 3)
            var sampleNames = objects
                .Take(3)
                .Where(o => o != null)
                .Select(o => o.name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            if (sampleNames.Count > 0)
                result.SampleNames = string.Join(", ", sampleNames);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Error = $"{ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    private class TemplateTestResult
    {
        public string TypeName { get; set; }
        public bool Success { get; set; }
        public int Count { get; set; }
        public string ResourcePath { get; set; }
        public string SampleNames { get; set; }
        public string Error { get; set; }
    }
}
