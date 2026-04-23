# Plan 4b-6 — Retrieval Circuit Breaker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Polly v8 circuit breakers around `IVectorStore.SearchAsync` (Qdrant) and `IKeywordSearchService.SearchAsync` (Postgres FTS) so persistent backend failures fail fast via the existing degrade-and-continue pipeline, while preserving hybrid retrieval's partial-success behaviour.

**Architecture:** Two named Polly `ResiliencePipeline` instances, built by a `RagCircuitBreakerRegistry` singleton from `AiRagSettings.CircuitBreakers` config. Decorators `CircuitBreakingVectorStore` and `CircuitBreakingKeywordSearch` wrap the real implementations via DI replacement. Open circuits surface as `BrokenCircuitException`, which `RagRetrievalService.WithTimeoutAsyncCore` treats as transient — so existing `DegradedStages` telemetry and fall-through keyword-only retrieval work unchanged. A new `AiRagCircuitMetrics.StateChanges` counter reports every Open/Closed/HalfOpened transition tagged by service name.

**Tech Stack:** .NET 10, Polly v8 (`Polly.Core`), Microsoft.Extensions.Options, xUnit + FluentAssertions + Moq, existing `TestMeterListener` pattern.

**Spec reference:** `docs/superpowers/specs/2026-04-21-ai-module-plan-4b-6-retrieval-circuit-breaker-design.md`

---

## File structure (locked)

### New

- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerOptions.cs` — options record for one breaker (`Enabled`, `MinimumThroughput`, `FailureRatio`, `BreakDurationMs`).
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerSettings.cs` — parent container with `Qdrant` and `PostgresFts` properties; hung off `AiRagSettings.CircuitBreakers`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagTransientExceptionClassifier.cs` — single source of truth for which exceptions count as transient; shared by `RagRetrievalService` and the new breakers.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerRegistry.cs` — builds the two named pipelines from options, hooks `OnOpened` / `OnClosed` / `OnHalfOpened` to `AiRagCircuitMetrics`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs` — decorator implementing `IVectorStore`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingKeywordSearch.cs` — decorator implementing `IKeywordSearchService`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiRagCircuitMetrics.cs` — meter + counter for state changes.
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerRegistryTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingKeywordSearchTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagRetrievalDegradationWithBreakerTests.cs` — end-to-end: open circuit on `IVectorStore` → `vector-search[0]` in `DegradedStages`, keyword hits still merged.

### Modified

- `boilerplateBE/Directory.Packages.props` — add `<PackageVersion Include="Polly" Version="8.4.2" />`.
- `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj` — add `<PackageReference Include="Polly" />`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs` — add `CircuitBreakers` property of type `RagCircuitBreakerSettings`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagStageOutcome.cs` — add `CircuitOpen = "circuit_open"`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` — replace the private `IsTransientStageException` with a delegation to `RagTransientExceptionClassifier.IsTransient`; add `Polly.CircuitBreaker.BrokenCircuitException` to the transient set; emit `circuit_open` outcome instead of `error` when the thrown exception is `BrokenCircuitException`.
- `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` — register `RagCircuitBreakerRegistry` as singleton, replace the two service registrations with decorators that resolve the real implementation by name.
- `boilerplateBE/src/Starter.Api/appsettings.json` and `appsettings.Development.json` — document defaults under `AI:Rag:CircuitBreakers`.

---

## Task 1: Add Polly package reference

**Files:**
- Modify: `boilerplateBE/Directory.Packages.props`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj`

- [ ] **Step 1: Add centrally managed Polly version**

Edit `boilerplateBE/Directory.Packages.props`, add the following line inside the existing `<ItemGroup>` that lists AI-module packages (near `<PackageVersion Include="ReverseMarkdown" Version="4.6.0" />`):

```xml
    <PackageVersion Include="Polly" Version="8.4.2" />
```

- [ ] **Step 2: Reference Polly from the AI module**

Edit `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj`, add inside the first `<ItemGroup>` (the one with other package references):

```xml
    <PackageReference Include="Polly" />
```

- [ ] **Step 3: Restore + build to verify the package resolves**

Run: `cd boilerplateBE && dotnet restore src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: restore succeeds with no errors.

Run: `dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/Directory.Packages.props boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj
git commit -m "chore(ai): add Polly v8 package reference for plan 4b-6"
```

---

## Task 2: Extract transient-exception classifier to a shared helper

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagTransientExceptionClassifier.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagTransientExceptionClassifierTests.cs`

- [ ] **Step 1: Write the failing test**

