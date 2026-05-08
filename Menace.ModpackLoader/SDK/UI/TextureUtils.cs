using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace Menace.SDK.UI;

/// <summary>
/// Utilities for loading PNG textures and icons from disk.
/// </summary>
public static class TextureUtils
{
    /// <summary>
    /// Load a single PNG file as a Texture2D.
    /// </summary>
    /// <param name="filePath">Path to PNG file</param>
    /// <param name="filterMode">Texture filter mode (default: Point for pixel-perfect)</param>
    /// <returns>Loaded texture or null if loading failed</returns>
    public static Texture2D LoadPngTexture(string filePath, FilterMode filterMode = FilterMode.Point)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var texture = new Texture2D(2, 2);
            texture.hideFlags = HideFlags.HideAndDontSave;

            if (ImageConversion.LoadImage(texture, bytes))
            {
                texture.filterMode = filterMode;
                return texture;
            }

            UnityEngine.Object.Destroy(texture);
            return null;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[TextureUtils] Failed to load texture from {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load all PNG files from a directory into a dictionary (filename -> texture).
    /// Filenames are lowercased and extensions removed for case-insensitive lookup.
    /// </summary>
    /// <param name="directoryPath">Directory containing PNG files</param>
    /// <param name="filterMode">Texture filter mode</param>
    /// <param name="log">Optional logger for warnings</param>
    /// <returns>Dictionary of icon name -> texture</returns>
    /// <example>
    /// <code>
    /// var icons = TextureUtils.LoadIconsFromDirectory("Mods/MyMod/icons");
    /// var playerIcon = icons["player"];  // loads player.png
    /// </code>
    /// </example>
    public static Dictionary<string, Texture2D> LoadIconsFromDirectory(
        string directoryPath,
        FilterMode filterMode = FilterMode.Point,
        MelonLogger.Instance log = null)
    {
        var icons = new Dictionary<string, Texture2D>();

        if (!Directory.Exists(directoryPath))
        {
            log?.Warning($"[TextureUtils] Icon directory not found: {directoryPath}");
            return icons;
        }

        foreach (var file in Directory.GetFiles(directoryPath, "*.png"))
        {
            try
            {
                var texture = LoadPngTexture(file, filterMode);
                if (texture != null)
                {
                    var iconKey = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    icons[iconKey] = texture;
                }
                else
                {
                    log?.Warning($"[TextureUtils] Failed to decode icon: {file}");
                }
            }
            catch (Exception exception)
            {
                log?.Warning($"[TextureUtils] Failed to load icon {file}: {exception.Message}");
            }
        }

        return icons;
    }

    /// <summary>
    /// Load multiple icon folders (folder path -> icon dictionary).
    /// Useful when different styles/themes have separate icon sets.
    /// </summary>
    /// <param name="folderNames">List of folder names relative to basePath</param>
    /// <param name="basePath">Base path to prepend to folder names</param>
    /// <param name="filterMode">Texture filter mode</param>
    /// <param name="log">Optional logger</param>
    /// <returns>Dictionary of folder name -> (icon name -> texture)</returns>
    public static Dictionary<string, Dictionary<string, Texture2D>> LoadIconFolders(
        IEnumerable<string> folderNames,
        string basePath,
        FilterMode filterMode = FilterMode.Point,
        MelonLogger.Instance log = null)
    {
        var allIcons = new Dictionary<string, Dictionary<string, Texture2D>>();

        foreach (var folder in folderNames)
        {
            var iconDirectory = Path.Combine(basePath, folder);
            var folderIcons = LoadIconsFromDirectory(iconDirectory, filterMode, log);

            if (folderIcons.Count > 0)
            {
                allIcons[folder] = folderIcons;
                log?.Msg($"[TextureUtils] Loaded {folderIcons.Count} icon(s) from {folder}/");
            }
        }

        return allIcons;
    }

    /// <summary>
    /// Crop a texture to a specific region.
    /// Creates a new texture with the cropped pixels.
    /// </summary>
    /// <param name="source">Source texture</param>
    /// <param name="x">Starting X coordinate</param>
    /// <param name="y">Starting Y coordinate</param>
    /// <param name="width">Crop width</param>
    /// <param name="height">Crop height</param>
    /// <param name="filterMode">Filter mode for new texture</param>
    /// <returns>New cropped texture</returns>
    public static Texture2D CropTexture(
        Texture2D source,
        int x, int y, int width, int height,
        FilterMode filterMode = FilterMode.Point)
    {
        var pixels = source.GetPixels(x, y, width, height);
        var cropped = new Texture2D(width, height);
        cropped.hideFlags = HideFlags.HideAndDontSave;
        cropped.filterMode = filterMode;
        cropped.SetPixels(pixels);
        cropped.Apply();
        return cropped;
    }

    /// <summary>
    /// Create a solid color texture.
    /// </summary>
    /// <param name="width">Texture width</param>
    /// <param name="height">Texture height</param>
    /// <param name="color">Fill color</param>
    /// <param name="filterMode">Filter mode</param>
    /// <returns>New solid color texture</returns>
    public static Texture2D CreateSolidTexture(
        int width, int height, Color color,
        FilterMode filterMode = FilterMode.Point)
    {
        var texture = new Texture2D(width, height);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = filterMode;

        var pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
