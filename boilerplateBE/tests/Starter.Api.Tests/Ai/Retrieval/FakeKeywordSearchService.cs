using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

internal sealed class FakeKeywordSearchService : IKeywordSearchService
{
    public List<KeywordSearchHit> HitsToReturn { get; set; } = new();
    public AclPayloadFilter? LastAclFilter { get; private set; }

    public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        AclPayloadFilter? aclFilter,
        int limit,
        CancellationToken ct)
    {
        LastAclFilter = aclFilter;
        IReadOnlyList<KeywordSearchHit> result = HitsToReturn.Take(limit).ToList();
        return Task.FromResult(result);
    }
}
