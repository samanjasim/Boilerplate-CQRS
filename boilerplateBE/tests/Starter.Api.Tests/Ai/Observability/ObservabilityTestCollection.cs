using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

/// <summary>
/// Tests in this collection share a process-global <see cref="System.Diagnostics.Metrics.MeterListener"/>
/// and assert on exact measurement counts. Running them in parallel would cross-contaminate
/// snapshots, so xUnit must serialize the entire collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ObservabilityTestCollection
{
    public const string Name = "AI Observability (serialized)";
}