Create directory `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/` if it does not exist, then create the file below.

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagTransientExceptionClassifierTests.cs`:

```csharp
using System.Data.Common;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagTransientExceptionClassifierTests
{
    [Theory]
    [InlineData(typeof(System.Net.Http.HttpRequestException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(TaskCanceledException))]
    public void Transient_framework_exceptions_are_classified_as_transient(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void Grpc_RpcException_is_transient()
    {
        var ex = new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "down"));
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void DbException_subclass_is_transient()
    {
        var ex = new FakeDbException();
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NullReferenceException))]
    public void Programmer_bugs_are_not_transient(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeFalse();
    }

    private sealed class FakeDbException : DbException
    {
        public FakeDbException() : base("fake") { }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagTransientExceptionClassifierTests" --nologo`
Expected: compile error — `RagTransientExceptionClassifier` does not exist yet.

- [ ] **Step 3: Create the classifier**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagTransientExceptionClassifier.cs`:

```csharp
using System.Data.Common;
using System.Net.Http;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

/// <summary>
/// Single source of truth for which exceptions <see cref="RagRetrievalService"/>
/// and the retrieval circuit breakers treat as transient dependency failures.
/// Programmer bugs (ArgumentException, InvalidOperationException, NullReferenceException,
/// ObjectDisposedException, etc.) are deliberately excluded so they fail the turn loudly
/// during development instead of being silently hidden as "degraded".
/// </summary>
internal static class RagTransientExceptionClassifier
{
    public static bool IsTransient(Exception ex) =>
        ex is HttpRequestException
           or TimeoutException
           or DbException
           or Grpc.Core.RpcException
           or TaskCanceledException;
}
```

- [ ] **Step 4: Replace the private method in `RagRetrievalService`**

Edit `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`:

Add `using Starter.Module.AI.Infrastructure.Retrieval.Resilience;` at the top.

Delete the private method `IsTransientStageException` (at the bottom of the class, the `ex is System.Net.Http.HttpRequestException or TimeoutException or ...` method).

Replace every call site of `IsTransientStageException` with `RagTransientExceptionClassifier.IsTransient`:

In `WithTimeoutAsync<T>`:

```csharp
private Task<T?> WithTimeoutAsync<T>(
    Func<CancellationToken, Task<T>> op,
    int timeoutMs,
    string stageName,
    List<string> degraded,
    CancellationToken ct) where T : class
    => WithTimeoutAsyncCore(op, timeoutMs, stageName, degraded, _logger, RagTransientExceptionClassifier.IsTransient, ct);
```

In the classify block (around line 180), change:

```csharp
catch (Exception ex) when (IsTransientStageException(ex))
```

to:

```csharp
catch (Exception ex) when (RagTransientExceptionClassifier.IsTransient(ex))
```

- [ ] **Step 5: Run the new test and the full AI test suite**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai" --nologo`
Expected: all tests pass (the extracted method has identical behaviour to the inlined original).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagTransientExceptionClassifier.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagTransientExceptionClassifierTests.cs
git commit -m "refactor(ai): extract transient-exception classifier for retrieval resilience"
```

---

## Task 3: Add `CircuitOpen` outcome + typed options

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagStageOutcome.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerOptions.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerSettings.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`

- [ ] **Step 1: Write the failing settings-binding test**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerSettingsBindingTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagCircuitBreakerSettingsBindingTests
{
    [Fact]
    public void Defaults_match_spec_when_section_is_absent()
    {
        var settings = BuildSettings(new Dictionary<string, string?>());

        settings.CircuitBreakers.Should().NotBeNull();
        settings.CircuitBreakers.Qdrant.Enabled.Should().BeTrue();
        settings.CircuitBreakers.Qdrant.MinimumThroughput.Should().Be(10);
        settings.CircuitBreakers.Qdrant.FailureRatio.Should().Be(0.5);
        settings.CircuitBreakers.Qdrant.BreakDurationMs.Should().Be(30_000);

        settings.CircuitBreakers.PostgresFts.Enabled.Should().BeTrue();
        settings.CircuitBreakers.PostgresFts.MinimumThroughput.Should().Be(10);
        settings.CircuitBreakers.PostgresFts.FailureRatio.Should().Be(0.5);
        settings.CircuitBreakers.PostgresFts.BreakDurationMs.Should().Be(30_000);
    }

    [Fact]
    public void Configured_values_override_defaults()
    {
        var settings = BuildSettings(new Dictionary<string, string?>
        {
            ["AI:Rag:CircuitBreakers:Qdrant:Enabled"] = "false",
            ["AI:Rag:CircuitBreakers:Qdrant:MinimumThroughput"] = "20",
            ["AI:Rag:CircuitBreakers:Qdrant:FailureRatio"] = "0.75",
            ["AI:Rag:CircuitBreakers:Qdrant:BreakDurationMs"] = "60000",
            ["AI:Rag:CircuitBreakers:PostgresFts:BreakDurationMs"] = "10000",
        });

        settings.CircuitBreakers.Qdrant.Enabled.Should().BeFalse();
        settings.CircuitBreakers.Qdrant.MinimumThroughput.Should().Be(20);
        settings.CircuitBreakers.Qdrant.FailureRatio.Should().Be(0.75);
        settings.CircuitBreakers.Qdrant.BreakDurationMs.Should().Be(60_000);
        settings.CircuitBreakers.PostgresFts.BreakDurationMs.Should().Be(10_000);
    }

    private static AiRagSettings BuildSettings(IDictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<AiRagSettings>().Bind(config.GetSection(AiRagSettings.SectionName));
        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<AiRagSettings>>().Value;
    }
}
```

- [ ] **Step 2: Run the test and confirm compile failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagCircuitBreakerSettingsBindingTests" --nologo`
Expected: compile error — `CircuitBreakers`, `RagCircuitBreakerSettings`, `RagCircuitBreakerOptions` do not exist.

- [ ] **Step 3: Add the `CircuitOpen` outcome constant**

Edit `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagStageOutcome.cs`:

```csharp
namespace Starter.Module.AI.Infrastructure.Observability;

/// Tag values for the rag.outcome dimension. Enumerated up-front to avoid
/// cardinality explosions from dynamic strings.
internal static class RagStageOutcome
{
    public const string Success = "success";
    public const string Timeout = "timeout";
    public const string Error = "error";
    public const string CircuitOpen = "circuit_open";
}
```

- [ ] **Step 4: Create the options record**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerOptions.cs`:

```csharp
namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

public sealed class RagCircuitBreakerOptions
{
    /// <summary>
    /// When false, calls bypass the breaker entirely (no sampling, no tripping).
    /// Use in dev environments where the backend is expected to flap.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Minimum number of executions within the 30-second sampling window before the
    /// breaker can trip. Too low a value causes trips on incidental failures.
    /// </summary>
    public int MinimumThroughput { get; init; } = 10;

    /// <summary>
    /// Failure ratio in [0, 1] that triggers the trip once <see cref="MinimumThroughput"/>
    /// is reached.
    /// </summary>
    public double FailureRatio { get; init; } = 0.5;

    /// <summary>
    /// Time the breaker stays open before transitioning to half-open and allowing
    /// one probe request through.
    /// </summary>
    public int BreakDurationMs { get; init; } = 30_000;
}
```

- [ ] **Step 5: Create the settings container**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerSettings.cs`:

```csharp
namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

public sealed class RagCircuitBreakerSettings
{
    public RagCircuitBreakerOptions Qdrant { get; init; } = new();
    public RagCircuitBreakerOptions PostgresFts { get; init; } = new();
}
```

- [ ] **Step 6: Attach the settings to `AiRagSettings`**

Edit `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`:

Add `using Starter.Module.AI.Infrastructure.Retrieval.Resilience;` at the top.

At the end of the class, add:

```csharp
    // ---- New in Plan 4b-6 — Retrieval circuit breakers ----
    public RagCircuitBreakerSettings CircuitBreakers { get; init; } = new();
```

- [ ] **Step 7: Run the test and confirm pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagCircuitBreakerSettingsBindingTests" --nologo`
Expected: both tests pass.

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagStageOutcome.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerOptions.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerSettings.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerSettingsBindingTests.cs
git commit -m "feat(ai): add circuit-breaker options to AiRagSettings"
```

---

## Task 4: Add `AiRagCircuitMetrics`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiRagCircuitMetrics.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/AiRagCircuitMetricsTests.cs`

- [ ] **Step 1: Write the failing test**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/AiRagCircuitMetricsTests.cs`:

```csharp
using FluentAssertions;
using Starter.Api.Tests.Ai.Observability;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class AiRagCircuitMetricsTests
{
    [Fact]
    public void StateChanges_emits_measurement_with_service_and_state_tags()
    {
        using var listener = new TestMeterListener(AiRagCircuitMetrics.MeterName);

        AiRagCircuitMetrics.StateChanges.Add(
            1,
            new KeyValuePair<string, object?>("rag.circuit.service", "qdrant"),
            new KeyValuePair<string, object?>("rag.circuit.state", "open"));

        var snap = listener.Snapshot();
        snap.Should().ContainSingle(m =>
            m.InstrumentName == "rag.circuit.state_changes" &&
            (string?)m.Tags["rag.circuit.service"] == "qdrant" &&
            (string?)m.Tags["rag.circuit.state"] == "open" &&
            m.Value == 1);
    }

    [Fact]
    public void Meter_name_is_distinct_from_rag_meter()
    {
        AiRagCircuitMetrics.MeterName.Should().NotBe(AiRagMetrics.MeterName);
    }
}
```

- [ ] **Step 2: Run and confirm compile failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~AiRagCircuitMetricsTests" --nologo`
Expected: compile error — `AiRagCircuitMetrics` does not exist.

- [ ] **Step 3: Create the metrics class**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiRagCircuitMetrics.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace Starter.Module.AI.Infrastructure.Observability;

/// <summary>
/// Dedicated meter for retrieval circuit-breaker state transitions so the cardinality
/// footprint is independent from the main RAG pipeline meter.
/// </summary>
internal static class AiRagCircuitMetrics
{
    public const string MeterName = "Starter.Module.AI.Rag.Circuit";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> StateChanges =
        _meter.CreateCounter<long>(
            name: "rag.circuit.state_changes",
            unit: "count",
            description: "Circuit breaker state transitions tagged by service (qdrant|postgres-fts) and state (open|closed|half_open).");
}
```

- [ ] **Step 4: Run the test and confirm pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~AiRagCircuitMetricsTests" --nologo`
Expected: both tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiRagCircuitMetrics.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/AiRagCircuitMetricsTests.cs
git commit -m "feat(ai): add circuit-breaker state-change metric"
```

---

## Task 5: Build the circuit-breaker registry

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerRegistry.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerRegistryTests.cs`

- [ ] **Step 1: Write the failing tests**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerRegistryTests.cs`:

```csharp
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
                static (_) => throw new TimeoutException(), default);
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
                static (_) => throw new TimeoutException(), default);
            await act.Should().ThrowAsync<TimeoutException>();
        }

        var tripped = async () => await registry.Qdrant.ExecuteAsync(
            static (_) => throw new TimeoutException(), default);
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
            new RagCircuitBreakerOptions { Enabled = true, MinimumThroughput = 5, FailureRatio = 0.5, BreakDurationMs = 200 });

        for (var i = 0; i < 5; i++)
        {
            var act = async () => await registry.PostgresFts.ExecuteAsync(
                static (_) => throw new TimeoutException(), default);
            await act.Should().ThrowAsync<TimeoutException>();
        }

        // Should be open now.
        var opened = async () => await registry.PostgresFts.ExecuteAsync(
            static (_) => throw new TimeoutException(), default);
        await opened.Should().ThrowAsync<BrokenCircuitException>();

        await Task.Delay(300);

        // Half-open probe — success closes the circuit.
        var ok = await registry.PostgresFts.ExecuteAsync(static (_) => ValueTask.FromResult(42), default);
        ok.Should().Be(42);

        // Circuit is now closed — subsequent failures do not short-circuit.
        var next = async () => await registry.PostgresFts.ExecuteAsync(
            static (_) => throw new TimeoutException(), default);
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
                static (_) => throw new InvalidOperationException(), default);
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
```

- [ ] **Step 2: Run and confirm compile failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagCircuitBreakerRegistryTests" --nologo`
Expected: compile error — `RagCircuitBreakerRegistry` does not exist.

