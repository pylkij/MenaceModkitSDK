using System;
using System.Collections.Generic;
using System.Text;

namespace Menace.SDK.Config;

/// <summary>
/// Lightweight JSON5 parser for config files.
/// Supports // and /* */ comments without requiring Newtonsoft.Json.
/// Provides simple key-value extraction without full deserialization.
/// </summary>
public static class JsonUtils
{
    /// <summary>
    /// Strip // and /* */ comments from JSON5 text.
    /// Preserves strings (escaped quotes are handled).
    /// </summary>
    /// <param name="input">Raw JSON5 text with comments</param>
    /// <returns>JSON text with comments removed</returns>
    /// <example>
    /// <code>
    /// var json = @"{
    ///     // This is a comment
    ///     ""key"": ""value"" /* block comment */
    /// }";
    /// var cleaned = JsonUtils.StripJsonComments(json);
    /// </code>
    /// </example>
    public static string StripJsonComments(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder(input.Length);
        int position = 0;

        while (position < input.Length)
        {
            // Handle strings (preserve everything inside quotes)
            if (input[position] == '"')
            {
                result.Append('"');
                position++;

                while (position < input.Length && input[position] != '"')
                {
                    // Handle escaped characters
                    if (input[position] == '\\' && position + 1 < input.Length)
                    {
                        result.Append(input[position]);
                        result.Append(input[position + 1]);
                        position += 2;
                    }
                    else
                    {
                        result.Append(input[position]);
                        position++;
                    }
                }

                if (position < input.Length)
                {
                    result.Append('"');
                    position++;
                }
            }
            // Handle // line comments
            else if (position + 1 < input.Length && input[position] == '/' && input[position + 1] == '/')
            {
                while (position < input.Length && input[position] != '\n')
                    position++;
            }
            // Handle /* block comments */
            else if (position + 1 < input.Length && input[position] == '/' && input[position + 1] == '*')
            {
                position += 2;
                while (position + 1 < input.Length && !(input[position] == '*' && input[position + 1] == '/'))
                    position++;
                if (position + 1 < input.Length)
                    position += 2;
            }
            // Regular character
            else
            {
                result.Append(input[position]);
                position++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Read a string value by key from JSON.
    /// Returns null if key not found or value is not a string.
    /// </summary>
    /// <param name="json">JSON text</param>
    /// <param name="key">Key to search for</param>
    /// <returns>String value or null</returns>
    public static string ReadString(string json, string key)
    {
        var needle = $"\"{key}\"";
        int keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0) return null;

        int colonIndex = json.IndexOf(':', keyIndex + needle.Length);
        if (colonIndex < 0) return null;

        int openQuote = json.IndexOf('"', colonIndex + 1);
        if (openQuote < 0) return null;

        int closeQuote = json.IndexOf('"', openQuote + 1);
        if (closeQuote < 0) return null;

        return json.Substring(openQuote + 1, closeQuote - openQuote - 1);
    }

    /// <summary>
    /// Read a boolean value by key from JSON.
    /// </summary>
    /// <param name="json">JSON text</param>
    /// <param name="key">Key to search for</param>
    /// <param name="fallback">Value to return if key not found</param>
    /// <returns>Boolean value or fallback</returns>
    public static bool ReadBool(string json, string key, bool fallback = false)
    {
        var needle = $"\"{key}\"";
        int keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0) return fallback;

        int colonIndex = json.IndexOf(':', keyIndex + needle.Length);
        if (colonIndex < 0) return fallback;

        var valueText = json.Substring(colonIndex + 1).TrimStart();
        if (valueText.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (valueText.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;

        return fallback;
    }

    /// <summary>
    /// Read an integer value by key from JSON.
    /// </summary>
    /// <param name="json">JSON text</param>
    /// <param name="key">Key to search for</param>
    /// <param name="fallback">Value to return if key not found or parse fails</param>
    /// <returns>Integer value or fallback</returns>
    public static int ReadInt(string json, string key, int fallback = 0)
    {
        var needle = $"\"{key}\"";
        int keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0) return fallback;

        int colonIndex = json.IndexOf(':', keyIndex + needle.Length);
        if (colonIndex < 0) return fallback;

        var valueText = json.Substring(colonIndex + 1).TrimStart();
        int numberEnd = 0;

        while (numberEnd < valueText.Length && (char.IsDigit(valueText[numberEnd]) || valueText[numberEnd] == '-'))
            numberEnd++;

        if (numberEnd == 0) return fallback;

        return int.TryParse(valueText.Substring(0, numberEnd), out var parsedValue) ? parsedValue : fallback;
    }

    /// <summary>
    /// Read a float value by key from JSON.
    /// </summary>
    /// <param name="json">JSON text</param>
    /// <param name="key">Key to search for</param>
    /// <param name="fallback">Value to return if key not found or parse fails</param>
    /// <returns>Float value or fallback</returns>
    public static float ReadFloat(string json, string key, float fallback = 0f)
    {
        var needle = $"\"{key}\"";
        int keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0) return fallback;

        int colonIndex = json.IndexOf(':', keyIndex + needle.Length);
        if (colonIndex < 0) return fallback;

        var valueText = json.Substring(colonIndex + 1).TrimStart();
        int numberEnd = 0;

        while (numberEnd < valueText.Length &&
               (char.IsDigit(valueText[numberEnd]) || valueText[numberEnd] == '-' || valueText[numberEnd] == '.'))
            numberEnd++;

        if (numberEnd == 0) return fallback;

        return float.TryParse(valueText.Substring(0, numberEnd), out var parsedValue) ? parsedValue : fallback;
    }

    /// <summary>
    /// Read a JSON object by key and return it as a string.
    /// Handles nested objects correctly.
    /// </summary>
    /// <param name="json">JSON text</param>
    /// <param name="key">Key to search for</param>
    /// <returns>JSON object as string or null</returns>
    public static string ReadObject(string json, string key)
    {
        var needle = $"\"{key}\"";
        int keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0) return null;

        int braceStart = json.IndexOf('{', keyIndex + needle.Length);
        if (braceStart < 0) return null;

        int braceDepth = 0;
        for (int position = braceStart; position < json.Length; position++)
        {
            // Skip strings
            if (json[position] == '"')
            {
                position++;
                while (position < json.Length && json[position] != '"')
                {
                    if (json[position] == '\\') position++;
                    position++;
                }
            }
            else if (json[position] == '{')
            {
                braceDepth++;
            }
            else if (json[position] == '}')
            {
                braceDepth--;
                if (braceDepth == 0)
                    return json.Substring(braceStart, position - braceStart + 1);
            }
        }

        return null;
    }

