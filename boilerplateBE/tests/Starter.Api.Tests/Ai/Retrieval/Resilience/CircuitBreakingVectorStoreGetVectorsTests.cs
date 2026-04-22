using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class CircuitBreakingVectorStoreGetVectorsTests
{
    [Fact]
    public async Task Forwards_GetVectorsByIds_to_inner_store_when_circuit_closed()
    {
        var inner = new RecordingVectorStore();
        var settings = new AiRagSettings
        {
            CircuitBreakers = new RagCircuitBreakerSettings
            {
                Qdrant = new RagCircuitBreakerOptions { Enabled = true },
                PostgresFts = new RagCircuitBreakerOptions { Enabled = true },
            }
        };
        var registry = new RagCircuitBreakerRegistry(
            Options.Create(settings),
            NullLogger<RagCircuitBreakerRegistry>.Instance);
        var decorator = new CircuitBreakingVectorStore(inner, registry);

        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        inner.VectorsByIdResult = new Dictionary<Guid, float[]> { [id] = new[] { 1f, 2f, 3f } };

        var result = await decorator.GetVectorsByIdsAsync(tenant, new[] { id }, CancellationToken.None);

        inner.GetVectorsCalls.Should().Be(1);
        result.Should().ContainKey(id).WhoseValue.Should().Equal(1f, 2f, 3f);
    }

    private sealed class RecordingVectorStore : IVectorStore
    {
        public int GetVectorsCalls { get; private set; }
        public IReadOnlyDictionary<Guid, float[]> VectorsByIdResult { get; set; }
            = new Dictionary<Guid, float[]>();

        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(Guid t, float[] v, IReadOnlyCollection<Guid>? d, AclPayloadFilter? acl, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VectorSearchHit>>(Array.Empty<VectorSearchHit>());
        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
        {
            GetVectorsCalls++;
            return Task.FromResult(VectorsByIdResult);
        }
    }
}