- [ ] **Step 3: Implement the registry**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerRegistry.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests and confirm pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagCircuitBreakerRegistryTests" --nologo`
Expected: all five tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerRegistry.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerRegistryTests.cs
git commit -m "feat(ai): add retrieval circuit-breaker registry"
```

---

## Task 6: `CircuitBreakingVectorStore` decorator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreTests.cs`:

```csharp
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

        var hits = await sut.SearchAsync(Guid.NewGuid(), new float[] { 0.1f }, null, 10, default);

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
            var act = async () => await sut.SearchAsync(Guid.NewGuid(), Array.Empty<float>(), null, 10, default);
            await act.Should().ThrowAsync<TimeoutException>();
        }

        var tripped = async () => await sut.SearchAsync(Guid.NewGuid(), Array.Empty<float>(), null, 10, default);
        await tripped.Should().ThrowAsync<BrokenCircuitException>();

        inner.CallCount.Should().Be(5, "further calls short-circuit before the inner store is touched");
    }

    [Fact]
    public async Task Delegates_non_search_methods_to_inner_without_breaker()
    {
        var inner = new FakeVectorStore();
        var sut = new CircuitBreakingVectorStore(inner, BuildRegistry());

        await sut.EnsureCollectionAsync(Guid.NewGuid(), 1536, default);
        await sut.UpsertAsync(Guid.NewGuid(), Array.Empty<VectorPoint>(), default);
        await sut.DeleteByDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), default);
        await sut.DropCollectionAsync(Guid.NewGuid(), default);
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
```

