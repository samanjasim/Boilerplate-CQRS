using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

/// <summary>
/// Gate for <see cref="ContextualQueryResolver"/>. Decides whether a user
/// message looks like a follow-up that needs conversation-history-aware
/// rewriting. False negatives are acceptable (we just skip the LLM call and
/// retrieve the raw message). True negatives dominate in practice — most
/// questions are self-contained.
/// </summary>
internal static class ContextualFollowUpHeuristic
{
    private const int ShortMessageMaxChars = 25;

    private static readonly HashSet<string> PronounTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        // English
        "it", "this", "that", "they", "them", "these", "those", "one", "ones", "which",
        // Arabic — note: "هي" is omitted because it doubles as a copula in "ما هي" questions,
        // which would generate false positives on self-contained Arabic questions.
        "هو", "هذا", "هذه", "ذلك", "تلك", "هؤلاء", "الذي", "التي"
    };

    private static readonly string[] EnglishContinuationStarters =
    {
        "and ", "or ", "but ", "also ", "what about", "how about", "why", "when"
    };

    private static readonly string[] ArabicContinuationStarters =
    {
        "و", "أو", "لكن", "ماذا عن", "كيف", "لماذا", "متى"
    };

    private static readonly Regex WordSplitter = new(@"[\p{L}]+", RegexOptions.Compiled);

    public static bool LooksLikeFollowUp(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var trimmed = message.Trim();
        if (trimmed.Length <= ShortMessageMaxChars) return true;

        foreach (var starter in EnglishContinuationStarters)
            if (trimmed.StartsWith(starter, StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var starter in ArabicContinuationStarters)
            if (trimmed.StartsWith(starter, StringComparison.Ordinal)) return true;

        foreach (Match m in WordSplitter.Matches(trimmed))
        {
            if (PronounTokens.Contains(m.Value)) return true;
        }

        // Arabic pronoun-suffix: messages like "نضبطه" carry the pronoun as a
        // suffix on the verb; check for the short attached-pronoun forms.
        if (ContainsArabicPronounSuffix(trimmed)) return true;

        return false;
    }

    private static bool ContainsArabicPronounSuffix(string s)
    {
        // Third-person pronoun clitics: ـه, ـها, ـهم, ـهن
        // Cheap heuristic: any word that ends with these sequences.
        foreach (Match m in WordSplitter.Matches(s))
        {
            var w = m.Value;
            if (w.EndsWith("ه", StringComparison.Ordinal)
                || w.EndsWith("ها", StringComparison.Ordinal)
                || w.EndsWith("هم", StringComparison.Ordinal)
                || w.EndsWith("هن", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
