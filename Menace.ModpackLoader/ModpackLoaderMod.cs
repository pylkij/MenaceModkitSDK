using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using Menace.ModpackLoader.Diagnostics;
using Menace.ModpackLoader.Mcp;
using Menace.ModpackLoader.TemplateLoading;
using Menace.ModpackLoader.VisualEditor.Runtime;
using Menace.SDK;
using Menace.SDK.CustomMaps;
using Menace.SDK.Internal;
using Menace.SDK.Repl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Menace.SDK.Modpacks;

[assembly: MelonInfo(typeof(Menace.ModpackLoader.ModpackLoaderMod), "Menace Modpack Loader", Menace.ModkitVersion.MelonVersion, "Menace Modkit")]
[assembly: MelonGame(null, null)]
[assembly: MelonOptionalDependencies(
    "Microsoft.CodeAnalysis",
    "Microsoft.CodeAnalysis.CSharp",
    "System.Collections.Immutable",
    "System.Reflection.Metadata",
    "System.Text.Encoding.CodePages",
    "Newtonsoft.Json",
    "SharpGLTF.Core")]

namespace Menace.ModpackLoader;

public partial class ModpackLoaderMod : MelonMod
{
    private readonly Dictionary<string, Modpack> _loadedModpacks = new();
    private readonly HashSet<string> _registeredAssetPaths = new();
    private bool _templatesLoaded = false;

    private readonly HashSet<string> _appliedPatchKeys = new();

    public override void OnInitializeMelon()
    {
        // SdkLogger must initialize before subsystems that log.
        SdkLogger.Initialize(LoggerInstance);
        OffsetCache.Initialize();
        DevConsole.Initialize();
        DevConsole.ApplyInputPatches(HarmonyInstance);

        InitializeTemplateSchema();

        SdkLogger.Msg($"{ModkitVersion.LoaderFull} initialized");
        ModSettings.Initialize();

        RegisterModpackLoaderSettings();

        InitializeRepl();

        InitializeDiagnostics();

        RegisterSdkCommands();

        GameMcpServer.Initialize(LoggerInstance);

        MenuInjector.Initialize();

        GlbLoader.Initialize();

        LoadModpacks();
        DllLoader.InitializeAllPlugins();

        EarlyTemplateInjection.Initialize(this, HarmonyInstance);

        TacticalEventHooks.Initialize(HarmonyInstance);

        TacticalEventHooks.OnRoundEnd += _ => EffectSystem.OnRoundEnd();

        SDK.Coroutine.Initialize();

        StrategyEventHooks.Initialize(HarmonyInstance);

        StrategyEventHooks.OnMissionFinished += (_, _, _) =>
        {
            SDK.Coroutine.Cleanup();
            StateMachine.Cleanup();
            OnceTracker.Cleanup();
            EffectSystem.ClearAll();
        };

        BugReporterPatches.Initialize(LoggerInstance, HarmonyInstance);

        BootSkip.Initialize(HarmonyInstance);

        CustomMaps.Initialize(HarmonyInstance);

        try
        {
            LuaScriptEngine.Instance.Initialize(LoggerInstance);
            GameState.SceneLoaded += LuaScriptEngine.Instance.OnSceneLoaded;
            GameState.TacticalReady += LuaScriptEngine.Instance.OnTacticalReady;
            LoadLuaScripts();
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[LuaEngine] Failed to initialize: {ex.GetType().Name}: {ex.Message}");
            SdkLogger.Error($"[LuaEngine] Stack: {ex.StackTrace}");
        }

        LoadCustomMaps();

        try
        {
            GraphInterpreter.Instance.Initialize();
            LoadVisualMods();
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[GraphInterpreter] Failed to initialize: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            SdkLogger.Msg("[Localization] Initializing multi-lingual system...");
            MultiLingualLocalization.Initialize();
            SdkLogger.Msg($"[Localization] Loaded {MultiLingualLocalization.GetAvailableLanguages().Length} languages");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Localization] Failed to initialize multi-lingual system: {ex.Message}");
        }

