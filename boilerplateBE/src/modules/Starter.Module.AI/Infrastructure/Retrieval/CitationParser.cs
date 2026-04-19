using System.Text.RegularExpressions;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal static class CitationParser
{
    private static readonly Regex MarkerRegex = new(
        @"\[(\d+(?:\s*,\s*\d+)*)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<AiMessageCitation> Parse(
        string? assistantText,
        IReadOnlyList<RetrievedChunk> retrievedChildren)
    {
        if (retrievedChildren.Count == 0)
            return [];

        if (string.IsNullOrWhiteSpace(assistantText))
            return Fallback(retrievedChildren);

        var parsedMarkers = new HashSet<int>();
        foreach (Match match in MarkerRegex.Matches(assistantText))
        {
            foreach (var tok in match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(tok, out var n)) continue;
                if (n < 1 || n > retrievedChildren.Count) continue;
                parsedMarkers.Add(n);
            }
        }

        if (parsedMarkers.Count == 0)
            return Fallback(retrievedChildren);

        return parsedMarkers
            .OrderBy(n => n)
            .Select(n => ToCitation(n, retrievedChildren[n - 1]))
            .ToList();
    }

    private static IReadOnlyList<AiMessageCitation> Fallback(IReadOnlyList<RetrievedChunk> retrieved) =>
        retrieved.Select((c, i) => ToCitation(i + 1, c)).ToList();

    private static AiMessageCitation ToCitation(int marker, RetrievedChunk c) => new(
        Marker: marker,
        ChunkId: c.ChunkId,
        DocumentId: c.DocumentId,
        DocumentName: c.DocumentName,
        SectionTitle: c.SectionTitle,
        PageNumber: c.PageNumber,
        Score: c.HybridScore);
}
