using PhoenixHttp.Arma;
using Xunit;

namespace PhoenixHttp.Tests.Arma;

/// <summary>
/// Locks in the SQF serialization contract, in particular the deliberate asymmetry between objects
/// (flattened into dotted keys) and arrays (kept as iterable arrays). These tests exist so the shape
/// is not "tidied up" into something that breaks the SQF-side ergonomics it was designed for.
/// </summary>
public class SerializerTests
{
    [Fact]
    public void FlatMap_WritesCreateHashMapFromArray()
    {
        Dictionary<string, object?> input = new() { ["key"] = "value" };

        Assert.Equal("createHashMapFromArray [[\"key\",\"value\"]]", Serializer.Serialize(input));
    }

    [Fact]
    public void NestedObject_IsFlattenedIntoDottedKey()
    {
        Dictionary<string, object?> input = new()
        {
            ["body"] = new Dictionary<string, object?> { ["user"] = new Dictionary<string, object?> { ["name"] = "x" } }
        };

        Assert.Equal("createHashMapFromArray [[\"body.user.name\",\"x\"]]", Serializer.Serialize(input));
    }

    [Fact]
    public void ArrayOfScalars_IsKeptAsArray()
    {
        Dictionary<string, object?> input = new() { ["items"] = new List<object?> { 1L, 2L, 3L } };

        Assert.Equal("createHashMapFromArray [[\"items\",[1,2,3]]]", Serializer.Serialize(input));
    }

    [Fact]
    public void ArrayOfObjects_KeepsArrayAndNestsEachObject()
    {
        // The contract: arrays stay arrays (so SQF can forEach them) and each object element becomes
        // its own locally-flattened hashmap, rather than dissolving into "items.0.name" string keys.
        Dictionary<string, object?> input = new()
        {
            ["items"] = new List<object?>
            {
                new Dictionary<string, object?> { ["name"] = "a" },
                new Dictionary<string, object?> { ["name"] = "b" }
            }
        };

        Assert.Equal(
            "createHashMapFromArray [[\"items\",[createHashMapFromArray [[\"name\",\"a\"]],createHashMapFromArray [[\"name\",\"b\"]]]]]",
            Serializer.Serialize(input));
    }

    [Fact]
    public void StringWithQuotes_DoublesThem()
    {
        Dictionary<string, object?> input = new() { ["k"] = "he said \"hi\"" };

        Assert.Equal("createHashMapFromArray [[\"k\",\"he said \"\"hi\"\"\"]]", Serializer.Serialize(input));
    }

    [Fact]
    public void NullValue_IsDropped()
    {
        Dictionary<string, object?> input = new() { ["missing"] = null };

        Assert.Equal("createHashMapFromArray []", Serializer.Serialize(input));
    }

    [Theory]
    [InlineData(true, "createHashMapFromArray [[\"v\",true]]")]
    [InlineData(false, "createHashMapFromArray [[\"v\",false]]")]
    public void Boolean_WritesSqfLiteral(bool value, string expected)
    {
        Dictionary<string, object?> input = new() { ["v"] = value };

        Assert.Equal(expected, Serializer.Serialize(input));
    }

    [Fact]
    public void Double_UsesInvariantCulture()
    {
        Dictionary<string, object?> input = new() { ["pi"] = 3.5 };

        Assert.Equal("createHashMapFromArray [[\"pi\",3.5]]", Serializer.Serialize(input));
    }
}
