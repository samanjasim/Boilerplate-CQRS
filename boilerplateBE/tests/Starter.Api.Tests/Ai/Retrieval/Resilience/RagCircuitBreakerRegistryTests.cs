using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Starter.Api.Tests.Ai.Observability;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagCircuitBreakerRegistryTests
{
    [Fact]
    public void Exposes_named_pipelines_for_qdrant_and_postgres_fts()
    {
        var registry = BuildRegistry(Enabled(true));

        registry.Qdrant.Should().NotBeNull();
        registry.PostgresFts.Should().NotBeNull();
    }

    [Fact]
    public async Task Disabled_pipeline_passes_exceptions_through_without_tripping()
    {
        var registry = BuildRegistry(Enabled(false));

        for (var i = 0; i < 50; i++)
        {
            var act = async () => await registry.Qdrant.ExecuteAsync(
                static (_) => throw new TimeoutException(), CancellationToken.None);
            await act.Should().ThrowAsync<TimeoutException>();
        }
        // Never trips — disabled pipeline forwards the raw exception every time.
    }

    [Fact]
    public async Task Trips_after_minimum_throughput_with_configured_failure_ratio()
    {
        using var listener = new TestMeterListener(AiRagCircuitMetrics.MeterName);

        var registry = BuildRegistry(
            new RagCircuitBreakerOptions { Enabled = true, MinimumThroughput = 5, FailureRatio = 0.5, BreakDurationMs = 60_000 });

        // 5 consecutive TimeoutExceptions → breaker should open on call 6.
        for (var i = 0; i < 5; i++)
        {
            var act = async () => await registry.Qdrant.ExecuteAsync(
                static (_) => throw new TimeoutException(), CancellationToken.None);
            await act.Should().ThrowAsync<TimeoutException>();
        }

        var tripped = async () => await registry.Qdrant.ExecuteAsync(
            static (_) => throw new TimeoutException(), CancellationToken.None);
        await tripped.Should().ThrowAsync<BrokenCircuitException>();

        listener.Snapshot().Should().Contain(m =>
            m.InstrumentName == "rag.circuit.state_changes" &&
            (string?)m.Tags["rag.circuit.service"] == "qdrant" &&
            (string?)m.Tags["rag.circuit.state"] == "open");
    }

    [Fact]
    public async Task Recovers_after_break_duration()
    {
        var registry = BuildRegistry(
            new RagCircuitBreakerOptions { Enabled = true, MinimumThroughput = 5, FailureRatio = 0.5, BreakDurationMs = 500 });

        for (var i = 0; i < 5; i++)
        {
            var act = async () => await registry.PostgresFts.ExecuteAsync(
                static (_) => throw new TimeoutException(), CancellationToken.None);
            await act.Should().ThrowAsync<TimeoutException>();
        }

        // Should be open now.
        var opened = async () => await registry.PostgresFts.ExecuteAsync(
            static (_) => throw new TimeoutException(), CancellationToken.None);
        await opened.Should().ThrowAsync<BrokenCircuitException>();

        await Task.Delay(700);

        // Half-open probe — success closes the circuit.
        var ok = await registry.PostgresFts.ExecuteAsync(static (_) => ValueTask.FromResult(42), CancellationToken.None);
        ok.Should().Be(42);

        // Circuit is now closed — subsequent failures do not short-circuit.
        var next = async () => await registry.PostgresFts.ExecuteAsync(
            static (_) => throw new TimeoutException(), CancellationToken.None);
        await next.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Programmer_bug_exceptions_do_not_count_toward_failure_ratio()
    {
        var registry = BuildRegistry(
            new RagCircuitBreakerOptions { Enabled = true, MinimumThroughput = 5, FailureRatio = 0.5, BreakDurationMs = 60_000 });

        // 50 programmer bugs — breaker should not trip because they aren't transient.
        for (var i = 0; i < 50; i++)
        {
            var act = async () => await registry.Qdrant.ExecuteAsync(
                static (_) => throw new InvalidOperationException(), CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    private static RagCircuitBreakerRegistry BuildRegistry(RagCircuitBreakerOptions bothServices)
    {
        var settings = new AiRagSettings
        {
            CircuitBreakers = new RagCircuitBreakerSettings
            {
                Qdrant = bothServices,
                PostgresFts = bothServices,
            }
        };
        return new RagCircuitBreakerRegistry(
            Options.Create(settings),
            NullLogger<RagCircuitBreakerRegistry>.Instance);
    }

    private static RagCircuitBreakerOptions Enabled(bool enabled) =>
        new() { Enabled = enabled, MinimumThroughput = 5, FailureRatio = 0.5, BreakDurationMs = 60_000 };
}
