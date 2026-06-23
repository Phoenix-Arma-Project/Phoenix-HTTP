using System.Globalization;
using System.Text;

namespace PhoenixHttp.Arma;

/// <summary>
/// Reads the SQF body format produced by the wrapper's <c>Serialize</c> function back into a plain
/// object graph that <see cref="Utils.JsonWriter"/> can turn into JSON. SQF has no JSON and no way to
/// tell a hashmap apart from a plain array once both are written as nested arrays, so the wrapper
/// tags maps as <c>["__MAP__", pairs]</c> and nulls as <c>"__NULL__"</c>; this class undoes that
/// tagging. The text grammar is a small subset of SQF literals: arrays, double-quoted strings (with
/// doubled quotes), booleans and numbers.
/// </summary>
public static class Deserializer
{
    /// <summary>Marker the wrapper wraps a hashmap's pair list in to distinguish it from an array.</summary>
    private const string MapMarker = "__MAP__";

    /// <summary>Marker the wrapper emits for a null value, which a bare SQF array cannot represent.</summary>
    private const string NullMarker = "__NULL__";

    /// <summary>Parses a serialized body and converts its top-level map into an object graph.</summary>
    /// <param name="body">The serialized SQF body text.</param>
    /// <returns>The decoded map, or an empty map if the top level was not a pair list.</returns>
    public static Dictionary<string, object?> Deserialize(string body)
    {
        object? raw = new Parser(body).ParseValue();
        return raw is List<object?> pairs ? ConvertMapPairs(pairs) : new Dictionary<string, object?>();
    }

    /// <summary>Converts a parsed node, resolving the map and null markers into real values.</summary>
    /// <param name="node">The raw parsed node.</param>
    /// <returns>The node with markers resolved: maps, arrays, strings, nulls or scalars.</returns>
    private static object? ConvertValue(object? node)
    {
        switch (node)
        {
            case List<object?> list:
                if (list.Count == 2 && list[0] is MapMarker)
                {
                    return ConvertMapPairs(list[1] as List<object?> ?? new List<object?>());
                }

                List<object?> array = new(list.Count);
                foreach (object? item in list)
                {
                    array.Add(ConvertValue(item));
                }

                return array;

            case string text:
                return text == NullMarker ? null : text;

            default:
                return node;
        }
    }

    /// <summary>Builds a map from a list of <c>[key, value]</c> pairs, converting each value.</summary>
    /// <param name="pairs">The raw pair list.</param>
    /// <returns>The assembled map.</returns>
    private static Dictionary<string, object?> ConvertMapPairs(List<object?> pairs)
    {
        Dictionary<string, object?> map = new();
        foreach (object? pair in pairs)
        {
            if (pair is List<object?> { Count: >= 2 } entry && entry[0] is string key)
            {
                map[key] = ConvertValue(entry[1]);
            }
        }

        return map;
    }

    /// <summary>
    /// A small recursive-descent reader over the serialized SQF literal grammar. It tracks a single
    /// position into the text and produces nested <see cref="List{T}"/>, <see cref="string"/>,
    /// <see cref="bool"/> and numeric nodes for <see cref="ConvertValue"/> to interpret.
    /// </summary>
    private sealed class Parser
    {
        /// <summary>The text being parsed.</summary>
        private readonly string text;

        /// <summary>Current read offset into <see cref="text"/>.</summary>
        private int position;

        /// <summary>Creates a parser positioned at the start of the text.</summary>
        /// <param name="text">The serialized body to parse.</param>
        public Parser(string text)
        {
            this.text = text;
        }

        /// <summary>Parses one value, dispatching on the next non-space character.</summary>
        /// <returns>The parsed node.</returns>
        public object? ParseValue()
        {
            SkipWhitespace();
            char current = Peek();
            return current switch
            {
                '[' => ParseArray(),
                '"' => ParseString(),
                't' or 'f' => ParseBoolean(),
                _ => ParseNumber()
            };
        }

        /// <summary>Parses a comma-separated, bracket-delimited array.</summary>
        /// <returns>The parsed items.</returns>
        private List<object?> ParseArray()
        {
            position++;
            List<object?> items = new();

            SkipWhitespace();
            if (Peek() == ']')
            {
                position++;
                return items;
            }

            while (true)
            {
                items.Add(ParseValue());
                SkipWhitespace();

                char separator = Next();
                if (separator == ']')
                {
                    return items;
                }

                if (separator != ',')
                {
                    throw new FormatException($"Expected ',' or ']' at position {position}.");
                }
            }
        }

        /// <summary>Parses a double-quoted string, collapsing each doubled quote into one.</summary>
        /// <returns>The decoded string.</returns>
        private string ParseString()
        {
            position++;
            StringBuilder builder = new();

            while (true)
            {
                char current = Next();
                if (current == '"')
                {
                    if (Peek() == '"')
                    {
                        builder.Append('"');
                        position++;
                        continue;
                    }

                    return builder.ToString();
                }

                builder.Append(current);
            }
        }

        /// <summary>Parses a <c>true</c> or <c>false</c> literal.</summary>
        /// <returns>The boolean value.</returns>
        private bool ParseBoolean()
        {
            if (Match("true"))
            {
                return true;
            }

            if (Match("false"))
            {
                return false;
            }

            throw new FormatException($"Expected a boolean at position {position}.");
        }

        /// <summary>Parses a number, returning a <see cref="long"/> when integral and a <see cref="double"/> otherwise.</summary>
        /// <returns>The numeric value.</returns>
        private object ParseNumber()
        {
            int start = position;
            if (Peek() == '-')
            {
                position++;
            }

            while (position < text.Length && (char.IsDigit(text[position]) || text[position] is '.' or 'e' or 'E' or '+' or '-'))
            {
                position++;
            }

            string token = text[start..position];
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole))
            {
                return whole;
            }

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
            {
                return real;
            }

            throw new FormatException($"Expected a number at position {start}.");
        }

        /// <summary>Returns the current character without consuming it, or NUL at end of input.</summary>
        /// <returns>The current character, or <c>'\0'</c> past the end.</returns>
        private char Peek() => position < text.Length ? text[position] : '\0';

        /// <summary>Consumes and returns the current character.</summary>
        /// <returns>The consumed character.</returns>
        /// <exception cref="FormatException">Thrown at end of input.</exception>
        private char Next()
        {
            if (position >= text.Length)
            {
                throw new FormatException("Unexpected end of input.");
            }

            return text[position++];
        }

        /// <summary>Advances past any run of whitespace.</summary>
        private void SkipWhitespace()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
        }

        /// <summary>Consumes the keyword if it appears at the current position.</summary>
        /// <param name="keyword">The keyword to match.</param>
        /// <returns><see langword="true"/> and advances on a match; otherwise <see langword="false"/>.</returns>
        private bool Match(string keyword)
        {
            if (position + keyword.Length > text.Length || text.AsSpan(position, keyword.Length).ToString() != keyword)
            {
                return false;
            }

            position += keyword.Length;
            return true;
        }
    }
}