    /// <summary>
    /// Read a JSON array by key and return it as a string.
    /// Handles nested arrays correctly.
    /// </summary>
    /// <param name="json">JSON text</param>
    /// <param name="key">Key to search for</param>
    /// <returns>JSON array as string or null</returns>
    public static string ReadArray(string json, string key)
    {
        var needle = $"\"{key}\"";
        int keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0) return null;

        int bracketStart = json.IndexOf('[', keyIndex + needle.Length);
        if (bracketStart < 0) return null;

        int bracketDepth = 0;
        for (int position = bracketStart; position < json.Length; position++)
        {
            // Skip strings
            if (json[position] == '"')
            {
                position++;
                while (position < json.Length && json[position] != '"')
                {
                    if (json[position] == '\\') position++;
                    position++;
                }
            }
            else if (json[position] == '[')
            {
                bracketDepth++;
            }
            else if (json[position] == ']')
            {
                bracketDepth--;
                if (bracketDepth == 0)
                    return json.Substring(bracketStart, position - bracketStart + 1);
            }
        }

        return null;
    }

    /// <summary>
    /// Split a JSON array string into individual object elements.
    /// Only splits top-level objects, handles nested structures correctly.
    /// </summary>
    /// <param name="json">JSON array text</param>
    /// <returns>List of JSON object strings</returns>
    public static List<string> SplitJsonArray(string json)
    {
        var elements = new List<string>();
        int arrayStart = json.IndexOf('[');
        if (arrayStart < 0) return elements;

        int braceDepth = 0;
        int objectStart = -1;

        for (int position = arrayStart + 1; position < json.Length; position++)
        {
            // Skip strings
            if (json[position] == '"')
            {
                position++;
                while (position < json.Length && json[position] != '"')
                {
                    if (json[position] == '\\') position++;
                    position++;
                }
            }
            else if (json[position] == '{')
            {
                if (braceDepth == 0) objectStart = position;
                braceDepth++;
            }
            else if (json[position] == '}')
            {
                braceDepth--;
                if (braceDepth == 0 && objectStart >= 0)
                {
                    elements.Add(json.Substring(objectStart, position - objectStart + 1));
                    objectStart = -1;
                }
            }
            else if (json[position] == ']' && braceDepth == 0)
            {
                break;
            }
        }

        return elements;
    }

    /// <summary>
    /// Extract all "key":"value" string pairs from a flat JSON object.
    /// Only returns string values, skips nested objects and arrays.
    /// </summary>
    /// <param name="json">JSON object text</param>
    /// <returns>List of key-value pairs</returns>
    public static List<KeyValuePair<string, string>> ReadAllStringPairs(string json)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        int position = json.IndexOf('{');
        if (position < 0) return pairs;
        position++;

        while (position < json.Length)
        {
            // Find key
            int keyOpenQuote = json.IndexOf('"', position);
            if (keyOpenQuote < 0) break;

            int keyCloseQuote = json.IndexOf('"', keyOpenQuote + 1);
            if (keyCloseQuote < 0) break;

            var key = json.Substring(keyOpenQuote + 1, keyCloseQuote - keyOpenQuote - 1);

            // Find colon
            int colonIndex = json.IndexOf(':', keyCloseQuote + 1);
            if (colonIndex < 0) break;

            // Find value start
            int valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            // Only handle string values
            if (valueStart < json.Length && json[valueStart] == '"')
            {
                int valueCloseQuote = json.IndexOf('"', valueStart + 1);
                if (valueCloseQuote < 0) break;

                var value = json.Substring(valueStart + 1, valueCloseQuote - valueStart - 1);
                pairs.Add(new KeyValuePair<string, string>(key, value));
                position = valueCloseQuote + 1;
            }
            else
            {
                position = valueStart + 1;
            }
        }

        return pairs;
    }
}
