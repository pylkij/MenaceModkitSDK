using System;
using System.IO;
using UnityEngine;

namespace Menace.SDK.Maps.IO;

/// <summary>
/// Utilities for loading and saving tactical map data.
/// Supports binary map format (.bin) and PNG heightmap import/export.
/// </summary>
public static class MapDataIO
{
    /// <summary>
    /// Load map data from binary file.
    /// Binary format:
    /// - Header: width (int32), height (int32), heightMin (float32), heightMax (float32)
    /// - Per-tile: height (float32), flags (byte)
    /// </summary>
    /// <param name="filePath">Path to .bin file</param>
    /// <returns>Loaded map data</returns>
    /// <exception cref="FileNotFoundException">File does not exist</exception>
    /// <exception cref="InvalidDataException">File format is invalid</exception>
    public static MapData LoadFromBinary(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Map file not found: {filePath}");

        using var reader = new BinaryReader(File.OpenRead(filePath));

        // Read header
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        float heightMin = reader.ReadSingle();
        float heightMax = reader.ReadSingle();

        if (width <= 0 || height <= 0)
            throw new InvalidDataException($"Invalid map dimensions: {width}x{height}");

        // Read tile data
        var tiles = new TileData[width * height];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i].Height = reader.ReadSingle();
            tiles[i].Flags = reader.ReadByte();
        }

        return new MapData
        {
            Width = width,
            Height = height,
            Tiles = tiles,
            HeightMin = heightMin,
            HeightMax = heightMax
        };
    }

    /// <summary>
    /// Save map data to binary file.
    /// This enables external map editor tools.
    /// </summary>
    /// <param name="filePath">Output path for .bin file</param>
    /// <param name="mapData">Map data to save</param>
    /// <exception cref="InvalidOperationException">Map data is invalid</exception>
    public static void SaveToBinary(string filePath, MapData mapData)
    {
        if (!mapData.IsValid)
            throw new InvalidOperationException("Cannot save invalid map data");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var writer = new BinaryWriter(File.Create(filePath));

        // Write header
        writer.Write(mapData.Width);
        writer.Write(mapData.Height);
        writer.Write(mapData.HeightMin);
        writer.Write(mapData.HeightMax);

        // Write tile data
        foreach (var tile in mapData.Tiles)
        {
            writer.Write(tile.Height);
            writer.Write(tile.Flags);
        }
    }

    /// <summary>
    /// Load map from PNG heightmap (grayscale values = elevation).
    /// Useful for importing terrain from external height map generators.
    /// </summary>
    /// <param name="pngPath">Path to grayscale PNG file</param>
    /// <param name="minHeight">Height value for black (0.0)</param>
    /// <param name="maxHeight">Height value for white (1.0)</param>
    /// <returns>Map data with heights interpolated from grayscale</returns>
    public static MapData LoadFromHeightmap(string pngPath, float minHeight, float maxHeight)
    {
        if (!File.Exists(pngPath))
            throw new FileNotFoundException($"Heightmap not found: {pngPath}");

        // Load PNG into texture
        var bytes = File.ReadAllBytes(pngPath);
        var texture = new Texture2D(2, 2);
        texture.hideFlags = HideFlags.HideAndDontSave;

        if (!ImageConversion.LoadImage(texture, bytes))
        {
            UnityEngine.Object.Destroy(texture);
            throw new InvalidDataException($"Failed to decode PNG: {pngPath}");
        }

        var pixels = texture.GetPixels();
        var mapData = new MapData
        {
            Width = texture.width,
            Height = texture.height,
            HeightMin = minHeight,
            HeightMax = maxHeight,
            Tiles = new TileData[texture.width * texture.height]
        };

        // Convert grayscale to height
        for (int i = 0; i < pixels.Length; i++)
        {
            float normalized = pixels[i].grayscale; // 0.0 to 1.0
            mapData.Tiles[i].Height = Mathf.Lerp(minHeight, maxHeight, normalized);
            mapData.Tiles[i].Flags = 0;
        }

        UnityEngine.Object.Destroy(texture);
        return mapData;
    }

    /// <summary>
    /// Export map heights to PNG heightmap (for visualization/editing in external tools).
    /// Heights are normalized to 0-255 grayscale.
    /// </summary>
    /// <param name="pngPath">Output PNG path</param>
    /// <param name="mapData">Map data to export</param>
    public static void SaveToHeightmap(string pngPath, MapData mapData)
    {
        if (!mapData.IsValid)
            throw new InvalidOperationException("Cannot export invalid map data");

        var texture = new Texture2D(mapData.Width, mapData.Height);
        texture.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color[mapData.Tiles.Length];
        float heightRange = mapData.HeightMax - mapData.HeightMin;
        if (heightRange < 0.01f) heightRange = 1f;

        for (int i = 0; i < mapData.Tiles.Length; i++)
        {
            float normalized = Mathf.Clamp01((mapData.Tiles[i].Height - mapData.HeightMin) / heightRange);
            pixels[i] = new Color(normalized, normalized, normalized, 1f);
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // Encode to PNG
        var pngBytes = texture.EncodeToPNG();
        File.WriteAllBytes(pngPath, pngBytes);

        UnityEngine.Object.Destroy(texture);
    }

    /// <summary>
    /// Load background texture from PNG file.
    /// Typically used for pre-rendered map backgrounds captured from the game.
    /// </summary>
    public static Texture2D LoadBackgroundTexture(string pngPath)
    {
        if (!File.Exists(pngPath))
            return null;

        var bytes = File.ReadAllBytes(pngPath);
        var texture = new Texture2D(2, 2);
        texture.hideFlags = HideFlags.HideAndDontSave;

        if (ImageConversion.LoadImage(texture, bytes))
            return texture;

        UnityEngine.Object.Destroy(texture);
        return null;
    }
}
