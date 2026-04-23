using System.Text;

namespace Starter.Module.AI.Infrastructure.Retrieval;

public readonly record struct ArabicNormalizationOptions(
    bool NormalizeTaMarbuta,
    bool NormalizeArabicDigits);

public static class ArabicTextNormalizer
{
    public static string Normalize(string input, ArabicNormalizationOptions options)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        var lastWasWhitespace = false;

        foreach (var ch in input)
        {
            // Diacritics / harakat (including tatweel) — strip.
            if (IsArabicDiacritic(ch) || ch == '\u0640') continue;

            char mapped = ch;

            // Alef variants → bare alef
            if (ch is '\u0623' or '\u0625' or '\u0622' or '\u0671') mapped = '\u0627';
            // Alef maksura → ya
            else if (ch == '\u0649') mapped = '\u064A';
            // Ta marbuta → ha (gated)
            else if (ch == '\u0629' && options.NormalizeTaMarbuta) mapped = '\u0647';
            // Hamza on ya → ya; hamza on waw → waw
            else if (ch == '\u0626') mapped = '\u064A';
            else if (ch == '\u0624') mapped = '\u0648';
            // Arabic-Indic digits → ASCII (gated)
            else if (options.NormalizeArabicDigits)
            {
                if (ch is >= '\u0660' and <= '\u0669')
                    mapped = (char)('0' + (ch - '\u0660'));
                else if (ch is >= '\u06F0' and <= '\u06F9')
                    mapped = (char)('0' + (ch - '\u06F0'));
            }

            // Collapse whitespace runs.
            if (char.IsWhiteSpace(mapped))
            {
                if (!lastWasWhitespace && sb.Length > 0) sb.Append(' ');
                lastWasWhitespace = true;
            }
            else
            {
                sb.Append(mapped);
                lastWasWhitespace = false;
            }
        }

        // Trim trailing space from collapse.
        if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
        return sb.ToString();
    }

    private static bool IsArabicDiacritic(char ch) =>
        ch is >= '\u064B' and <= '\u065F'      // harakat + tanween + shadda + sukun + quranic marks
        || ch == '\u0670'                       // superscript alef
        || ch is >= '\u0610' and <= '\u061A'    // Islamic honorific marks
        || ch is >= '\u06D6' and <= '\u06ED'    // Quranic annotation marks in Arabic block
        || ch is >= '\u08D3' and <= '\u08FF';   // Arabic Extended-A combining marks
}
