namespace Starter.Module.AI.Infrastructure.Retrieval;

/// <summary>
/// Stage name constants used by <see cref="RagRetrievalService"/> to record
/// per-stage failures in <c>RetrievedContext.DegradedStages</c> and to tag
/// OpenTelemetry activity events. Tests assert on these exact strings, so treat
/// them as part of the observable contract.
/// </summary>
internal static class RagStages
{
    public const string Classify = "classify";
    public const string QueryRewrite = "query-rewrite";
    public const string EmbedQuery = "embed-query";
    public const string Rerank = "rerank";
    public const string NeighborExpand = "neighbor-expand";

    public static string VectorSearch(int variantIndex) => $"vector-search[{variantIndex}]";
    public static string KeywordSearch(int variantIndex) => $"keyword-search[{variantIndex}]";
}
