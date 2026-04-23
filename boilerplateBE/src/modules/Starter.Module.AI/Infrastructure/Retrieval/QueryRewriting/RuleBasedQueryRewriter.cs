using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal static class RuleBasedQueryRewriter
{
    private static readonly Regex LeadingEnglishQuestionWord = new(
        @"^\s*(what|how|when|where|why|who|which|is|are|can|does|do)\b[\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LeadingArabicQuestionWord = new(
        @"^\s*(هل|ماذا|ما\s+هو|ما\s+هي|ما|كيف|متى|أين|لماذا|لم|من|أي|كم)\s+",
        RegexOptions.Compiled);

    private static readonly Regex TrailingPoliteEnglish = new(
        @"\s+(please|thanks|thank you)\s*[?.!]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TrailingPoliteArabic = new(
        @"\s+(من\s+فضلك|لو\s+سمحت|شكرا)\s*[؟?.!]?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex TrailingQuestionMark = new(@"[?؟]\s*$", RegexOptions.Compiled);
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Returns [original] or [original, content-only-variant] depending on whether
    /// stripping question-words / polite tokens produces a materially different string.
    /// Empty / whitespace input returns [].
    /// </summary>
    public static IReadOnlyList<string> Rewrite(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

        var original = MultiWhitespace.Replace(input.Trim(), " ");
        var reduced = original;

        // Apply leading-word stripping iteratively so compound openings like
        // "what is …" are fully consumed (first pass strips "what ", second strips "is ").
        string prev;
        do
        {
            prev = reduced;
            reduced = LeadingEnglishQuestionWord.Replace(reduced, string.Empty);
            reduced = LeadingArabicQuestionWord.Replace(reduced, string.Empty);
        }
        while (!string.Equals(prev, reduced, StringComparison.Ordinal));
        reduced = TrailingPoliteEnglish.Replace(reduced, string.Empty);
        reduced = TrailingPoliteArabic.Replace(reduced, string.Empty);
        reduced = TrailingQuestionMark.Replace(reduced, string.Empty);
        reduced = MultiWhitespace.Replace(reduced, " ").Trim();

        if (string.Equals(original, reduced, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(reduced))
            return new[] { original };
        return new[] { original, reduced };
    }
}
