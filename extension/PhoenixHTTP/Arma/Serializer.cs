using System.Collections;
using System.Globalization;
using System.Text;

namespace PhoenixHttp.Arma;

/// <summary>
/// Serializes a response object into SQF source that the wrapper turns back into a hashmap with
/// <c>call compile</c>. The structure is generated here and every leaf value is escaped, so the
/// compiled text is data, not executable script, regardless of what the server returned.
/// <para>
/// The shape is deliberately asymmetric, to match how SQF is actually used:
/// nested <b>objects</b> are flattened into dotted keys (<c>"body.user.name"</c>) so a value can be
/// read with a single <c>get</c> instead of a chain of them; <b>arrays</b> are kept as real arrays
/// so they stay iterable with <c>forEach</c>, and each element is itself flattened locally. Flat
/// keys would otherwise dissolve arrays into <c>items.0.x</c> string keys that cannot be looped.
/// </para>
/// </summary>
public static class Serializer
{
    /// <summary>Serializes a response map into a SQF <c>createHashMapFromArray</c> expression.</summary>
    /// <param name="response">The response map to serialize.</param>
    /// <returns>SQF source that evaluates to the equivalent hashmap.</returns>
    public static string Serialize(IReadOnlyDictionary<string, object?> response)
    {
        Dictionary<string, object?> flat = new();
        FlattenInto(flat, string.Empty, response);
        return WriteHashMap(flat);
    }

    /// <summary>
    /// Walks an object graph and writes leaves into <paramref name="target"/> under dotted keys.
    /// Nested objects recurse and extend the key prefix; arrays are kept whole (and locally
    /// flattened) so they remain iterable; nulls are dropped because SQF has no null map value.
    /// </summary>
    /// <param name="target">The flat map being built.</param>
    /// <param name="prefix">The dotted key prefix accumulated so far.</param>
    /// <param name="node">The current node in the graph.</param>
    private static void FlattenInto(Dictionary<string, object?> target, string prefix, object? node)
    {
        if (node == null)
        {
            return;
        }

        if (node is IDictionary map)
        {
            foreach (DictionaryEntry entry in map)
            {
                FlattenInto(target, Combine(prefix, entry.Key?.ToString() ?? string.Empty), entry.Value);
            }
        }
        else if (node is IList array)
        {
            target[prefix] = FlattenArray(array);
        }
        else
        {
            target[prefix] = node;
        }
    }

    /// <summary>
    /// Flattens the elements of an array while keeping the array itself intact. Object elements are
    /// flattened into their own local map (so iteration yields a usable hashmap); nested arrays
    /// recurse; scalars pass through unchanged.
    /// </summary>
    /// <param name="array">The array to process.</param>
    /// <returns>An array of flattened elements.</returns>
    private static List<object?> FlattenArray(IList array)
    {
        List<object?> result = new();
        foreach (object? item in array)
        {
            if (item is IDictionary)
            {
                Dictionary<string, object?> nested = new();
                FlattenInto(nested, string.Empty, item);
                result.Add(nested);
            }
            else if (item is IList innerArray)
            {
                result.Add(FlattenArray(innerArray));
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>Joins a key onto a dotted prefix, or returns the key when the prefix is empty.</summary>
    /// <param name="prefix">The accumulated prefix.</param>
    /// <param name="key">The key to append.</param>
    /// <returns>The combined dotted key.</returns>
    private static string Combine(string prefix, string key) =>
        prefix.Length == 0 ? key : $"{prefix}.{key}";

    /// <summary>Writes one value as its SQF literal, dispatching on the runtime type.</summary>
    /// <param name="value">The value to write.</param>
    /// <returns>The SQF literal text.</returns>
    private static string WriteValue(object? value) => value switch
    {
        null => "objNull",
        string text => WriteString(text),
        bool flag => flag ? "true" : "false",
        Dictionary<string, object?> map => WriteHashMap(map),
        IList list => WriteArray(list),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => WriteString(value.ToString() ?? string.Empty)
    };

    /// <summary>Writes a map as a SQF <c>createHashMapFromArray</c> expression of key/value pairs.</summary>
    /// <param name="map">The map to write.</param>
    /// <returns>The SQF expression text.</returns>
    private static string WriteHashMap(Dictionary<string, object?> map)
    {
        StringBuilder builder = new("createHashMapFromArray [");
        bool first = true;
        foreach (KeyValuePair<string, object?> entry in map)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append('[').Append(WriteString(entry.Key)).Append(',').Append(WriteValue(entry.Value)).Append(']');
        }

        return builder.Append(']').ToString();
    }

    /// <summary>Writes a list as a SQF array literal.</summary>
    /// <param name="array">The list to write.</param>
    /// <returns>The SQF array text.</returns>
    private static string WriteArray(IList array)
    {
        StringBuilder builder = new("[");
        bool first = true;
        foreach (object? item in array)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(WriteValue(item));
        }

        return builder.Append(']').ToString();
    }

    /// <summary>
    /// Writes a SQF string literal, doubling embedded quotes. In SQF the quote is the only character
    /// special inside a string literal, so doubling it is complete escaping and prevents any
    /// server-supplied text from breaking out of the literal when the result is compiled.
    /// </summary>
    /// <param name="value">The raw string.</param>
    /// <returns>The quoted, escaped SQF string literal.</returns>
    private static string WriteString(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
