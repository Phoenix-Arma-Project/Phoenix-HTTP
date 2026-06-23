using System.Text;

namespace PhoenixHttp.Utils;

/// <summary>
/// Expands <c>{{name}}</c> macros against the configured environment values. A token with no matching
/// environment value is left untouched (braces and all), so an unresolved macro is visible rather
/// than silently blanked. The scan is a single left-to-right pass with no allocation when the input
/// contains no macro at all.
/// </summary>
public static class MacroProcessor
{
    /// <summary>Replaces every resolvable <c>{{name}}</c> token in the input with its value.</summary>
    /// <param name="input">The text to expand.</param>
    /// <param name="environments">Macro name to replacement value lookup.</param>
    /// <returns>The expanded text; the original instance when it contains no macro.</returns>
    public static string Apply(string input, IReadOnlyDictionary<string, string> environments)
    {
        int open = input.IndexOf("{{", StringComparison.Ordinal);
        if (open < 0)
        {
            return input;
        }

        StringBuilder builder = new();
        int position = 0;

        while (open >= 0)
        {
            int close = input.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }

            string token = input[(open + 2)..close];
            builder.Append(input, position, open - position);

            if (environments.TryGetValue(token, out string? value))
            {
                builder.Append(value);
            }
            else
            {
                // Unknown macro: keep the original text, braces included, so it is not lost.
                builder.Append(input, open, close + 2 - open);
            }

            position = close + 2;
            open = input.IndexOf("{{", position, StringComparison.Ordinal);
        }

        builder.Append(input, position, input.Length - position);
        return builder.ToString();
    }
}
