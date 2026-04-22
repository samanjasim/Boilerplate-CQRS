using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Diversification;

public class VectorStoreContractTests
{
    [Fact]
    public async Task GetVectorsByIdsAsync_is_part_of_the_interface()
    {
        IVectorStore store = new StubVectorStore();
        var tenant = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var result = await store.GetVectorsByIdsAsync(tenant, new[] { id1, id2 }, CancellationToken.None);

        result.Should().ContainKey(id1).WhoseValue.Should().BeEquivalentTo(new[] { 1f, 0f });
        result.Should().ContainKey(id2).WhoseValue.Should().BeEquivalentTo(new[] { 0f, 1f });
    }

    private sealed class StubVectorStore : IVectorStore
    {
        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(Guid t, float[] v, IReadOnlyCollection<Guid>? d, AclPayloadFilter? acl, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VectorSearchHit>>(Array.Empty<VectorSearchHit>());

        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
        {
            var ids = pointIds.ToList();
            IReadOnlyDictionary<Guid, float[]> dict = new Dictionary<Guid, float[]>
            {
                [ids[0]] = new[] { 1f, 0f },
                [ids[1]] = new[] { 0f, 1f },
            };
            return Task.FromResult(dict);
        }
    }
}
