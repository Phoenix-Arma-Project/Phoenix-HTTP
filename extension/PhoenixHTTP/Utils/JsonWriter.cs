using System.Collections;
using System.Text;
using System.Text.Json;

namespace PhoenixHttp.Utils;

/// <summary>
/// Writes a plain object graph back out as JSON, used to turn a hashmap request body into the JSON
/// payload that is actually sent. It is the inverse of <see cref="JsonParser"/> and handles the same
/// value shapes: maps, lists, strings, integers, doubles, booleans and null. Any unrecognized type
/// falls back to its string form so the writer never throws on an unexpected value.
/// </summary>
public static class JsonWriter
{
    /// <summary>Serializes an object graph to a JSON string.</summary>
    /// <param name="value">The graph to serialize.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(object? value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            WriteValue(writer, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Writes one value, dispatching on its runtime type.</summary>
    /// <param name="writer">The JSON writer to emit into.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case long whole:
                writer.WriteNumberValue(whole);
                break;
            case double real:
                writer.WriteNumberValue(real);
                break;
            case IDictionary<string, object?> map:
                writer.WriteStartObject();
                foreach (KeyValuePair<string, object?> entry in map)
                {
                    writer.WritePropertyName(entry.Key);
                    WriteValue(writer, entry.Value);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable list:
                writer.WriteStartArray();
                foreach (object? item in list)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
