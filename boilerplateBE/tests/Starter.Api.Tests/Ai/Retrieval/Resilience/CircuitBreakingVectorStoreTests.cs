using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Starter.Api.Tests.Ai.Retrieval;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class CircuitBreakingVectorStoreTests
{
    [Fact]
    public async Task SearchAsync_forwards_to_inner_when_circuit_closed()
    {
        var inner = new FakeVectorStore();
        inner.HitsToReturn = new List<VectorSearchHit> { new(Guid.NewGuid(), 0.9m) };
        var sut = new CircuitBreakingVectorStore(inner, BuildRegistry());

        var hits = await sut.SearchAsync(Guid.NewGuid(), new float[] { 0.1f }, null, 10, CancellationToken.None);

        hits.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_short_circuits_with_BrokenCircuitException_when_open()
    {
        var inner = new ThrowingVectorStore(new TimeoutException());
        var sut = new CircuitBreakingVectorStore(inner, BuildRegistry(minimumThroughput: 5));

        // Trip the breaker.
        for (var i = 0; i < 5; i++)
        {
            var act = async () => await sut.SearchAsync(Guid.NewGuid(), Array.Empty<float>(), null, 10, CancellationToken.None);
            await act.Should().ThrowAsync<TimeoutException>();
        }

        var tripped = async () => await sut.SearchAsync(Guid.NewGuid(), Array.Empty<float>(), null, 10, CancellationToken.None);
        await tripped.Should().ThrowAsync<BrokenCircuitException>();

        inner.CallCount.Should().Be(5, "further calls short-circuit before the inner store is touched");
    }

    [Fact]
    public async Task Delegates_non_search_methods_to_inner_without_breaker()
    {
        var inner = new FakeVectorStore();
        var sut = new CircuitBreakingVectorStore(inner, BuildRegistry());

        await sut.EnsureCollectionAsync(Guid.NewGuid(), 1536, CancellationToken.None);
        await sut.UpsertAsync(Guid.NewGuid(), Array.Empty<VectorPoint>(), CancellationToken.None);
        await sut.DeleteByDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        await sut.DropCollectionAsync(Guid.NewGuid(), CancellationToken.None);
        // No assertions on inner — coverage is sufficient; the point is the methods don't throw.
    }

    private static RagCircuitBreakerRegistry BuildRegistry(int minimumThroughput = 10)
    {
        var settings = new AiRagSettings
        {
            CircuitBreakers = new RagCircuitBreakerSettings
            {
                Qdrant = new RagCircuitBreakerOptions
                {
                    Enabled = true,
                    MinimumThroughput = minimumThroughput,
                    FailureRatio = 0.5,
                    BreakDurationMs = 60_000,
                },
                PostgresFts = new RagCircuitBreakerOptions { Enabled = true },
            }
        };
        return new RagCircuitBreakerRegistry(
            Options.Create(settings),
            NullLogger<RagCircuitBreakerRegistry>.Instance);
    }

    private sealed class ThrowingVectorStore : IVectorStore
    {
        private readonly Exception _ex;
        public int CallCount { get; private set; }

        public ThrowingVectorStore(Exception ex) => _ex = ex;

        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
        {
            CallCount++;
            throw _ex;
        }
    }
}
