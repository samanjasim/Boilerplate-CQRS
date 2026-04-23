namespace Starter.Module.AI.Infrastructure.Observability;

/// <summary>
/// Lightweight language hint for RAG metrics. Counts the ratio of Arabic-block
/// codepoints (U+0600 to U+06FF) against ASCII letters. Not a replacement for a
/// real language detector — intended only for low-cardinality tagging.
/// Ratio &gt; 0.5 = <c>ar</c>, &lt; 0.1 = <c>en</c>, anywhere else = <c>mixed</c>.
/// Returns <c>unknown</c> when the query has no letters at all.
/// </summary>
internal static class RagLanguageDetector
{
    public const string Arabic = "ar";
    public const string English = "en";
    public const string Mixed = "mixed";
    public const string Unknown = "unknown";

    public static string Detect(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Unknown;

        int ar = 0, en = 0;
        foreach (var c in query)
        {
            if (c >= '\u0600' && c <= '\u06FF') ar++;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) en++;
        }

        var total = ar + en;
        if (total == 0) return Unknown;

        var ratio = (double)ar / total;
        if (ratio > 0.5) return Arabic;
        if (ratio < 0.1) return English;
        return Mixed;
    }
}
