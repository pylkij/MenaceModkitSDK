namespace Menace.SDK.Maps;

/// <summary>
/// Represents per-tile data for tactical maps.
/// Contains height information and bitfield flags for terrain features.
/// </summary>
public struct TileData
{
    /// <summary>Height of this tile in world units.</summary>
    public float Height;

    /// <summary>
    /// Terrain feature flags (bitfield):
    /// <list type="bullet">
    /// <item>bit 0 (0x01): Road - pathable road surface</item>
    /// <item>bit 1 (0x02): Blocked - impassable terrain</item>
    /// <item>bit 2 (0x04): Vegetation - trees, bushes, etc.</item>
    /// <item>bit 3 (0x08): Structure - buildings, walls, etc.</item>
    /// <item>bits 4-7: Available for custom overlay layers</item>
    /// </list>
    /// </summary>
    public byte Flags;

    // Flag bit constants
    public const byte FLAG_ROAD = 0x01;
    public const byte FLAG_BLOCKED = 0x02;
    public const byte FLAG_VEGETATION = 0x04;
    public const byte FLAG_STRUCTURE = 0x08;

    /// <summary>Check if a specific flag bit is set.</summary>
    public bool HasFlag(byte flag) => (Flags & flag) != 0;

    /// <summary>Set a specific flag bit.</summary>
    public void SetFlag(byte flag) => Flags |= flag;

    /// <summary>Clear a specific flag bit.</summary>
    public void ClearFlag(byte flag) => Flags &= (byte)~flag;

    /// <summary>Toggle a specific flag bit.</summary>
    public void ToggleFlag(byte flag) => Flags ^= flag;
}
