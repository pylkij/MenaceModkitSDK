#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Multi-language localization system that loads ALL languages simultaneously.
/// Allows modders to view and edit translations for any language without
/// switching the game's active language.
/// </summary>
public static class MultiLingualLocalization
{
    /// <summary>
    /// Supported language names (matches game's LocaLanguage enum values)
    /// Note: Not all languages are shipped with the base game.
    /// </summary>
    public static class Languages
    {
        // Available in base game (confirmed working)
        public const string English = "English";
        public const string German = "German";
        public const string French = "French";
        public const string Russian = "Russian";
        public const string ChineseSimplified = "ChineseSimplified";
        public const string ChineseTraditional = "ChineseTraditional";
        public const string Japanese = "Japanese";
        public const string Korean = "Korean";
        public const string Polish = "Polish";
        public const string Turkish = "Turkish";

        // Not shipped with base game (may be DLC or regional)
        public const string Spanish = "Spanish";
        public const string Italian = "Italian";
        public const string Portuguese = "Portuguese";

        public static readonly string[] All = new[]
        {
            // Try all known languages (missing ones will be skipped with warning)
            English, German, French, Russian,
            ChineseSimplified, ChineseTraditional, Japanese, Korean, Polish, Turkish,
            Spanish, Italian, Portuguese
        };
    }

    // All translations: Language -> Category -> Key -> Translation
    private static Dictionary<string, Dictionary<string, Dictionary<string, TranslationEntry>>>
        _allTranslations;

    private static bool _initialized = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Translation entry with metadata
    /// </summary>
    public class TranslationEntry
    {
        public string Key { get; set; }
        public string Category { get; set; }
        public string DefaultText { get; set; }  // English
        public string TranslatedText { get; set; } // This language
        public string EntryType { get; set; }     // LocaEntryType string
    }

    /// <summary>
    /// Initialize the multi-lingual system by loading all available languages
    /// </summary>
    public static void Initialize(bool force = false)
    {
        lock (_lock)
        {
            if (_initialized && !force)
                return;

            _allTranslations = new Dictionary<string, Dictionary<string, Dictionary<string, TranslationEntry>>>();

            // Load all known language names
            foreach (var language in Languages.All)
            {
                try
                {
                    LoadLanguage(language);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load language {language}: {ex.Message}");
                }
            }

            _initialized = true;
            Debug.Log($"MultiLingualLocalization initialized with {_allTranslations.Count} languages");
        }
    }

    /// <summary>
    /// Load a specific language's CSV file
    /// </summary>
    private static void LoadLanguage(string language)
    {
        // Use game's naming convention
        var languageName = language.ToString();

        // Try to read CSV using game's system (checks filesystem first, then Resources)
        string csvContent = ReadCsvFile(languageName);

        if (string.IsNullOrEmpty(csvContent))
        {
            Debug.LogWarning($"No CSV content found for language: {languageName}");
            return;
        }

        // Parse CSV
        var categoryDict = ParseCsv(csvContent, language);

        if (categoryDict.Count > 0)
        {
            _allTranslations[language] = categoryDict;
            Debug.Log($"Loaded {language}: {categoryDict.Count} categories");
        }
    }

    /// <summary>
    /// Read CSV file from filesystem or Resources (mimics game's LocaData.ReadCsvFile)
    /// </summary>
    private static string ReadCsvFile(string languageName)
    {
        // Try filesystem first (modder override location)
        var dataPath = Application.dataPath;
        var parentDir = Directory.GetParent(dataPath)?.FullName;

        if (parentDir != null)
        {
            // Try common locations
            string[] possiblePaths = new[]
            {
                Path.Combine(parentDir, "Localization", $"{languageName}.csv"),
                Path.Combine(parentDir, "UserData", "Localization", $"{languageName}.csv"),
                Path.Combine(dataPath, "Localization", $"{languageName}.csv"),
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        Debug.Log($"Loading {languageName} from filesystem: {path}");
                        return File.ReadAllText(path, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to read {path}: {ex.Message}");
                    }
                }
            }
        }

