namespace Menace.SDK.Maps;

/// <summary>
/// Defines a visual overlay layer linked to TileData flag bits.
/// Used for rendering features like roads, vegetation, structures, etc.
/// </summary>
public readonly struct OverlayDefinition
{
    /// <summary>Display name of this overlay layer.</summary>
    public string Name { get; init; }

    /// <summary>
    /// TileData.Flags bitmask for this overlay.
    /// When a tile has this bit set, the overlay is rendered.
    /// </summary>
    public byte FlagMask { get; init; }

    /// <summary>
    /// Optional path to tile texture for rendering this overlay.
    /// Null for flat-color rendering.
    /// </summary>
    public string TileTexturePath { get; init; }

    public OverlayDefinition(string name, byte flagMask, string tileTexturePath = null)
    {
        Name = name;
        FlagMask = flagMask;
        TileTexturePath = tileTexturePath;
    }
}