- [ ] **Step 2: Run and confirm compile failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CircuitBreakingVectorStoreTests" --nologo`
Expected: compile error — `CircuitBreakingVectorStore` does not exist.

- [ ] **Step 3: Implement the decorator**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs`:

```csharp
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

/// <summary>
/// <see cref="IVectorStore"/> decorator that routes <see cref="SearchAsync"/>
/// through the Qdrant circuit-breaker pipeline. Mutating operations (Ensure/Upsert/
/// Delete/Drop) bypass the breaker because they are invoked from the ingestion path
/// (indexing retries are MassTransit's concern, not the live-chat latency budget).
/// </summary>
internal sealed class CircuitBreakingVectorStore : IVectorStore
{
    private readonly IVectorStore _inner;
    private readonly RagCircuitBreakerRegistry _registry;

    public CircuitBreakingVectorStore(IVectorStore inner, RagCircuitBreakerRegistry registry)
    {
        _inner = inner;
        _registry = registry;
    }

    public Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct)
        => _inner.EnsureCollectionAsync(tenantId, vectorSize, ct);

    public Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct)
        => _inner.UpsertAsync(tenantId, points, ct);

    public Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
        => _inner.DeleteByDocumentAsync(tenantId, documentId, ct);

    public Task DropCollectionAsync(Guid tenantId, CancellationToken ct)
        => _inner.DropCollectionAsync(tenantId, ct);

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid tenantId,
        float[] queryVector,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
    {
        return await _registry.Qdrant.ExecuteAsync(
            async token => await _inner.SearchAsync(tenantId, queryVector, documentFilter, limit, token),
            ct);
    }
}
```

- [ ] **Step 4: Run the tests and confirm pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CircuitBreakingVectorStoreTests" --nologo`
Expected: all three tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreTests.cs
git commit -m "feat(ai): add circuit-breaking IVectorStore decorator"
```

---

## Task 7: `CircuitBreakingKeywordSearch` decorator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingKeywordSearch.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingKeywordSearchTests.cs`