        PlayerLog("========================================");
        PlayerLog("THIS GAME SESSION IS RUNNING MODDED");
        PlayerLog(ModkitVersion.LoaderFull);
        PlayerLog($"Loaded {_loadedModpacks?.Count ?? 0} modpack(s):");
        if (_loadedModpacks != null)
            foreach (var mp in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
                PlayerLog($"  - {mp.Name} v{mp.Version} by {mp.Author ?? "Unknown"} (order: {mp.LoadOrder})");
        try { PlayerLog($"Bundles: {BundleLoader.LoadedBundleCount} ({BundleLoader.LoadedAssetCount} assets)"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] BundleLoader failed: {ex.Message}"); }
        try { PlayerLog($"Asset replacements registered: {AssetReplacer.RegisteredCount}"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] AssetReplacer.RegisteredCount failed: {ex.Message}"); }
        try { PlayerLog($"Custom sprites loaded: {AssetReplacer.CustomSpriteCount}"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] AssetReplacer.CustomSpriteCount failed: {ex.Message}"); }
        try { PlayerLog($"Compiled assets in manifest: {CompiledAssetLoader.ManifestAssetCount}"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] CompiledAssetLoader failed: {ex.Message}"); }
        try { PlayerLog($"Mod DLLs: {DllLoader.GetLoadedAssemblies()?.Count ?? 0}"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] DllLoader failed: {ex.Message}"); }
        var pluginSummary = DllLoader.GetPluginSummary();
        if (pluginSummary != null)
            PlayerLog($"Modpack plugins: {pluginSummary}");
        try { PlayerLog($"Lua scripts loaded: {LuaScriptEngine.Instance?.LoadedScriptCount ?? 0}"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] LuaScriptEngine failed: {ex.Message}"); }
        try { PlayerLog($"Visual mods loaded: {GraphInterpreter.Instance?.LoadedMods?.Count ?? 0}"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] GraphInterpreter failed: {ex.Message}"); }
        try { PlayerLog($"Custom maps registered: {CustomMapRegistry.Count}"); } catch (Exception ex) { SdkLogger.Warning($"[Summary] CustomMapRegistry failed: {ex.Message}"); }
        PlayerLog("========================================");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        ModSettings.Save();

        DevConsole.IsVisible = false;
        GameState.NotifySceneLoaded(sceneName);
        GameQuery.ClearCache();
        DllLoader.NotifySceneLoaded(buildIndex, sceneName);
        MenuInjector.OnSceneLoaded(sceneName);

        // Retry template patches on every scene until all types are found.
        // Some builds (e.g. EA) load templates in later scenes, not the title screen.
        if (!_templatesLoaded)
        {
            SdkLogger.Msg($"Scene '{sceneName}' loaded, attempting template patches...");
            MelonCoroutines.Start(WaitForTemplatesAndApply(sceneName));
        }

        if (AssetReplacer.RegisteredCount > 0 || BundleLoader.LoadedAssetCount > 0)
        {
            MelonCoroutines.Start(ApplyAssetReplacementsDelayed(sceneName));
        }
    }

    public override void OnUpdate()
    {
        MainThreadExecutor.ProcessQueue();
        GameState.ProcessUpdate();
        DevConsole.Update();
        MenuInjector.Update();
        DllLoader.NotifyUpdate();
    }

    public override void OnGUI()
    {
        DevConsole.Draw();
        ErrorNotification.Draw();
        MenuInjector.Draw();
        DllLoader.NotifyOnGUI();
    }

    public override void OnApplicationQuit()
    {
        ModSettings.Save();
        GameMcpServer.Stop();
        SaveSystemPatches.Shutdown();
    }

    private System.Collections.IEnumerator WaitForTemplatesAndApply(string sceneName)
    {
        for (int i = 0; i < 30; i++)
        {
            yield return null;
        }

        if (EarlyTemplateInjection.IsEnabled && EarlyTemplateInjection.HasInjectedThisSession)
        {
            SdkLogger.Msg($"Early injection already applied, skipping legacy scene-load injection");
            _templatesLoaded = true;
            yield break;
        }

        SdkLogger.Msg($"Applying modpack modifications (scene: {sceneName})...");

        SaveSystemPatches.TryInitialize();

        CompiledAssetLoader.LoadAssets();

        var pendingCount = AssetReplacer.PendingSpriteCount;
        if (pendingCount > 0)
        {
            SdkLogger.Msg($"Loading {pendingCount} sprites asynchronously...");
            var spriteLoader = AssetReplacer.LoadPendingSpritesAsync(5);
            while (spriteLoader.MoveNext())
            {
                yield return spriteLoader.Current;
            }
        }

        var allApplied = ApplyAllModpacks();

        if (allApplied)
        {
            SdkLogger.Msg("All template patches applied successfully.");
            _templatesLoaded = true;
            PlayerLog("All template patches applied successfully");
        }
        else
        {
            SdkLogger.Warning("Some template types not yet loaded — will retry on next scene.");
        }
    }

    private static void RegisterModpackLoaderSettings()
    {
        ModSettings.Register("Modpack Loader", settings =>
        {
            settings.AddHeader("GLB Model Loading");
            settings.AddToggle("GlbLoader", "Enable GLB Loading", true);
        });
    }

    private static void RegisterSdkCommands()
    {
        Inventory.RegisterConsoleCommands();
        Operation.RegisterConsoleCommands();
        ArmyGeneration.RegisterConsoleCommands();
        Vehicle.RegisterConsoleCommands();
        Conversation.RegisterConsoleCommands();
        Emotions.RegisterConsoleCommands();
        BlackMarket.RegisterConsoleCommands();
        Mission.RegisterConsoleCommands();
        Roster.RegisterConsoleCommands();
        Perks.RegisterConsoleCommands();
        TileMap.RegisterConsoleCommands();
        Pathfinding.RegisterConsoleCommands();
        LineOfSight.RegisterConsoleCommands();
        TileEffects.RegisterConsoleCommands();
        BootSkip.RegisterConsoleCommands();
        SimpleAnimations.RegisterConsoleCommands();
        UIInspector.RegisterConsoleCommands();
        Modpacks.RegisterConsoleCommands();
        GraphInterpreter.RegisterConsoleCommands();

        TestHarnessCommands.Register();

        DataTemplateLoaderDiagnostics.RegisterConsoleCommands();
        SceneLoadingDiagnostics.RegisterConsoleCommands();
        SdkSafetyTesting.RegisterConsoleCommands();
        TemplatePipelineValidator.RegisterConsoleCommands();
    }

    private void InitializeDiagnostics()
    {
        try
        {
            DataTemplateLoaderDiagnostics.Initialize(HarmonyInstance);
            SceneLoadingDiagnostics.Initialize(HarmonyInstance);

            TemplateLoadingFixes.Initialize(HarmonyInstance);
            SceneLoadingFixes.Initialize(HarmonyInstance);

            SdkLogger.Msg("Diagnostics and fixes initialized");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"Failed to initialize diagnostics: {ex.Message}");
        }
    }

    private void InitializeTemplateSchema()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var candidatePaths = new[]
        {
            Path.Combine(baseDir, "UserData", "Menace", "schema.json"),
            Path.Combine(baseDir, "Mods", "MenaceModpackLoader", "schema.json"),
            Path.Combine(baseDir, "schema.json"),
            Path.Combine(Path.GetDirectoryName(typeof(ModpackLoaderMod).Assembly.Location) ?? baseDir, "schema.json"),
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                TemplateSchema.Initialize(path);
                return;
            }
        }

        SdkLogger.Warning("[TemplateSchema] schema.json not found - schema-driven offsets unavailable");
    }

    private void LoadModpacks()
    {
        var modsPath = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
        if (!Directory.Exists(modsPath))
        {
            SdkLogger.Warning($"Mods directory not found: {modsPath}");
            return;
        }

        SdkLogger.Msg($"Loading modpacks from: {modsPath}");

        var modpackFiles = Directory.GetFiles(modsPath, "modpack.json", SearchOption.AllDirectories);

        var modpackEntries = new List<(string file, int order, int version)>();

        foreach (var modpackFile in modpackFiles)
        {
            try
            {
                var json = File.ReadAllText(modpackFile);
                var obj = JObject.Parse(json);
                var manifestVersion = obj.Value<int?>("manifestVersion") ?? 1;
                var loadOrder = obj.Value<int?>("loadOrder") ?? 100;
                modpackEntries.Add((modpackFile, loadOrder, manifestVersion));
            }
            catch
            {
                modpackEntries.Add((modpackFile, 100, 1));
            }
        }

        foreach (var (modpackFile, _, manifestVersion) in modpackEntries.OrderBy(e => e.order))
        {
            try
            {
                var modpackDir = Path.GetDirectoryName(modpackFile);
                var json = File.ReadAllText(modpackFile);
                var modpack = JsonConvert.DeserializeObject<Modpack>(json);

                if (modpack != null)
                {
                    modpack.DirectoryPath = modpackDir;
                    modpack.ManifestVersion = manifestVersion;

                    if ((modpack.Clones == null || modpack.Clones.Count == 0) && !string.IsNullOrEmpty(modpackDir))
                    {
                        var clonesDir = Path.Combine(modpackDir, "clones");
                        if (Directory.Exists(clonesDir))
                        {
                            modpack.Clones = new Dictionary<string, Dictionary<string, string>>();
                            foreach (var file in Directory.GetFiles(clonesDir, "*.json"))
                            {
                                try
                                {
                                    var templateType = Path.GetFileNameWithoutExtension(file);
                                    var cloneJson = File.ReadAllText(file);
                                    var cloneMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(cloneJson);
                                    if (cloneMap != null && cloneMap.Count > 0)
                                        modpack.Clones[templateType] = cloneMap;
                                }
                                catch { }
                            }
                        }
                    }

                    _loadedModpacks[modpack.Name] = modpack;

                    ModRegistry.RegisterModpack(modpack.Name, modpack.Version, modpack.Author);

                    var vLabel = manifestVersion >= 2 ? "v2" : "v1 (legacy)";
                    SdkLogger.Msg($"  Loaded [{vLabel}]: {modpack.Name} v{modpack.Version} (order: {modpack.LoadOrder})");

                    if (manifestVersion >= 2 && !string.IsNullOrEmpty(modpackDir))
                    {
                        BundleLoader.LoadBundles(modpackDir, modpack.Name);
                        GlbLoader.LoadModpackModels(modpackDir, modpack.Name);
                        DllLoader.LoadModDlls(modpackDir, modpack.Name, modpack.SecurityStatus ?? "Unreviewed");
                    }

                    // Textures are NOT compiled into assets due to ColorSpace issues,
                    // so replacements are applied at runtime via ImageConversion.LoadImage
                    if (modpack.Assets != null)
                    {
                        LoadModpackAssets(modpack);
                    }
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"Failed to load modpack from {modpackFile}: {ex.Message}");
            }
        }

        SdkLogger.Msg($"Loaded {_loadedModpacks.Count} modpack(s)");

        var compiledDir = Path.Combine(modsPath, "compiled");
        if (Directory.Exists(compiledDir))
        {
            CompiledAssetLoader.LoadManifest(compiledDir);
        }
    }

    private void LoadLuaScripts()
    {
        int scriptCount = 0;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            if (string.IsNullOrEmpty(modpack.DirectoryPath))
                continue;

            var scriptsDir = Path.Combine(modpack.DirectoryPath, "scripts");
            if (!Directory.Exists(scriptsDir))
                continue;

            var luaFiles = Directory.GetFiles(scriptsDir, "*.lua", SearchOption.AllDirectories);
            foreach (var luaFile in luaFiles)
            {
                if (LuaScriptEngine.Instance.LoadModpackScript(modpack.Name, luaFile))
                    scriptCount++;
            }
        }

        if (scriptCount > 0)
            SdkLogger.Msg($"Loaded {scriptCount} Lua script(s)");
    }

    private void LoadCustomMaps()
    {
        int mapCount = 0;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            if (string.IsNullOrEmpty(modpack.DirectoryPath))
                continue;

            var mapsDir = Path.Combine(modpack.DirectoryPath, "custom_maps");
            if (!Directory.Exists(mapsDir))
                continue;

            var count = CustomMapRegistry.LoadFromDirectory(mapsDir);
            if (count > 0)
            {
                SdkLogger.Msg($"  Loaded {count} custom map(s) from {modpack.Name}");
                mapCount += count;
            }
        }

        if (mapCount > 0)
            SdkLogger.Msg($"Loaded {mapCount} custom map(s) total");
    }

    private void LoadVisualMods()
    {
        int graphCount = 0;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            if (string.IsNullOrEmpty(modpack.DirectoryPath))
                continue;

            var modsDir = Path.Combine(modpack.DirectoryPath, "visual_mods");
            if (!Directory.Exists(modsDir))
                continue;

            var count = GraphInterpreter.Instance.LoadModsFromDirectory(modsDir);
            if (count > 0)
            {
                SdkLogger.Msg($"  Loaded {count} visual mod(s) from {modpack.Name}");
                graphCount += count;
            }
        }

        if (graphCount > 0)
            SdkLogger.Msg($"Loaded {graphCount} visual mod(s) total");
    }

    private void LoadModpackAssets(Modpack modpack)
    {
        if (modpack.Assets == null || string.IsNullOrEmpty(modpack.DirectoryPath))
            return;

        if (modpack.PreloadAssets != null)
        {
            foreach (var preloadName in modpack.PreloadAssets)
            {
                AssetReplacer.RegisterPreloadAsset(preloadName);
            }
            if (modpack.PreloadAssets.Count > 0)
                SdkLogger.Msg($"  Registered {modpack.PreloadAssets.Count} preload asset(s)");
        }

        foreach (var (assetPath, replacementFile) in modpack.Assets)
        {
            try
            {
                var fullPath = ValidatePathWithinModpack(modpack.DirectoryPath, replacementFile);
                if (fullPath == null)
                {
                    SdkLogger.Warning($"  Path traversal blocked for asset: {replacementFile}");
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    _registeredAssetPaths.Add(assetPath);
                    var ext = Path.GetExtension(assetPath).ToLowerInvariant();
                    var assetName = Path.GetFileNameWithoutExtension(assetPath);

                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".bmp")
                    {
                        AssetReplacer.RegisterAssetReplacement(assetPath, fullPath);
                        SdkLogger.Msg($"  Registered asset replacement: {assetPath}");

                        var sprite = AssetReplacer.LoadCustomSprite(fullPath, assetName);
                        if (sprite != null)
                            SdkLogger.Msg($"  Preloaded sprite ready: '{assetName}'");
                    }
                    else if (ext == ".glb" || ext == ".gltf")
                    {
                        var model = GlbLoader.LoadAndRegisterGlb(fullPath, modpack.Name);
                        if (model != null)
                            SdkLogger.Msg($"  Custom model loaded: '{assetName}' ({model.Meshes.Count} meshes)");
                        else if (!GlbLoader.IsEnabled)
                            SdkLogger.Msg($"  Skipped custom model: '{assetName}' (GLB loading disabled)");
                        else
                            SdkLogger.Warning($"  Failed to load custom model: '{assetName}'");
                    }
                    else if (ext == ".wav" || ext == ".ogg")
                    {
                        var clip = AssetReplacer.LoadCustomAudio(fullPath, assetName);
                        if (clip != null)
                            SdkLogger.Msg($"  Custom audio loaded: '{assetName}'");
                        else
                            SdkLogger.Warning($"  Failed to load custom audio: '{assetName}'");
                    }
                    else
                    {
                        AssetReplacer.RegisterAssetReplacement(assetPath, fullPath);
                        SdkLogger.Msg($"  Registered asset replacement: {assetPath}");
                    }
                }
                else
                {
                    SdkLogger.Warning($"  Asset file not found: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"  Failed to load asset {assetPath}: {ex.Message}");
            }
        }

        var pendingCount = AssetReplacer.PendingSpriteCount;
        if (pendingCount > 0)
            SdkLogger.Msg($"  {pendingCount} sprite(s) queued for async loading");
    }

    private static string ValidatePathWithinModpack(string modpackDir, string relativePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(modpackDir, relativePath));
            var baseFullPath = Path.GetFullPath(modpackDir);

            if (!baseFullPath.EndsWith(Path.DirectorySeparatorChar))
                baseFullPath += Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(baseFullPath, StringComparison.Ordinal) &&
                !fullPath.Equals(baseFullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
            {
                return null;
            }

            return fullPath;
        }
        catch
        {
            return null;
        }
    }

    internal bool ApplyAllModpacks()
    {
        if (_loadedModpacks.Count == 0)
        {
            SdkLogger.Msg("No modpacks to apply");
            return true;
        }

        RegisterBundleClones();

        var allSucceeded = true;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            var hasClones = modpack.Clones != null && modpack.Clones.Count > 0;
            var hasPatches = modpack.Patches != null && modpack.Patches.Count > 0;
            var hasTemplates = modpack.Templates != null && modpack.Templates.Count > 0;

            if (!hasClones && !hasPatches && !hasTemplates)
                continue;

            SdkLogger.Msg($"Applying modpack: {modpack.Name}");

            // Apply clones BEFORE patches so cloned templates exist when patches run
            if (hasClones)
            {
                if (!ApplyClones(modpack))
                    allSucceeded = false;

                InvalidateNameLookupCache();
            }

            bool success;
            if (hasPatches && modpack.ManifestVersion >= 2)
            {
                success = ApplyModpackPatches(modpack);
            }
            else if (hasTemplates)
            {
                success = ApplyModpackTemplates(modpack);
            }
            else
            {
                success = true;
            }

            if (!success)
                allSucceeded = false;
        }

        if (_appliedPatchKeys.Count > 0)
        {
            var patchedTypes = _appliedPatchKeys.Select(k => k.Split(':').Last()).Distinct();
            PlayerLog($"Template types patched: {string.Join(", ", patchedTypes)}");
        }

        return allSucceeded;
    }

