using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal static class SentenceSplitter
{
    private static readonly Regex Terminators = new(@"(?<=[\.!\?\u061F\u060C])\s+", RegexOptions.Compiled);

    public static IReadOnlyList<string> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        var parts = Terminators.Split(text.Trim());
        return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
    }
}