- [ ] **Step 1: Write the failing tests**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingKeywordSearchTests.cs`:

```csharp
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

        var hits = await sut.SearchAsync(Guid.NewGuid(), "hello", null, 10, default);

        hits.Should().HaveCount(1);
    }

    [Fact]
    public async Task Trips_on_sustained_DbException_and_short_circuits()
    {
        var inner = new ThrowingKeywordSearch(new FakeDbException());
        var sut = new CircuitBreakingKeywordSearch(inner, BuildRegistry(minimumThroughput: 5));

        for (var i = 0; i < 5; i++)
        {
            var act = async () => await sut.SearchAsync(Guid.NewGuid(), "x", null, 10, default);
            await act.Should().ThrowAsync<FakeDbException>();
        }

        var tripped = async () => await sut.SearchAsync(Guid.NewGuid(), "x", null, 10, default);
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
```

- [ ] **Step 2: Run and confirm compile failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CircuitBreakingKeywordSearchTests" --nologo`
Expected: compile error — `CircuitBreakingKeywordSearch` does not exist.

- [ ] **Step 3: Implement the decorator**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingKeywordSearch.cs`:

```csharp
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

/// <summary>
/// <see cref="IKeywordSearchService"/> decorator that routes
/// <see cref="SearchAsync"/> through the Postgres-FTS circuit-breaker pipeline.
/// </summary>
internal sealed class CircuitBreakingKeywordSearch : IKeywordSearchService
{
    private readonly IKeywordSearchService _inner;
    private readonly RagCircuitBreakerRegistry _registry;

    public CircuitBreakingKeywordSearch(IKeywordSearchService inner, RagCircuitBreakerRegistry registry)
    {
        _inner = inner;
        _registry = registry;
    }

    public async Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
    {
        return await _registry.PostgresFts.ExecuteAsync(
            async token => await _inner.SearchAsync(tenantId, queryText, documentFilter, limit, token),
            ct);
    }
}
```

- [ ] **Step 4: Run the tests and confirm pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CircuitBreakingKeywordSearchTests" --nologo`
Expected: both tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingKeywordSearch.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingKeywordSearchTests.cs
git commit -m "feat(ai): add circuit-breaking IKeywordSearchService decorator"
```

---

## Task 8: Teach `WithTimeoutAsyncCore` about `BrokenCircuitException`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagTransientExceptionClassifier.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagTransientExceptionClassifierTests.cs`

- [ ] **Step 1: Write the failing tests**

Append the following fact to `RagTransientExceptionClassifierTests.cs`:

```csharp
    [Fact]
    public void BrokenCircuitException_is_transient()
    {
        var ex = new Polly.CircuitBreaker.BrokenCircuitException();
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeTrue();
    }
```

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/WithTimeoutAsyncCircuitOpenOutcomeTests.cs`:

```csharp
using System.Linq;
using FluentAssertions;
using Starter.Api.Tests.Ai.Observability;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

[Collection(ObservabilityTestCollection.Name)]
public class WithTimeoutAsyncCircuitOpenOutcomeTests
{
    [Fact]
    public async Task Open_circuit_records_circuit_open_outcome_and_degrades_stage()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalService.RunWithTimeoutAsyncForTests<string>(
            op: _ => throw new Polly.CircuitBreaker.BrokenCircuitException(),
            timeoutMs: 500,
            stageName: "vector-search[0]",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("vector-search[0]");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome"
                        && (string?)m.Tags["rag.stage"] == "vector-search[0]")
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "circuit_open");
    }
}
```

- [ ] **Step 2: Run and confirm failures**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~WithTimeoutAsyncCircuitOpenOutcomeTests|FullyQualifiedName~RagTransientExceptionClassifierTests" --nologo`
Expected: new tests fail because `BrokenCircuitException` isn't classified as transient yet, and the outcome tag is still `error`.

- [ ] **Step 3: Classify `BrokenCircuitException` as transient**

Edit `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagTransientExceptionClassifier.cs`:

```csharp
using System.Data.Common;
using System.Net.Http;
using Polly.CircuitBreaker;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

internal static class RagTransientExceptionClassifier
{
    public static bool IsTransient(Exception ex) =>
        ex is HttpRequestException
           or TimeoutException
           or DbException
           or Grpc.Core.RpcException
           or TaskCanceledException
           or BrokenCircuitException;
}
```