    private bool ApplyTemplateData(
        Modpack modpack,
        Dictionary<string, Dictionary<string, Dictionary<string, object>>> data,
        string label)
    {
        if (data == null) return true;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            SdkLogger.Error($"Assembly-CSharp not found, cannot apply {label}");
            return false;
        }

        var allFound = true;

        foreach (var (templateTypeName, templateInstances) in data)
        {
            var patchKey = $"{modpack.Name}:{templateTypeName}";

            if (_appliedPatchKeys.Contains(patchKey))
                continue;

            try
            {
                var templateType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

                if (templateType == null)
                {
                    SdkLogger.Warning($"  Template type '{templateTypeName}' not found in Assembly-CSharp");
                    allFound = false;
                    continue;
                }

                EnsureTemplatesLoaded(gameAssembly, templateType);

                var il2cppType = Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects == null || objects.Length == 0)
                {
                    SdkLogger.Warning($"  No {templateTypeName} instances found — will retry on next scene");
                    allFound = false;
                    continue;
                }

                int appliedCount = 0;
                var getIdMethod = templateType.GetMethod("GetID",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                var patchKeys = string.Join(", ", templateInstances.Keys.Take(5));
                SdkLogger.Msg($"  [Debug] {templateTypeName}: {objects.Length} instances, GetID={getIdMethod != null}, patches=[{patchKeys}]");

                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    var templateName = obj.name;

                    if (templateInstances.ContainsKey(templateName))
                    {
                        SdkLogger.Msg($"    [Debug] obj.name matched: '{templateName}'");
                        var modifications = templateInstances[templateName];
                        ApplyTemplateModifications(obj, templateType, modifications);
                        appliedCount++;
                    }
                    // Fall back to matching by GetID() (m_ID) for cloned templates
                    // Cloned templates have m_Name unchanged but m_ID set to the new name
                    else if (getIdMethod != null)
                    {
                        try
                        {
                            var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
                            var castObj = genericTryCast.Invoke(obj, null);
                            if (castObj != null)
                            {
                                var templateId = getIdMethod.Invoke(castObj, null)?.ToString();
                                if (!string.IsNullOrEmpty(templateId) && templateInstances.ContainsKey(templateId))
                                {
                                    SdkLogger.Msg($"    [Debug] GetID matched: '{templateId}'");
                                    var modifications = templateInstances[templateId];
                                    ApplyTemplateModifications(obj, templateType, modifications);
                                    appliedCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SdkLogger.Warning($"    [Debug] GetID failed for {templateName}: {ex.Message}");
                        }
                    }
                }

                if (appliedCount > 0)
                {
                    SdkLogger.Msg($"  Applied {label} to {appliedCount} {templateTypeName} instance(s)");
                    _appliedPatchKeys.Add(patchKey);
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"  Failed to apply {label} to {templateTypeName}: {ex.Message}");
            }
        }

        return allFound;
    }

