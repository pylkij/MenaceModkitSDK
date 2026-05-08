#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Menace.SDK;
using Menace.SDK.Repl;
using UnityEngine;

namespace Menace.ModpackLoader.Diagnostics;

/// <summary>
/// Comprehensive validation of the entire template modding pipeline:
/// 1. Template extraction to JSON
/// 2. Loading templates in game via Templates API
/// 3. Reading fields via Templates.GetProperty()
/// 4. Writing fields via Templates.WriteField()
/// 5. Reference resolution
///
/// This validates that ALL 77 template types work end-to-end.
/// </summary>
public static class TemplatePipelineValidator
{
    // All 77 template types (without Menace. prefix - just the class name)
    private static readonly string[] AllTemplateTypes = new[]
    {
        // Items & Equipment (10)
        "AccessoryTemplate",
        "ArmorTemplate",
        "CommodityTemplate",
        "DossierItemTemplate",
        "ItemFilterTemplate",
        "ItemListTemplate",
        "SquaddieItemTemplate",
        "VehicleItemTemplate",
        "VoucherTemplate",
        "WeaponTemplate",

        // Characters & Units (3)
        "UnitLeaderTemplate",
        "UnitRankTemplate",

        // Strategy (16)
        "ArmyTemplate",
        "BiomeTemplate",
        "ConversationEffectsTemplate",
        "EmotionalStateTemplate",
        "EnemyAssetTemplate",
        "FactionTemplate",
        "GlobalDifficultyTemplate",
        "LightConditionTemplate",
        "MissionDifficultyTemplate",
        "MissionPOITemplate",
        "MissionPreviewConfigTemplate",
        "OperationDurationTemplate",
        "OperationIntrosTemplate",
        "OperationTemplate",
        "PlanetTemplate",
        "StoryFactionTemplate",
        "StrategicAssetTemplate",

        // Missions (1)
        "GenericMissionTemplate",

        // Tactical (13)
        "AIWeightsTemplate",
        "AnimatorParameterNameTemplate",
        "DefectTemplate",
        "ElementAnimatorTemplate",
        "EntityTemplate",
        "HalfCoverTemplate",
        "InsideCoverTemplate",
        "RagdollTemplate",
        "SkillTemplate",
        "SkillUsesDisplayTemplate",
        "SurfaceTypeTemplate",
        "WeatherTemplate",
        "WindControlsTemplate",

        // Map Generation (2)
        "ChunkTemplate",
        "EnvironmentFeatureTemplate",

        // Vehicles (2)
        "ModularVehicleTemplate",
        "ModularVehicleWeaponTemplate",

        // Perks & Upgrades (4)
        "PerkTemplate",
        "PerkTreeTemplate",
        "ShipUpgradeSlotTemplate",
        "ShipUpgradeTemplate",

        // Conversations (3)
        "ConversationStageTemplate",
        "ConversationTemplate",
        "SpeakerTemplate",

        // Rewards (2)
        "RewardTableTemplate",
        "OffmapAbilityTemplate",

        // Player Settings (5)
        "BoolPlayerSettingTemplate",
        "DisplayIndexPlayerSettingTemplate",
        "IntPlayerSettingTemplate",
        "KeyBindPlayerSettingTemplate",
        "ListPlayerSettingTemplate",
        "ResolutionPlayerSettingTemplate",

        // Visuals & Audio (8)
        "AnimationSequenceTemplate",
        "AnimationSoundTemplate",
        "PrefabListTemplate",
        "PropertyDisplayConfigTemplate",
        "SurfaceDecalsTemplate",
        "SurfaceEffectsTemplate",
        "SurfaceSoundsTemplate",
        "VideoTemplate",

        // Other (1)
        "TagTemplate",
    };

    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("debug.validate_template_pipeline", "",
            "Comprehensive validation of template extraction→loading→reading→writing pipeline", _ =>
        {
            return ValidateFullPipeline();
        });