- [ ] **Step 4: Emit `circuit_open` outcome when the exception is `BrokenCircuitException`**

Edit `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`.

Locate the `WithTimeoutAsyncCore` method (around line 566). Add `using Polly.CircuitBreaker;` at the top.

Replace the `catch (Exception ex) when (isTransient(ex))` block with:

```csharp
        catch (Exception ex) when (isTransient(ex))
        {
            outcome = ex is BrokenCircuitException
                ? RagStageOutcome.CircuitOpen
                : RagStageOutcome.Error;
            degraded.Add(stageName);
            logger?.LogWarning(ex,
                "RAG stage '{Stage}' failed ({Outcome})", stageName, outcome);
            return null;
        }
```

(Previously the block was `LogError` unconditionally. The log level drops to `Warning` because an open circuit is expected steady-state behaviour during an outage — not an exception worth paging over. The stack trace is still captured.)

- [ ] **Step 5: Run the suite and confirm pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai" --nologo`
Expected: all AI tests pass, including the new ones.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagTransientExceptionClassifier.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagTransientExceptionClassifierTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/WithTimeoutAsyncCircuitOpenOutcomeTests.cs
git commit -m "feat(ai): route open circuits through existing degradation pipeline with circuit_open outcome"
```

---

## Task 9: Wire the decorators into DI

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerDIRegistrationTests.cs`

- [ ] **Step 1: Write the failing test**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerDIRegistrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Module.AI;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagCircuitBreakerDIRegistrationTests
{
    [Fact]
    public void Registered_IVectorStore_is_the_circuit_breaking_decorator()
    {
        using var sp = BuildProvider();

        var resolved = sp.GetRequiredService<IVectorStore>();
        resolved.Should().BeOfType<CircuitBreakingVectorStore>();
    }

    [Fact]
    public void Registered_IKeywordSearchService_is_the_circuit_breaking_decorator()
    {
        using var sp = BuildProvider();

        var resolved = sp.GetRequiredService<IKeywordSearchService>();
        resolved.Should().BeOfType<CircuitBreakingKeywordSearch>();
    }

    [Fact]
    public void Registry_is_singleton()
    {
        using var sp = BuildProvider();

        var a = sp.GetRequiredService<RagCircuitBreakerRegistry>();
        var b = sp.GetRequiredService<RagCircuitBreakerRegistry>();
        a.Should().BeSameAs(b);
    }

    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=x;Username=x;Password=x",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        // Only register enough of Starter.Application to satisfy AI module dependencies
        // that surface in this test (ICacheService etc.). For a narrower DI test we can
        // register mocks directly if full module wiring is too heavy.
        services.AddSingleton<Starter.Application.Common.Interfaces.ICacheService, Starter.Api.Tests.Ai.Fakes.NullCacheService>();
        new AIModule().ConfigureServices(services, config);
        return services.BuildServiceProvider();
    }
}
```

Verify `Starter.Api.Tests.Ai.Fakes.NullCacheService` exists. If not, create it:

`boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/NullCacheService.cs` (only if the file does not already exist):

```csharp
using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Ai.Fakes;

public sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
}
```

(If the existing `ICacheService` has a different member surface, match it — search `boilerplateBE/src/modules/Starter.Application/Common/Interfaces/ICacheService.cs` and mirror the methods with empty implementations.)

- [ ] **Step 2: Run and confirm failures**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagCircuitBreakerDIRegistrationTests" --nologo`
Expected: either compile failure (if `NullCacheService` was added new) then test failures — registered types are `QdrantVectorStore` / `PostgresKeywordSearchService`, not the decorators.

- [ ] **Step 3: Wire the decorators in `AIModule.ConfigureServices`**

Edit `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`.

Add the using at the top:

```csharp
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
```

Replace the existing line:

```csharp
services.AddSingleton<IVectorStore, Infrastructure.Ingestion.QdrantVectorStore>();
```

with:

```csharp
services.AddSingleton<Infrastructure.Ingestion.QdrantVectorStore>();
services.AddSingleton<IVectorStore>(sp => new CircuitBreakingVectorStore(
    sp.GetRequiredService<Infrastructure.Ingestion.QdrantVectorStore>(),
    sp.GetRequiredService<RagCircuitBreakerRegistry>()));
```

Replace the existing line:

```csharp
services.AddScoped<IKeywordSearchService, Infrastructure.Retrieval.PostgresKeywordSearchService>();
```

with:

```csharp
services.AddScoped<Infrastructure.Retrieval.PostgresKeywordSearchService>();
services.AddScoped<IKeywordSearchService>(sp => new CircuitBreakingKeywordSearch(
    sp.GetRequiredService<Infrastructure.Retrieval.PostgresKeywordSearchService>(),
    sp.GetRequiredService<RagCircuitBreakerRegistry>()));
```

Immediately before the `IVectorStore` block add the registry registration:

```csharp
services.AddSingleton<RagCircuitBreakerRegistry>();
```

