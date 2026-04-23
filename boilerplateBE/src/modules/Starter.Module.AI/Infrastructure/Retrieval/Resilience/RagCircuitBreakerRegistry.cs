using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

/// <summary>
/// Builds and owns the two retrieval circuit-breaker pipelines. Singleton-scoped
/// because Polly's state is per-pipeline-instance and must be shared across all
/// callers to meaningfully protect a backend.
/// </summary>
internal sealed class RagCircuitBreakerRegistry
{
    public ResiliencePipeline Qdrant { get; }
    public ResiliencePipeline PostgresFts { get; }

    public RagCircuitBreakerRegistry(
        IOptions<AiRagSettings> settings,
        ILogger<RagCircuitBreakerRegistry> logger)
    {
        var cfg = settings.Value.CircuitBreakers;
        Qdrant = Build("qdrant", cfg.Qdrant, logger);
        PostgresFts = Build("postgres-fts", cfg.PostgresFts, logger);
    }

    private static ResiliencePipeline Build(string service, RagCircuitBreakerOptions opts, ILogger logger)
    {
        if (!opts.Enabled)
            return ResiliencePipeline.Empty;

        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = opts.FailureRatio,
                MinimumThroughput = opts.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromMilliseconds(opts.BreakDurationMs),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(RagTransientExceptionClassifier.IsTransient),
                OnOpened = args =>
                {
                    AiRagCircuitMetrics.StateChanges.Add(
                        1,
                        new KeyValuePair<string, object?>("rag.circuit.service", service),
                        new KeyValuePair<string, object?>("rag.circuit.state", "open"));
                    logger.LogWarning(
                        "RAG circuit '{Service}' opened for {BreakDurationMs}ms after failure ratio exceeded",
                        service, opts.BreakDurationMs);
                    return default;
                },
                OnClosed = args =>
                {
                    AiRagCircuitMetrics.StateChanges.Add(
                        1,
                        new KeyValuePair<string, object?>("rag.circuit.service", service),
                        new KeyValuePair<string, object?>("rag.circuit.state", "closed"));
                    logger.LogInformation("RAG circuit '{Service}' closed — probe succeeded", service);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    AiRagCircuitMetrics.StateChanges.Add(
                        1,
                        new KeyValuePair<string, object?>("rag.circuit.service", service),
                        new KeyValuePair<string, object?>("rag.circuit.state", "half_open"));
                    logger.LogInformation("RAG circuit '{Service}' half-open — allowing one probe", service);
                    return default;
                },
            })
            .Build();
    }
}
