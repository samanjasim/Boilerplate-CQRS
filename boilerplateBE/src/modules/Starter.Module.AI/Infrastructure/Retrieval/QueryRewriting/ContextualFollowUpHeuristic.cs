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

        var words = WordSplitter.Matches(trimmed);
        foreach (Match m in words)
        {
            if (PronounTokens.Contains(m.Value)) return true;
        }

        return ContainsArabicPronounSuffix(words);
    }

    private static bool ContainsArabicPronounSuffix(MatchCollection words)
    {
        // Arabic third-person pronoun clitics (ـه, ـها, ـهم, ـهن) attach to verbs
        // as suffixes, e.g. "نضبطه" = "we configure it". WordSplitter's [\p{L}]+
        // keeps the full token intact, so suffix matching is a cheap EndsWith.
        foreach (Match m in words)
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
