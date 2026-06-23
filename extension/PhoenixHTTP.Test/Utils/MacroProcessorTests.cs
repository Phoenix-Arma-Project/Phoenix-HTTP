using PhoenixHttp.Utils;
using Xunit;

namespace PhoenixHttp.Tests.Utils;

/// <summary>Covers <c>{{macro}}</c> expansion, including the choice to leave unknown macros intact.</summary>
public class MacroProcessorTests
{
    private static readonly Dictionary<string, string> Environment = new()
    {
        ["scheme"] = "https",
        ["host"] = "example.com"
    };

    [Theory]
    [InlineData("https://example.com", "https://example.com")]              // no macro: unchanged
    [InlineData("https://{{host}}/api", "https://example.com/api")]         // single known macro
    [InlineData("{{scheme}}://{{host}}", "https://example.com")]            // several known macros
    [InlineData("Bearer {{token}}", "Bearer {{token}}")]                    // unknown macro: left intact
    public void Apply_ExpandsKnownMacrosOnly(string input, string expected)
    {
        Assert.Equal(expected, MacroProcessor.Apply(input, Environment));
    }
}
