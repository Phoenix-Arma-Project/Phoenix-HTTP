using System.Text.Json;

namespace PhoenixHttp.Utils;

/// <summary>
/// Parses a JSON document into a plain object graph (<see cref="Dictionary{TKey, TValue}"/>,
/// <see cref="List{T}"/>, <see cref="string"/>, <see cref="long"/>/<see cref="double"/>,
/// <see cref="bool"/> and null) that the <see cref="Arma.Serializer"/> can render for SQF. Numbers
/// are read as <see cref="long"/> when they fit and as <see cref="double"/> otherwise, so integer
/// values survive the round trip without being widened to floating point.
/// </summary>
public static class JsonParser
{
    /// <summary>Parses JSON text into an object graph.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The parsed graph.</returns>
    public static object? Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Convert(document.RootElement);
    }

    /// <summary>Converts a single JSON element to its object-graph equivalent.</summary>
    /// <param name="element">The element to convert.</param>
    /// <returns>The converted value.</returns>
    private static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ConvertObject(element),
        JsonValueKind.Array => ConvertArray(element),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out long whole) ? whole : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    /// <summary>Converts a JSON object into a string-keyed map.</summary>
    /// <param name="element">The object element.</param>
    /// <returns>The converted map.</returns>
    private static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        Dictionary<string, object?> result = new();
        foreach (JsonProperty property in element.EnumerateObject())
        {
            result[property.Name] = Convert(property.Value);
        }

        return result;
    }

    /// <summary>Converts a JSON array into a list.</summary>
    /// <param name="element">The array element.</param>
    /// <returns>The converted list.</returns>
    private static List<object?> ConvertArray(JsonElement element)
    {
        List<object?> result = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            result.Add(Convert(item));
        }

        return result;
    }
}