        // Fall back to Resources (embedded in game)
        try
        {
            // Try with direct name first
            var textAsset = Resources.Load<TextAsset>(languageName);
            if (textAsset != null)
            {
                Debug.Log($"Loading {languageName} from Resources (direct)");
                return textAsset.text;
            }

            // Try with "Localization/" prefix (game might use this)
            textAsset = Resources.Load<TextAsset>($"Localization/{languageName}");
            if (textAsset != null)
            {
                Debug.Log($"Loading {languageName} from Resources (Localization/ prefix)");
                return textAsset.text;
            }

            // Try lowercase
            textAsset = Resources.Load<TextAsset>(languageName.ToLower());
            if (textAsset != null)
            {
                Debug.Log($"Loading {languageName} from Resources (lowercase)");
                return textAsset.text;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load {languageName} from Resources: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Parse CSV content into category -> key -> translation structure
    /// </summary>
    private static Dictionary<string, Dictionary<string, TranslationEntry>> ParseCsv(string csvContent, string language)
    {
        var result = new Dictionary<string, Dictionary<string, TranslationEntry>>();

        // Replace line endings
        csvContent = csvContent.Replace("\r\n", "\n").Replace("\r", "\n");

        var lines = csvContent.Split('\n');
        bool isFirstLine = true;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Skip header row
            if (isFirstLine)
            {
                isFirstLine = false;
                // Check if first cell is "Key" to confirm it's a header
                if (line.StartsWith("Key,") || line.StartsWith("\"Key\","))
                    continue;
            }

            // Parse CSV line (basic parsing, doesn't handle quotes with commas inside)
            var cells = ParseCsvLine(line);

            if (cells.Length < 4)
                continue; // Need at least: Key, Category/Type, DefaultText, TranslatedText

            var key = cells[0];

            // Skip if key is empty or is "Key" (header in some files)
            if (string.IsNullOrWhiteSpace(key) || key.Trim() == "Key")
                continue;

            // Extract category from key (e.g., "weapons.rifle.name" -> "weapons")
            var category = ExtractCategory(key);

            // Determine which columns contain what
            // Format appears to be: Key, Type, DefaultText, TranslatedText, [optional columns]
            var entryType = cells.Length > 1 ? cells[1] : "";
            var defaultText = cells.Length > 2 ? cells[2] : "";
            var translatedText = cells.Length > 3 ? cells[3] : defaultText;

            var entry = new TranslationEntry
            {
                Key = key,
                Category = category,
                EntryType = entryType,
                DefaultText = defaultText,
                TranslatedText = translatedText
            };

            // Add to result structure
            if (!result.ContainsKey(category))
            {
                result[category] = new Dictionary<string, TranslationEntry>();
            }

            result[category][key] = entry;
        }

        return result;
    }

    /// <summary>
    /// Simple CSV line parser (handles basic quoted fields)
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current.Append('"');
                    i++;
                }
                else
                {
                    // Toggle quote mode
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // End of field
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        // Add last field
        result.Add(current.ToString().Trim());

        return result.ToArray();
    }

    /// <summary>
    /// Extract category from localization key
    /// </summary>
    private static string ExtractCategory(string key)
    {
        var dotIndex = key.IndexOf('.');
        if (dotIndex > 0)
        {
            return key.Substring(0, dotIndex);
        }
        return "unknown";
    }