- [ ] **Step 4: Run the DI tests and the full AI suite**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai" --nologo`
Expected: all tests pass, including the three new DI registration tests.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagCircuitBreakerDIRegistrationTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/NullCacheService.cs
git commit -m "feat(ai): wire circuit-breaking retrieval decorators into DI"
```

(Drop `NullCacheService.cs` from the `git add` if it already existed.)

---

## Task 10: End-to-end degradation test — open circuit still produces results from the healthy side

**Files:**
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagRetrievalDegradationWithBreakerTests.cs`

- [ ] **Step 1: Write the failing test**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagRetrievalDegradationWithBreakerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Retrieval;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagRetrievalDegradationWithBreakerTests
{
    [Fact]
    public async Task Open_qdrant_circuit_degrades_vector_stage_and_keyword_hits_still_flow()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(nameof(Open_qdrant_circuit_degrades_vector_stage_and_keyword_hits_still_flow))
            .Options;
        await using var db = new AiDbContext(dbOptions);

        var chunkPointId = Guid.NewGuid();
        db.AiDocuments.Add(new AiDocument { Id = documentId, TenantId = tenantId, Name = "doc.md",
            Status = AiDocumentStatus.Ready });
        db.AiDocumentChunks.Add(new AiDocumentChunk {
            Id = Guid.NewGuid(), DocumentId = documentId, QdrantPointId = chunkPointId,
            Content = "Qdrant is a vector database", ChunkIndex = 0 });
        await db.SaveChangesAsync();

        // Qdrant breaker trips after one sample; vector store always throws TimeoutException.
        var vectorInner = new ThrowingVectorStoreForBreakerTest();
        var keywordInner = new FixedKeywordSearchForBreakerTest(new List<KeywordSearchHit>
        {
            new(chunkPointId, 0.9m)
        });

        var settings = new AiRagSettings
        {
            TopK = 3, RetrievalTopK = 3, RerankStrategy = RerankStrategy.Off,
            StageTimeoutVectorMs = 200, StageTimeoutKeywordMs = 1000,
            CircuitBreakers = new RagCircuitBreakerSettings
            {
                Qdrant = new RagCircuitBreakerOptions { Enabled = true, MinimumThroughput = 1, FailureRatio = 0.5, BreakDurationMs = 60_000 },
                PostgresFts = new RagCircuitBreakerOptions { Enabled = true },
            }
        };
        var registry = new RagCircuitBreakerRegistry(
            Options.Create(settings), NullLogger<RagCircuitBreakerRegistry>.Instance);

        var breaker = new CircuitBreakingVectorStore(vectorInner, registry);

        // First call fails on the raw Qdrant timeout and flips the breaker to Open.
        var firstTry = await breaker.WarmUpAsync();
        firstTry.Should().BeFalse();

        var svc = new RagRetrievalService(
            db, breaker, keywordInner,
            new StaticEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var assistant = AiAssistant.Create(
            tenantId: tenantId, name: "t", description: null, systemPrompt: "x",
            provider: null, model: null, temperature: 0.2, maxTokens: 256,
            executionMode: AssistantExecutionMode.Chat, maxAgentSteps: 1, isActive: true);
        assistant.SetRagScope(AiRagScope.AllTenantDocuments, documents: []);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "qdrant", Array.Empty<RagHistoryMessage>(), default);

        ctx.DegradedStages.Should().Contain(s => s.StartsWith("vector-search"));
        ctx.Children.Should().ContainSingle(c => c.ChunkId != Guid.Empty);
    }

    private sealed class ThrowingVectorStoreForBreakerTest : IVectorStore
    {
        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => throw new TimeoutException("simulated qdrant outage");
    }

    private sealed class FixedKeywordSearchForBreakerTest : IKeywordSearchService
    {
        private readonly IReadOnlyList<KeywordSearchHit> _hits;
        public FixedKeywordSearchForBreakerTest(IReadOnlyList<KeywordSearchHit> hits) => _hits = hits;
        public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
            Guid t, string q, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => Task.FromResult(_hits);
    }

    private sealed class StaticEmbeddingService : IEmbeddingService
    {
        public Task<float[][]> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct,
            EmbeddingAttribution? attribution = null, AiRequestType? requestType = null)
            => Task.FromResult(inputs.Select(_ => new[] { 0.1f }).ToArray());

        public Task<int> GetVectorSizeAsync(CancellationToken ct) => Task.FromResult(1);
    }
}

