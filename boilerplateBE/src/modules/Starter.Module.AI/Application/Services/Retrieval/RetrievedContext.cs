namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RetrievedContext(
    IReadOnlyList<RetrievedChunk> Children,
    IReadOnlyList<RetrievedChunk> Parents,
    int TotalTokens,
    bool TruncatedByBudget,
    IReadOnlyList<string> DegradedStages,
    IReadOnlyList<RetrievedChunk> Siblings,
    int FusedCandidates,
    string DetectedLanguage)
{
    public static RetrievedContext Empty { get; } = new([], [], 0, false, [], [], 0, "unknown");
    public bool IsEmpty => Children.Count == 0;
}