    /// <summary>
    /// Get translation for a specific language
    /// </summary>
    public static string GetTranslation(string language, string category, string key)
    {
        EnsureInitialized();

        if (_allTranslations.TryGetValue(language, out var categories))
        {
            if (categories.TryGetValue(category, out var entries))
            {
                if (entries.TryGetValue(key, out var entry))
                {
                    return entry.TranslatedText;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Get all translations for a key (all languages)
    /// </summary>
    public static Dictionary<string, string> GetAllTranslations(string category, string key)
    {
        EnsureInitialized();

        var result = new Dictionary<string, string>();

        foreach (var language in _allTranslations.Keys)
        {
            var translation = GetTranslation(language, category, key);
            if (!string.IsNullOrEmpty(translation))
            {
                result[language] = translation;
            }
        }

        return result;
    }

    /// <summary>
    /// Get full translation entry with metadata
    /// </summary>
    public static TranslationEntry GetTranslationEntry(string language, string category, string key)
    {
        EnsureInitialized();

        if (_allTranslations.TryGetValue(language, out var categories))
        {
            if (categories.TryGetValue(category, out var entries))
            {
                if (entries.TryGetValue(key, out var entry))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Get all translation entries for a key (all languages with metadata)
    /// </summary>
    public static Dictionary<string, TranslationEntry> GetAllTranslationEntries(string category, string key)
    {
        EnsureInitialized();

        var result = new Dictionary<string, TranslationEntry>();

        foreach (var language in _allTranslations.Keys)
        {
            var entry = GetTranslationEntry(language, category, key);
            if (entry != null)
            {
                result[language] = entry;
            }
        }

        return result;
    }

    /// <summary>
    /// List all available languages that were successfully loaded
    /// </summary>
    public static string[] GetAvailableLanguages()
    {
        EnsureInitialized();
        return _allTranslations.Keys.ToArray();
    }

    /// <summary>
    /// List all categories in a language
    /// </summary>
    public static string[] GetCategories(string language)
    {
        EnsureInitialized();

        if (_allTranslations.TryGetValue(language, out var categories))
        {
            return categories.Keys.ToArray();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// List all keys in a category for a language
    /// </summary>
    public static string[] GetKeys(string language, string category)
    {
        EnsureInitialized();

        if (_allTranslations.TryGetValue(language, out var categories))
        {
            if (categories.TryGetValue(category, out var entries))
            {
                return entries.Keys.ToArray();
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Check if a key exists in a specific language
    /// </summary>
    public static bool HasKey(string language, string category, string key)
    {
        EnsureInitialized();

        if (_allTranslations.TryGetValue(language, out var categories))
        {
            if (categories.TryGetValue(category, out var entries))
            {
                return entries.ContainsKey(key);
            }
        }

        return false;
    }

    /// <summary>
    /// Set/update translation for a specific language (in-memory only)
    /// </summary>
    public static void SetTranslation(string language, string category, string key, string text)
    {
        EnsureInitialized();

        if (!_allTranslations.ContainsKey(language))
        {
            _allTranslations[language] = new Dictionary<string, Dictionary<string, TranslationEntry>>();
        }

        if (!_allTranslations[language].ContainsKey(category))
        {
            _allTranslations[language][category] = new Dictionary<string, TranslationEntry>();
        }

        if (!_allTranslations[language][category].ContainsKey(key))
        {
            // Create new entry
            _allTranslations[language][category][key] = new TranslationEntry
            {
                Key = key,
                Category = category,
                DefaultText = text,
                TranslatedText = text,
                EntryType = "Text"
            };
        }
        else
        {
            // Update existing
            _allTranslations[language][category][key].TranslatedText = text;
        }

        Debug.Log($"Set translation [{language}] {category}.{key} = {text}");
    }

    /// <summary>
    /// Export language to CSV file
    /// </summary>
    public static void ExportLanguage(string language, string outputPath)
    {
        EnsureInitialized();

        if (!_allTranslations.TryGetValue(language, out var categories))
        {
            throw new ArgumentException($"Language {language} not loaded");
        }

        // Create CSV content
        var csv = new StringBuilder();
        csv.AppendLine("Key,Type,Default,Translation");

        foreach (var categoryPair in categories.OrderBy(c => c.Key))
        {
            foreach (var entryPair in categoryPair.Value.OrderBy(e => e.Key))
            {
                var entry = entryPair.Value;
                csv.AppendLine($"\"{entry.Key}\",\"{entry.EntryType}\",\"{EscapeCsv(entry.DefaultText)}\",\"{EscapeCsv(entry.TranslatedText)}\"");
            }
        }

        // Write to file
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, csv.ToString(), Encoding.UTF8);
        Debug.Log($"Exported {language} to {outputPath}");
    }

    /// <summary>
    /// Import language from CSV file
    /// </summary>
    public static void ImportLanguage(string language, string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"CSV file not found: {inputPath}");
        }

        var csvContent = File.ReadAllText(inputPath, Encoding.UTF8);
        var categoryDict = ParseCsv(csvContent, language);

        _allTranslations[language] = categoryDict;
        Debug.Log($"Imported {language} from {inputPath}: {categoryDict.Count} categories");
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Replace("\"", "\"\"").Replace("\n", "\\n").Replace("\r", "");
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Get statistics about loaded translations
    /// </summary>
    public static string GetStatistics()
    {
        EnsureInitialized();

        var sb = new StringBuilder();
        sb.AppendLine("=== Multi-Lingual Localization Statistics ===");
        sb.AppendLine($"Languages loaded: {_allTranslations.Count}");
        sb.AppendLine();

        foreach (var langPair in _allTranslations.OrderBy(l => l.Key))
        {
            var totalKeys = langPair.Value.Sum(c => c.Value.Count);
            sb.AppendLine($"{langPair.Key}: {langPair.Value.Count} categories, {totalKeys} keys");

            // Show category breakdown
            foreach (var categoryPair in langPair.Value.OrderByDescending(c => c.Value.Count).Take(5))
            {
                sb.AppendLine($"  - {categoryPair.Key}: {categoryPair.Value.Count} keys");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Diagnostic method to discover localization TextAsset resources in the game
    /// </summary>
    public static string DiscoverLocalizationResources()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Discovering Localization Resources ===");
        sb.AppendLine();

        try
        {
            // Get all TextAssets in the game
            var allAssets = Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<TextAsset>());
            sb.AppendLine($"Total TextAssets in game: {allAssets.Length}");
            sb.AppendLine();

            var candidates = new List<(string name, int size)>();

            // Search for assets that look like localization CSVs
            foreach (var obj in allAssets)
            {
                var asset = obj.Cast<TextAsset>();
                if (asset == null) continue;

                var text = asset.text;
                if (string.IsNullOrEmpty(text) || text.Length < 500) continue;

                // Check if it looks like a localization CSV
                // Must have Key column and be reasonably large
                if ((text.Contains("Key,") || text.Contains("\"Key\"")) &&
                    (text.Contains("Default") || text.Contains("Translation") || text.Contains("Text")) &&
                    text.Length > 10000) // Localization files are typically large
                {
                    candidates.Add((asset.name, text.Length));
                    sb.AppendLine($"Found candidate: \"{asset.name}\" ({text.Length} chars)");

                    // Show first 200 characters
                    var preview = text.Substring(0, Math.Min(200, text.Length)).Replace("\n", "\\n");
                    sb.AppendLine($"  Preview: {preview}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"Total candidates found: {candidates.Count}");
            sb.AppendLine();

            // Try to load each candidate
            sb.AppendLine("=== Attempting to Load Candidates ===");
            foreach (var (name, size) in candidates)
            {
                try
                {
                    var asset = Resources.Load<TextAsset>(name);
                    if (asset != null)
                    {
                        sb.AppendLine($"✓ Successfully loaded: \"{name}\"");

                        // Try to parse as CSV
                        var categoryDict = ParseCsv(asset.text, name);
                        sb.AppendLine($"  Parsed: {categoryDict.Count} categories");

                        if (categoryDict.Count > 0)
                        {
                            var firstCat = categoryDict.First();
                            sb.AppendLine($"  Sample category: \"{firstCat.Key}\" with {firstCat.Value.Count} keys");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"✗ Failed to load: \"{name}\" (Resources.Load returned null)");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"✗ Error loading \"{name}\": {ex.Message}");
                }
                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"ERROR: {ex.Message}");
            sb.AppendLine(ex.StackTrace);
        }

        return sb.ToString();
    }
}