internal static class CircuitBreakingVectorStoreWarmUp
{
    // Helper to trip the breaker before the real retrieval test.
    public static async Task<bool> WarmUpAsync(this CircuitBreakingVectorStore store)
    {
        try
        {
            await store.SearchAsync(Guid.NewGuid(), Array.Empty<float>(), null, 1, default);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

NOTE to implementer: the fakes `NoOpQueryRewriter`, `NoOpContextualQueryResolver`, `NoOpQuestionClassifier`, `NoOpReranker`, `NoOpNeighborExpander` already exist under `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/`. If a `NoOpReranker` is missing, look at existing `RagRetrievalServiceTestHarness.cs` for the reranker fake it uses and copy the same pattern.

If `IEmbeddingService` has additional required members beyond the two shown, mirror them with trivial implementations. Do not change `IEmbeddingService` itself.

- [ ] **Step 2: Run and confirm failures**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagRetrievalDegradationWithBreakerTests" --nologo`
Expected: possible compile errors around fake service surface area; fix them by mirroring existing fakes in the same folder. Once compiling, the assertion `DegradedStages.Should().Contain(...)` and `Children.Should().ContainSingle` must pass — if they don't, it means the end-to-end integration is broken and needs diagnosis before moving on.

- [ ] **Step 3: Run the full AI test suite**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai" --nologo`
Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/RagRetrievalDegradationWithBreakerTests.cs
git commit -m "test(ai): end-to-end degradation test with open Qdrant circuit"
```

---

## Task 11: Document configuration defaults in `appsettings`

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/appsettings.json`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`

- [ ] **Step 1: Add defaults to `appsettings.json`**

Edit `boilerplateBE/src/Starter.Api/appsettings.json`. Inside the `"AI": { "Rag": { ... } }` block, add:

```json
      "CircuitBreakers": {
        "Qdrant": {
          "Enabled": true,
          "MinimumThroughput": 10,
          "FailureRatio": 0.5,
          "BreakDurationMs": 30000
        },
        "PostgresFts": {
          "Enabled": true,
          "MinimumThroughput": 10,
          "FailureRatio": 0.5,
          "BreakDurationMs": 30000
        }
      }
```

(Place it adjacent to existing per-stage timeout settings so related keys stay together.)

- [ ] **Step 2: Mirror in `appsettings.Development.json`**

Edit `boilerplateBE/src/Starter.Api/appsettings.Development.json`. Add the identical `"CircuitBreakers": { ... }` block inside the `"AI": { "Rag": { ... } }` section. The dev file inherits from the base file, so you only need to include it if you want dev-only overrides; for now include the same defaults so the full shape of the config is documented.

- [ ] **Step 3: Smoke-test the API boots**

Run: `cd boilerplateBE && dotnet build`
Expected: `Build succeeded`.

Run: `cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http &`

Wait ~5 seconds, then:

Run: `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/health`
Expected: `200`.

Stop the server: `kill %1`

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Api/appsettings.json boilerplateBE/src/Starter.Api/appsettings.Development.json
git commit -m "chore(ai): document default circuit-breaker settings in appsettings"
```

---

## Task 12: Run the full backend test suite and summarise

- [ ] **Step 1: Run all tests**

Run: `cd boilerplateBE && dotnet test --nologo`
Expected: every test passes. Record the pass count (baseline 328 + N new tests).

- [ ] **Step 2: Verify observable success criteria from the spec**

Re-read the "Success criteria" section of `docs/superpowers/specs/2026-04-21-ai-module-plan-4b-6-retrieval-circuit-breaker-design.md`:

1. With Qdrant stopped, subsequent RAG turns return < 50 ms after the breaker trips. — Validated by unit tests asserting `inner.CallCount` does not increase after trip.
2. Recovery via half-open probe. — Validated by `Recovers_after_break_duration`.
3. Symmetrical behaviour for FTS. — Validated by `CircuitBreakingKeywordSearchTests.Trips_on_sustained_DbException_and_short_circuits`.
4. Negligible overhead when healthy. — Validated implicitly by happy-path tests still passing.
5. Full suite green. — Validated by step 1.

Mark each criterion in the spec as covered (no doc edit required; this is a mental check).

- [ ] **Step 3: Live QA note (optional)**

Per the project's post-feature testing workflow (`CLAUDE.md` → "Post-Feature Testing Workflow"), live QA for 4b-6 means: stop the Qdrant container, send an AI chat turn, observe the fast-fail plus `vector-search[0]` in the `DegradedStages` stage. Document this as a manual follow-up — do not block plan completion on it. Record the Grafana dashboard impact (new `rag.circuit.state_changes{service, state}` series) in the next observability report.

---

## Self-review checklist (already executed by the plan author)

- **Spec coverage** — every Scope decision row maps to a task: library (Task 1), targets (Tasks 6–7), breaker scope (Task 5 registry), failure signal + BrokenCircuitException handling (Task 8), trip policy + break duration + half-open (Task 5 tests), open-circuit outcome (Task 3 + Task 8), telemetry (Task 4 + Task 5), configuration (Task 3 + Task 11), injection sites (Task 9), architecture diagram and file map (all tasks).
- **Placeholders** — no TBD/TODO; every code block is complete and copy-pasteable.
- **Type consistency** — `RagCircuitBreakerRegistry.Qdrant` / `.PostgresFts` used identically across Tasks 5–7 and 9; `RagCircuitBreakerOptions`/`RagCircuitBreakerSettings` property names stable across Tasks 3, 5, 6, 7; `AiRagCircuitMetrics.MeterName` stable across Tasks 4 and 5; `RagStageOutcome.CircuitOpen = "circuit_open"` matches the tag string used in tests.
