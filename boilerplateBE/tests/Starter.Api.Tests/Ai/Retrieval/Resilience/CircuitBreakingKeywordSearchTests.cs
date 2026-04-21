using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class CircuitBreakingKeywordSearchTests
{
    [Fact]
    public async Task SearchAsync_forwards_to_inner_when_circuit_closed()
    {
        var inner = new FixedKeywordSearch(new List<KeywordSearchHit> { new(Guid.NewGuid(), 0.8m) });
        var sut = new CircuitBreakingKeywordSearch(inner, BuildRegistry());

        var hits = await sut.SearchAsync(Guid.NewGuid(), "hello", null, 10, CancellationToken.None);

        hits.Should().HaveCount(1);
    }

    [Fact]
    public async Task Trips_on_sustained_DbException_and_short_circuits()
    {
        var inner = new ThrowingKeywordSearch(new FakeDbException());
        var sut = new CircuitBreakingKeywordSearch(inner, BuildRegistry(minimumThroughput: 5));

        for (var i = 0; i < 5; i++)
        {
            var act = async () => await sut.SearchAsync(Guid.NewGuid(), "x", null, 10, CancellationToken.None);
            await act.Should().ThrowAsync<FakeDbException>();
        }

        var tripped = async () => await sut.SearchAsync(Guid.NewGuid(), "x", null, 10, CancellationToken.None);
        await tripped.Should().ThrowAsync<BrokenCircuitException>();

        inner.CallCount.Should().Be(5);
    }

    private static RagCircuitBreakerRegistry BuildRegistry(int minimumThroughput = 10)
    {
        var settings = new AiRagSettings
        {
            CircuitBreakers = new RagCircuitBreakerSettings
            {
                Qdrant = new RagCircuitBreakerOptions { Enabled = true },
                PostgresFts = new RagCircuitBreakerOptions
                {
                    Enabled = true,
                    MinimumThroughput = minimumThroughput,
                    FailureRatio = 0.5,
                    BreakDurationMs = 60_000,
                },
            }
        };
        return new RagCircuitBreakerRegistry(
            Options.Create(settings),
            NullLogger<RagCircuitBreakerRegistry>.Instance);
    }

    private sealed class FixedKeywordSearch : IKeywordSearchService
    {
        private readonly IReadOnlyList<KeywordSearchHit> _hits;
        public FixedKeywordSearch(IReadOnlyList<KeywordSearchHit> hits) => _hits = hits;
        public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
            Guid t, string q, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => Task.FromResult(_hits);
    }

    private sealed class ThrowingKeywordSearch : IKeywordSearchService
    {
        private readonly Exception _ex;
        public int CallCount { get; private set; }
        public ThrowingKeywordSearch(Exception ex) => _ex = ex;
        public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
            Guid t, string q, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
        {
            CallCount++;
            throw _ex;
        }
    }

    private sealed class FakeDbException : System.Data.Common.DbException
    {
        public FakeDbException() : base("fake") { }
    }
}
