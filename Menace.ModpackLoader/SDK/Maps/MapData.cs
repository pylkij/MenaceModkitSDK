using UnityEngine;

namespace Menace.SDK.Maps;

/// <summary>
/// Complete tactical map data including dimensions, tiles, height range, and optional background texture.
/// </summary>
public struct MapData
{
    /// <summary>Map width in tiles.</summary>
    public int Width;

    /// <summary>Map height in tiles.</summary>
    public int Height;

    /// <summary>
    /// Tile data array (row-major order: Tiles[z * Width + x]).
    /// Length must equal Width * Height.
    /// </summary>
    public TileData[] Tiles;

    /// <summary>Minimum height value across all tiles (for normalization).</summary>
    public float HeightMin;

    /// <summary>Maximum height value across all tiles (for normalization).</summary>
    public float HeightMax;

    /// <summary>Optional background texture for rendering (loaded from PNG or generated).</summary>
    public Texture2D BackgroundTexture;

    /// <summary>Get tile at coordinates (x, z). No bounds checking.</summary>
    public TileData GetTile(int x, int z) => Tiles[z * Width + x];

    /// <summary>Set tile at coordinates (x, z). No bounds checking.</summary>
    public void SetTile(int x, int z, TileData tile) => Tiles[z * Width + x] = tile;

    /// <summary>Try to get tile at coordinates. Returns false if out of bounds.</summary>
    public bool TryGetTile(int x, int z, out TileData tile)
    {
        if (x >= 0 && x < Width && z >= 0 && z < Height)
        {
            tile = Tiles[z * Width + x];
            return true;
        }
        tile = default;
        return false;
    }

    /// <summary>Check if coordinates are within map bounds.</summary>
    public bool IsInBounds(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height;

    /// <summary>Get total tile count.</summary>
    public int TileCount => Width * Height;

    /// <summary>Check if map data is valid (non-null tiles array with correct size).</summary>
    public bool IsValid => Tiles != null && Tiles.Length == Width * Height && Width > 0 && Height > 0;
}
