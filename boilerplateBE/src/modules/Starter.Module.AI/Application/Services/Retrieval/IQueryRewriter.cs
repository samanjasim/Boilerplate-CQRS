namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IQueryRewriter
{
    /// <summary>
    /// Returns the original query at index 0 plus up to N-1 rewrites.
    /// Never throws — falls back to [originalQuery] on any failure.
    /// </summary>
    Task<IReadOnlyList<string>> RewriteAsync(
        string originalQuery,
        string? language,
        CancellationToken ct);
}
