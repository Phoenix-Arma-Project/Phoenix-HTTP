using System.Text;
using PhoenixHttp.Utils;
using Xunit;

namespace PhoenixHttp.Tests.Utils;

/// <summary>
/// Verifies that response chunking respects the byte budget and never splits a multi-byte UTF-8
/// sequence, so chunks are individually valid and rejoin into exactly the original string.
/// </summary>
public class ChunkerTests
{
    [Fact]
    public void EmptyString_ProducesNoChunks()
    {
        Assert.Empty(Chunker.Split(string.Empty, 10));
    }

    [Theory]
    [InlineData(25, 10, 3)]
    [InlineData(20, 10, 2)]
    [InlineData(5, 10, 1)]
    public void Split_HonorsBudgetAndRejoinsToOriginal(int length, int maxBytes, int expectedChunks)
    {
        string value = new string('a', length);

        IReadOnlyList<string> chunks = Chunker.Split(value, maxBytes);

        Assert.Equal(expectedChunks, chunks.Count);
        Assert.All(chunks, chunk => Assert.True(Encoding.UTF8.GetByteCount(chunk) <= maxBytes));
        Assert.Equal(value, string.Concat(chunks));
    }

    [Fact]
    public void NeverBreaksMultiByteRune()
    {
        // "😀" is four UTF-8 bytes; with a four-byte budget each emoji must land in its own chunk
        // whole, never half in one chunk and half in the next.
        string value = string.Concat(Enumerable.Repeat("😀", 3));

        IReadOnlyList<string> chunks = Chunker.Split(value, 4);

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal("😀", chunk));
        Assert.Equal(value, string.Concat(chunks));
    }
}
