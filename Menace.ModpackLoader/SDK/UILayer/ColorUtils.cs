using System;
using UnityEngine;

namespace Menace.SDK.UI;

/// <summary>
/// Utilities for parsing and converting colors between hex strings and Unity Color.
/// </summary>
public static class ColorUtils
{
    /// <summary>
    /// Parse a hex color string to Unity Color.
    /// Supports formats: #RGB, #RRGGBB, #RRGGBBAA
    /// </summary>
    /// <param name="hex">Hex color string (with or without # prefix)</param>
    /// <returns>Parsed Unity Color</returns>
    /// <exception cref="ArgumentException">Invalid hex color format</exception>
    /// <example>
    /// <code>
    /// var color1 = ColorUtils.ParseHex("#FF0000");      // Red
    /// var color2 = ColorUtils.ParseHex("#00FF00FF");    // Green with alpha
    /// var color3 = ColorUtils.ParseHex("0000FF");       // Blue (no # prefix)
    /// </code>
    /// </example>
    public static Color ParseHex(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            throw new ArgumentException("Hex color string cannot be null or empty");

        // Remove # prefix if present
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        // Expand shorthand #RGB to #RRGGBB
        if (hex.Length == 3)
        {
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        }

        if (hex.Length != 6 && hex.Length != 8)
            throw new ArgumentException($"Invalid hex color format: #{hex}. Expected #RRGGBB or #RRGGBBAA");

        byte red = ParseHexByte(hex, 0);
        byte green = ParseHexByte(hex, 2);
        byte blue = ParseHexByte(hex, 4);
        byte alpha = hex.Length >= 8 ? ParseHexByte(hex, 6) : (byte)255;

        return new Color(red / 255f, green / 255f, blue / 255f, alpha / 255f);
    }

    /// <summary>
    /// Try to parse a hex color string. Returns false if parsing fails.
    /// </summary>
    /// <param name="hex">Hex color string</param>
    /// <param name="color">Parsed color (or default if parsing fails)</param>
    /// <returns>True if parsing succeeded</returns>
    public static bool TryParseHex(string hex, out Color color)
    {
        try
        {
            color = ParseHex(hex);
            return true;
        }
        catch
        {
            color = default;
            return false;
        }
    }

    /// <summary>
    /// Convert Unity Color to hex string.
    /// </summary>
    /// <param name="color">Unity Color</param>
    /// <param name="includeAlpha">Include alpha channel in output</param>
    /// <returns>Hex color string with # prefix</returns>
    /// <example>
    /// <code>
    /// var hex1 = ColorUtils.ToHex(Color.red);              // "#FF0000"
    /// var hex2 = ColorUtils.ToHex(Color.green, true);      // "#00FF00FF"
    /// </code>
    /// </example>
    public static string ToHex(Color color, bool includeAlpha = false)
    {
        byte r = (byte)(Mathf.Clamp01(color.r) * 255);
        byte g = (byte)(Mathf.Clamp01(color.g) * 255);
        byte b = (byte)(Mathf.Clamp01(color.b) * 255);

        if (includeAlpha)
        {
            byte a = (byte)(Mathf.Clamp01(color.a) * 255);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static int HexVal(char hexChar) =>
        hexChar >= '0' && hexChar <= '9' ? hexChar - '0' :
        hexChar >= 'a' && hexChar <= 'f' ? hexChar - 'a' + 10 :
        hexChar >= 'A' && hexChar <= 'F' ? hexChar - 'A' + 10 : 0;

    private static byte ParseHexByte(string hexString, int offset)
    {
        if (offset + 1 >= hexString.Length)
            throw new ArgumentException($"Invalid hex byte at offset {offset}");

        return (byte)(HexVal(hexString[offset]) * 16 + HexVal(hexString[offset + 1]));
    }
}
