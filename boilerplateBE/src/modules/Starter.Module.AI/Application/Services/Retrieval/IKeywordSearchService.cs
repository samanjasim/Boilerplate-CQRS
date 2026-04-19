namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IKeywordSearchService
{
    Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct);
}
