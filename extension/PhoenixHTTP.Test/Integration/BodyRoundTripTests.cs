using PhoenixHttp.Arma;
using PhoenixHttp.Utils;
using Xunit;

namespace PhoenixHttp.Tests.Integration;

/// <summary>
/// Exercises the data paths end to end across the serialization boundary: a SQF hashmap body becomes
/// the JSON actually sent, and a server JSON response becomes the SQF the wrapper compiles.
/// </summary>
public class BodyRoundTripTests
{
    [Fact]
    public void RequestBody_PairFormat_BecomesJsonObject()
    {
        string json = JsonWriter.Write(Deserializer.Deserialize("[[\"name\",\"x\"],[\"age\",30]]"));

        Assert.Equal("{\"name\":\"x\",\"age\":30}", json);
    }

    [Fact]
    public void RequestBody_NestedMapMarker_BecomesNestedJson()
    {
        string json = JsonWriter.Write(Deserializer.Deserialize("[[\"user\",[\"__MAP__\",[[\"name\",\"x\"]]]]]"));

        Assert.Equal("{\"user\":{\"name\":\"x\"}}", json);
    }

    [Fact]
    public void RequestBody_NullMarker_BecomesJsonNull()
    {
        string json = JsonWriter.Write(Deserializer.Deserialize("[[\"value\",\"__NULL__\"]]"));

        Assert.Equal("{\"value\":null}", json);
    }

    [Fact]
    public void Response_Json_ParsesIntoSerializableSqf()
    {
        object? graph = JsonParser.Parse("{\"ok\":true,\"count\":2}");
        Dictionary<string, object?> wrapped = new() { ["body"] = graph };

        string sqf = Serializer.Serialize(wrapped);

        Assert.Contains("[\"body.ok\",true]", sqf);
        Assert.Contains("[\"body.count\",2]", sqf);
    }
}