    private bool ApplyModpackPatches(Modpack modpack)
    {
        return ApplyTemplateData(modpack, modpack.Patches, "patches");
    }

    private bool ApplyModpackTemplates(Modpack modpack)
    {
        return ApplyTemplateData(modpack, modpack.Templates, "modifications");
    }

    private System.Collections.IEnumerator ApplyAssetReplacementsDelayed(string sceneName)
    {
        SdkLogger.Msg($"Asset replacement queued for scene: {sceneName} ({AssetReplacer.RegisteredCount} disk, {BundleLoader.LoadedAssetCount} bundle)");

        for (int i = 0; i < 15; i++)
            yield return null;

        SdkLogger.Msg($"Applying asset replacements for scene: {sceneName}");
        AssetReplacer.ApplyAllReplacements();
        PlayerLog($"Asset replacements applied for scene: {sceneName}");
    }

    private void InitializeRepl()
    {
        try
        {
            // Roslyn types live in a separate method to prevent the JIT from resolving
            // Microsoft.CodeAnalysis when compiling THIS method. Without this split,
            // the FileNotFoundException fires during JIT (before the try block runs)
            // and escapes the catch, crashing the entire OnInitializeMelon.
            InitializeReplCore();
        }
        catch (System.IO.FileNotFoundException ex)
        {
            SdkLogger.Warning($"REPL initialization failed - missing assembly: {ex.FileName}");
            SdkLogger.Warning($"  Message: {ex.Message}");
            SdkLogger.Warning($"  FusionLog: {ex.FusionLog ?? "(none)"}");
        }
        catch (System.IO.FileLoadException ex)
        {
            SdkLogger.Warning($"REPL initialization failed - assembly load error: {ex.FileName}");
            SdkLogger.Warning($"  Message: {ex.Message}");
            SdkLogger.Warning($"  FusionLog: {ex.FusionLog ?? "(none)"}");
        }
        catch (System.TypeLoadException ex)
        {
            SdkLogger.Warning($"REPL initialization failed - type load error: {ex.TypeName}");
            SdkLogger.Warning($"  Message: {ex.Message}");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"REPL initialization failed: {ex.GetType().Name}");
            SdkLogger.Warning($"  Message: {ex.Message}");
            if (ex.InnerException != null)
                SdkLogger.Warning($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void InitializeReplCore()
    {
        var resolver = new RuntimeReferenceResolver();
        var refs = resolver.ResolveAll();
        var compiler = new RuntimeCompiler(refs);
        var evaluator = new ConsoleEvaluator(compiler);
        ReplPanel.Initialize(evaluator);
        DevConsole.SetReplEvaluator(evaluator);
        SdkLogger.Msg($"REPL initialized with {refs.Count} references");
    }

    private static void PlayerLog(string message)
    {
        SdkLogger.Msg($"[MODDED] {message}");
    }
}

public class Modpack
{
    [JsonProperty("manifestVersion")]
    public int ManifestVersion { get; set; } = 1;

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; }

    [JsonProperty("loadOrder")]
    public int LoadOrder { get; set; } = 100;

    [JsonProperty("templates")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>> Templates { get; set; }

    [JsonProperty("patches")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>> Patches { get; set; }

    [JsonProperty("assets")]
    public Dictionary<string, string> Assets { get; set; }

    [JsonProperty("bundles")]
    public List<string> Bundles { get; set; }

    [JsonProperty("securityStatus")]
    public string SecurityStatus { get; set; }

    [JsonProperty("clones")]
    public Dictionary<string, Dictionary<string, string>> Clones { get; set; }

    [JsonProperty("preloadAssets")]
    public List<string> PreloadAssets { get; set; }

    [JsonProperty("repositoryType")]
    public string RepositoryType { get; set; }

    [JsonProperty("repositoryUrl")]
    public string RepositoryUrl { get; set; }

    [JsonIgnore]
    public string DirectoryPath { get; set; }
}
