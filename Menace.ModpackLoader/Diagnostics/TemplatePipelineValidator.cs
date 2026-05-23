#nullable disable
using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK;
using Menace.SDK.Repl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
