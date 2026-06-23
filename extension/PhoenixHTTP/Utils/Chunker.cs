using System.Text;

namespace PhoenixHttp.Utils;

/// <summary>
/// Splits a string into chunks no larger than a byte budget, so a response can be pulled back into
/// SQF in engine-sized pieces. Splitting is done on whole UTF-8 code points (runes), never inside a
/// multi-byte sequence, so each chunk is independently valid text and the pieces rejoin losslessly.
/// </summary>
public static class Chunker
{
    /// <summary>Splits a string into chunks each at most <paramref name="maxBytes"/> bytes of UTF-8.</summary>
    /// <param name="value">The string to split.</param>
    /// <param name="maxBytes">Maximum UTF-8 byte size of each chunk.</param>
    /// <returns>The chunks in order; empty when the input is empty.</returns>
    public static IReadOnlyList<string> Split(string value, int maxBytes)
    {
        List<string> chunks = [];
        if (value.Length == 0)
        {
            return chunks;
        }

        StringBuilder current = new();
        int currentBytes = 0;

        foreach (Rune rune in value.EnumerateRunes())
        {
            int runeBytes = rune.Utf8SequenceLength;
            if (currentBytes + runeBytes > maxBytes && current.Length > 0)
            {
                chunks.Add(current.ToString());
                current.Clear();
                currentBytes = 0;
            }

            current.Append(rune.ToString());
            currentBytes += runeBytes;
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }
}
