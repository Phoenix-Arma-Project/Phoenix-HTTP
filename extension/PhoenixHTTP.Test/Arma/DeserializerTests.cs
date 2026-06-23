using PhoenixHttp.Arma;
using Xunit;

namespace PhoenixHttp.Tests.Arma;

/// <summary>
/// Covers the request-body format: the pair arrays the SQF wrapper produces, including the
/// <c>__MAP__</c> and <c>__NULL__</c> markers that let a bare SQF array express maps and nulls.
/// </summary>
public class DeserializerTests
{
    [Fact]
    public void PairList_BecomesMap()
    {
        Dictionary<string, object?> result = Deserializer.Deserialize("[[\"name\",\"x\"]]");

        Assert.Equal("x", Assert.Contains("name", result));
    }

    [Fact]
    public void MapMarker_BecomesNestedMap()
    {
        Dictionary<string, object?> result = Deserializer.Deserialize("[[\"user\",[\"__MAP__\",[[\"name\",\"x\"]]]]]");

        Dictionary<string, object?> user = Assert.IsType<Dictionary<string, object?>>(Assert.Contains("user", result));
        Assert.Equal("x", Assert.Contains("name", user));
    }

    [Fact]
    public void NullMarker_BecomesNull()
    {
        Dictionary<string, object?> result = Deserializer.Deserialize("[[\"value\",\"__NULL__\"]]");

        Assert.Null(Assert.Contains("value", result));
    }

    [Fact]
    public void IntegerToken_BecomesLong()
    {
        Dictionary<string, object?> result = Deserializer.Deserialize("[[\"age\",30]]");

        Assert.Equal(30L, Assert.Contains("age", result));
    }

    [Fact]
    public void DoubledQuotesInString_BecomeOne()
    {
        Dictionary<string, object?> result = Deserializer.Deserialize("[[\"text\",\"a\"\"b\"]]");

        Assert.Equal("a\"b", Assert.Contains("text", result));
    }

    [Fact]
    public void PlainArray_StaysArray()
    {
        Dictionary<string, object?> result = Deserializer.Deserialize("[[\"nums\",[1,2,3]]]");

        List<object?> nums = Assert.IsType<List<object?>>(Assert.Contains("nums", result));
        Assert.Equal(new object?[] { 1L, 2L, 3L }, nums);
    }
}