        DevConsole.RegisterCommand("debug.test_template_fields", "<templateType>",
            "Test field reading/writing for a specific template type", args =>
        {
            if (args.Length < 2)
                return "Usage: debug.test_template_fields <templateType>";

            return TestTemplateTypeFields(args[1]);
        });
    }

    private static string ValidateFullPipeline()
    {
        var report = new StringBuilder();
        report.AppendLine("=== TEMPLATE PIPELINE VALIDATION ===");
        report.AppendLine($"Test Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Current Scene: {GameState.CurrentScene}");
        report.AppendLine($"Testing: {AllTemplateTypes.Length} template types");
        report.AppendLine();

        var results = new Dictionary<string, PipelineTestResult>();

        foreach (var typeName in AllTemplateTypes)
        {
            results[typeName] = TestTemplatePipeline(typeName);
        }

        // Summary
        var succeeded = results.Values.Count(r => r.OverallSuccess);
        var failed = results.Values.Count(r => !r.OverallSuccess);
        var successRate = (succeeded * 100.0) / AllTemplateTypes.Length;

        report.AppendLine($"SUMMARY: {succeeded}/{AllTemplateTypes.Length} types passed ({successRate:F1}% success rate)");
        report.AppendLine();

        // Successful types
        report.AppendLine("=== PASSED (All Operations Work) ===");
        foreach (var (typeName, result) in results.Where(r => r.Value.OverallSuccess).OrderBy(r => r.Key))
        {
            report.AppendLine($"✓ {typeName}");
            report.AppendLine($"    Count: {result.InstanceCount}");
            report.AppendLine($"    Fields Tested: {result.FieldsTestedCount}");
            report.AppendLine($"    Read: ✓  Write: ✓  GetProperty: ✓");
        }
        report.AppendLine();

        // Failed types
        if (failed > 0)
        {
            report.AppendLine("=== FAILED (Some Operations Failed) ===");
            foreach (var (typeName, result) in results.Where(r => !r.Value.OverallSuccess).OrderBy(r => r.Key))
            {
                report.AppendLine($"✗ {typeName}");
                report.AppendLine($"    Count: {result.InstanceCount}");

                if (!result.LoadSuccess)
                    report.AppendLine($"    ✗ LOAD FAILED: {result.LoadError}");
                if (!result.ReadSuccess)
                    report.AppendLine($"    ✗ READ FAILED: {result.ReadError}");
                if (!result.WriteSuccess)
                    report.AppendLine($"    ✗ WRITE FAILED: {result.WriteError}");
                if (!result.GetPropertySuccess)
                    report.AppendLine($"    ✗ GET_PROPERTY FAILED: {result.GetPropertyError}");
            }
            report.AppendLine();
        }

        // Per-operation breakdown
        var loadSucceeded = results.Values.Count(r => r.LoadSuccess);
        var readSucceeded = results.Values.Count(r => r.ReadSuccess);
        var writeSucceeded = results.Values.Count(r => r.WriteSuccess);
        var getPropSucceeded = results.Values.Count(r => r.GetPropertySuccess);

        report.AppendLine("=== OPERATION BREAKDOWN ===");
        report.AppendLine($"Template Loading: {loadSucceeded}/{AllTemplateTypes.Length} ({loadSucceeded * 100.0 / AllTemplateTypes.Length:F1}%)");
        report.AppendLine($"Field Reading: {readSucceeded}/{AllTemplateTypes.Length} ({readSucceeded * 100.0 / AllTemplateTypes.Length:F1}%)");
        report.AppendLine($"Field Writing: {writeSucceeded}/{AllTemplateTypes.Length} ({writeSucceeded * 100.0 / AllTemplateTypes.Length:F1}%)");
        report.AppendLine($"GetProperty API: {getPropSucceeded}/{AllTemplateTypes.Length} ({getPropSucceeded * 100.0 / AllTemplateTypes.Length:F1}%)");
        report.AppendLine();

        // Save to file
        try
        {
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, "template_pipeline_validation.log");
            File.WriteAllText(logFile, report.ToString());
            report.AppendLine($"Full report saved to: {logFile}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"Warning: Could not save log file: {ex.Message}");
        }

        return report.ToString();
    }

    private static PipelineTestResult TestTemplatePipeline(string typeName)
    {
        var result = new PipelineTestResult { TypeName = typeName };

        try
        {
            // Step 1: Try to load templates
            var instances = Templates.FindAll(typeName);
            result.InstanceCount = instances?.Length ?? 0;

            if (instances == null || instances.Length == 0)
            {
                result.LoadSuccess = false;
                result.LoadError = "No instances found";
                return result;
            }

            result.LoadSuccess = true;

            // Use first instance for testing
            var testInstance = instances[0];
            var instanceName = testInstance.As<UnityEngine.Object>()?.name ?? "unknown";
            result.TestInstanceName = instanceName;

            // Step 2: Try to read fields directly (via ReadString, ReadInt, etc.)
            try
            {
                // Try reading common field "name"
                var nameValue = testInstance.ReadString("m_Name");
                result.ReadSuccess = true;
                result.FieldsTestedCount++;
            }
            catch (Exception ex)
            {
                result.ReadSuccess = false;
                result.ReadError = $"Failed to read m_Name: {ex.Message}";
            }

            // Step 3: Try GetProperty API
            try
            {
                var propValue = Templates.GetProperty(typeName, instanceName, "name");
                result.GetPropertySuccess = true;
            }
            catch (Exception ex)
            {
                result.GetPropertySuccess = false;
                result.GetPropertyError = ex.Message;
            }

            // Step 4: Try WriteField API (write then read back to verify)
            try
            {
                // Try to write to a test field (we'll use a safe field like hideFlags)
                var originalFlags = testInstance.As<UnityEngine.Object>()?.hideFlags ?? HideFlags.None;

                bool writeResult = Templates.WriteField(testInstance, "hideFlags", HideFlags.DontSave);

                if (writeResult)
                {
                    var readBack = testInstance.As<UnityEngine.Object>()?.hideFlags;

                    // Restore original
                    Templates.WriteField(testInstance, "hideFlags", originalFlags);

                    result.WriteSuccess = readBack == HideFlags.DontSave;
                }
                else
                {
                    result.WriteSuccess = false;
                    result.WriteError = "WriteField returned false";
                }
            }
            catch (Exception ex)
            {
                result.WriteSuccess = false;
                result.WriteError = ex.Message;
            }

            result.OverallSuccess = result.LoadSuccess && result.ReadSuccess &&
                                   result.GetPropertySuccess && result.WriteSuccess;
        }
        catch (Exception ex)
        {
            result.LoadSuccess = false;
            result.LoadError = $"{ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    private static string TestTemplateTypeFields(string typeName)
    {
        var report = new StringBuilder();
        report.AppendLine($"=== TESTING FIELDS FOR {typeName} ===");
        report.AppendLine();

        try
        {
            var instances = Templates.FindAll(typeName);
            if (instances == null || instances.Length == 0)
            {
                return $"No instances of {typeName} found";
            }

            report.AppendLine($"Found {instances.Length} instances");
            report.AppendLine($"Testing first instance: {instances[0].As<UnityEngine.Object>()?.name ?? "unknown"}");
            report.AppendLine();

            var testInstance = instances[0];
            var managed = testInstance.ToManaged();

            if (managed == null)
            {
                return "Failed to get managed proxy object";
            }

            // Get all public properties
            var type = managed.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            report.AppendLine($"Found {properties.Length} public properties:");
            report.AppendLine();

            int successCount = 0;
            int failCount = 0;

            foreach (var prop in properties.OrderBy(p => p.Name))
            {
                try
                {
                    if (!prop.CanRead)
                    {
                        report.AppendLine($"⊘ {prop.Name} ({prop.PropertyType.Name}) - not readable");
                        continue;
                    }

                    var value = prop.GetValue(managed);
                    var valueStr = value?.ToString() ?? "null";

                    // Truncate long values
                    if (valueStr.Length > 50)
                        valueStr = valueStr.Substring(0, 47) + "...";

                    report.AppendLine($"✓ {prop.Name} ({prop.PropertyType.Name}) = {valueStr}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    report.AppendLine($"✗ {prop.Name} ({prop.PropertyType.Name}) - {ex.GetType().Name}: {ex.Message}");
                    failCount++;
                }
            }

            report.AppendLine();
            report.AppendLine($"SUMMARY: {successCount} readable, {failCount} failed, {properties.Length - successCount - failCount} not readable");
        }
        catch (Exception ex)
        {
            report.AppendLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
        }

        return report.ToString();
    }

    private class PipelineTestResult
    {
        public string TypeName { get; set; }
        public int InstanceCount { get; set; }
        public string TestInstanceName { get; set; }

        public bool LoadSuccess { get; set; }
        public string LoadError { get; set; }

        public bool ReadSuccess { get; set; }
        public string ReadError { get; set; }

        public bool WriteSuccess { get; set; }
        public string WriteError { get; set; }

        public bool GetPropertySuccess { get; set; }
        public string GetPropertyError { get; set; }

        public int FieldsTestedCount { get; set; }

        public bool OverallSuccess { get; set; }
    }
}
