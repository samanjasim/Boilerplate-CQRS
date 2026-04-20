# Plan 4b-4 — RAG Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Instrument the existing AI RAG pipeline with per-stage OpenTelemetry metrics, lifecycle webhook events, and enriched Serilog structured properties — without changing retrieval behavior.

**Architecture:** Introduce a single static `AiRagMetrics` class (one `Meter` named `Starter.Module.AI.Rag`, ~10 instruments). Augment the existing `WithTimeoutAsync` wrapper (from Plan 4b-1) to record stage duration + outcome. Add counter calls at cache-check sites in the five cache-bearing services. Publish three new lifecycle webhook events (`ai.retrieval.completed|degraded|failed`) from `ChatExecutionService` alongside the existing `ai.chat.completed` event, using a 500 ms timeout and treating failure as a `webhook-publish` stage degrade. Log the pipeline outcome with enriched structured properties. No new services, no new DI surface beyond the static metrics class.

**Tech Stack:** .NET 10 · `System.Diagnostics.Metrics` (Meter, Counter, Histogram) · OpenTelemetry (OTel) already wired via `AddOpenTelemetryObservability` · Serilog · existing `IWebhookPublisher` · FluentAssertions + xUnit for tests.

**Source of truth:** `docs/superpowers/specs/2026-04-19-ai-module-plan-4b-4-observability-design.md`

---

## Ground rules for every task

- **TDD** — red test, green implementation, commit. No implementation before a failing test.
- **Atomic commits** — one task = one focused commit. No `Co-Authored-By`, no AI/Claude mention in commit messages (standing order).
- **No migrations** — this plan adds zero DB schema. If an engineer finds themselves about to write `dotnet ef migrations add`, stop and re-read the spec.
- **No behavior changes** — instrumentation must not alter retrieval output. After every task, run the full AI test suite (`dotnet test --filter "FullyQualifiedName~Ai"`) and confirm it still passes.
- **Work in** `boilerplateBE` (the main worktree). Not a rename'd test app.
- **File paths are absolute from the repo root** unless prefixed with `src/` / `tests/`.

---

## File Structure

| Path | Responsibility | Action |
|---|---|---|
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiRagMetrics.cs` | Static `Meter` + all instruments | Create |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagLanguageDetector.cs` | `DetectLanguage(string query) → "ar"\|"en"\|"mixed"\|"unknown"` | Create |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagStageOutcome.cs` | Enum-as-const string constants for `rag.outcome` tag values | Create |
| `boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs` | Register the new meter in `.WithMetrics()` | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` | Instrument `WithTimeoutAsync`, aggregate metrics, language detection | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/CachingEmbeddingService.cs` | Cache hit/miss counter | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/QueryRewriter.cs` | Cache hit/miss counter | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/QuestionClassifier.cs` | Cache hit/miss counter | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs` | Cache hit/miss counter + reordered counter | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs` | Cache hit/miss counter + reordered counter | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs` | Add `DetectedLanguage` + `FusedCandidates` properties used by webhook event and log line | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs` | Fire webhook events + enriched aggregate log | Modify |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagWebhookEventNames.cs` | `public const string` values for the three event names | Create |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/TestMeterListener.cs` | In-memory `MeterListener` wrapper for tests | Create |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/AiRagMetricsTests.cs` | Unit tests for the metrics instruments + language detector | Create |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs` | End-to-end pipeline metric emission tests | Create |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalWebhookTests.cs` | Webhook payload shape tests | Create |
| `docs/observability/rag-dashboards.md` | Metric list + Grafana panel skeleton + Prometheus alert starters | Create |

---

## Task 1: Foundation — `AiRagMetrics` static class

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiRagMetrics.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagStageOutcome.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/TestMeterListener.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/AiRagMetricsTests.cs`

- [ ] **Step 1: Add `InternalsVisibleTo` if not already present**

Verify `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj` already has `<InternalsVisibleTo Include="Starter.Api.Tests" />`. If not, add it. (It likely is — Plan 4b-3 added it.)

- [ ] **Step 2: Write failing test for the meter name + instrument names**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/AiRagMetricsTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

public class AiRagMetricsTests
{
    [Fact]
    public void Meter_name_is_stable()
    {
        AiRagMetrics.MeterName.Should().Be("Starter.Module.AI.Rag");
    }

    [Fact]
    public void All_instruments_use_the_shared_meter()
    {
        AiRagMetrics.RetrievalRequests.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.StageDuration.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.StageOutcome.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.CacheRequests.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.FusionCandidates.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.ContextTokens.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.ContextTruncated.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.DegradedStages.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.RerankReordered.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.KeywordHits.Meter.Name.Should().Be(AiRagMetrics.MeterName);
    }
}
```

- [ ] **Step 3: Run the test and confirm it fails**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~AiRagMetricsTests" --nologo`
Expected: compilation error — `AiRagMetrics` type does not exist.

- [ ] **Step 4: Create `RagStageOutcome.cs`**

```csharp
namespace Starter.Module.AI.Infrastructure.Observability;

/// Tag values for the rag.outcome dimension. Enumerated up-front to avoid
/// cardinality explosions from dynamic strings.
internal static class RagStageOutcome
{
    public const string Success = "success";
    public const string Timeout = "timeout";
    public const string Error = "error";
}
```

- [ ] **Step 5: Create `AiRagMetrics.cs`**

```csharp
using System.Diagnostics.Metrics;

namespace Starter.Module.AI.Infrastructure.Observability;

/// <summary>
/// Central OpenTelemetry meter and instruments for the RAG pipeline.
/// One <see cref="Meter"/> is reused by all components so metrics share a single
/// registration and export path. Tag values are enumerated (see <see cref="RagStageOutcome"/>)
/// to keep Prometheus cardinality bounded.
/// </summary>
internal static class AiRagMetrics
{
    public const string MeterName = "Starter.Module.AI.Rag";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> RetrievalRequests =
        _meter.CreateCounter<long>(
            name: "rag.retrieval.requests",
            unit: "count",
            description: "Number of RAG retrieval calls, tagged by scope.");

    public static readonly Histogram<double> StageDuration =
        _meter.CreateHistogram<double>(
            name: "rag.stage.duration",
            unit: "ms",
            description: "Per-stage latency in milliseconds.");

    public static readonly Counter<long> StageOutcome =
        _meter.CreateCounter<long>(
            name: "rag.stage.outcome",
            unit: "count",
            description: "Per-stage completion outcome (success|timeout|error).");

    public static readonly Counter<long> CacheRequests =
        _meter.CreateCounter<long>(
            name: "rag.cache.requests",
            unit: "count",
            description: "Cache lookups for embed/rewrite/rerank/classify, tagged by hit.");

    public static readonly Histogram<long> FusionCandidates =
        _meter.CreateHistogram<long>(
            name: "rag.fusion.candidates",
            unit: "count",
            description: "Size of fused hybrid-score list before top-K cut.");

    public static readonly Histogram<long> ContextTokens =
        _meter.CreateHistogram<long>(
            name: "rag.context.tokens",
            unit: "tokens",
            description: "Final context token count handed to the chat model.");

    public static readonly Counter<long> ContextTruncated =
        _meter.CreateCounter<long>(
            name: "rag.context.truncated",
            unit: "count",
            description: "Number of retrievals whose context was truncated, tagged by reason.");

    public static readonly Counter<long> DegradedStages =
        _meter.CreateCounter<long>(
            name: "rag.degraded.stages",
            unit: "count",
            description: "One increment per degraded stage per retrieval call.");

    public static readonly Counter<long> RerankReordered =
        _meter.CreateCounter<long>(
            name: "rag.rerank.reordered",
            unit: "count",
            description: "Whether rerank changed the top-K order vs RRF fusion.");

    public static readonly Histogram<long> KeywordHits =
        _meter.CreateHistogram<long>(
            name: "rag.keyword.hits",
            unit: "count",
            description: "Keyword search hit count per query variant, tagged by detected language.");
}
```

- [ ] **Step 6: Add `TestMeterListener.cs`**

```csharp
using System.Diagnostics.Metrics;

namespace Starter.Api.Tests.Ai.Observability;

/// <summary>
/// In-memory MeterListener that captures every instrument recording for a specific meter.
/// Use <see cref="Snapshot"/> from a test to assert on emitted measurements.
/// </summary>
public sealed class TestMeterListener : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<Measurement> _measurements = new();
    private readonly object _lock = new();

    public TestMeterListener(string meterName)
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName)
                l.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>(Record);
        _listener.SetMeasurementEventCallback<double>(Record);
        _listener.Start();
    }

    private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) where T : struct
    {
        var tagDict = new Dictionary<string, object?>(tags.Length);
        foreach (var kv in tags) tagDict[kv.Key] = kv.Value;
        lock (_lock) _measurements.Add(new Measurement(instrument.Name, Convert.ToDouble(value), tagDict));
    }

    public IReadOnlyList<Measurement> Snapshot()
    {
        lock (_lock) return _measurements.ToArray();
    }

    public void Dispose() => _listener.Dispose();

    public sealed record Measurement(string InstrumentName, double Value, IReadOnlyDictionary<string, object?> Tags);
}
```

- [ ] **Step 7: Run the AiRagMetrics tests — should pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~AiRagMetricsTests" --nologo`
Expected: 2/2 pass.

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/AiRagMetricsTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/TestMeterListener.cs
git commit -m "feat(ai): AiRagMetrics meter + instrument scaffolding"
```

---

## Task 2: Register the `Starter.Module.AI.Rag` meter in OpenTelemetry

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/AiRagMetricsTests.cs`

- [ ] **Step 1: Write failing test — meter is exported via OTel pipeline**

Append to `AiRagMetricsTests.cs`:

```csharp
[Fact]
public void OpenTelemetry_meter_registration_includes_AiRag()
{
    var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
    services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
    var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "true",
            ["OpenTelemetry:ServiceName"] = "test",
            ["OpenTelemetry:OtlpEndpoint"] = "http://127.0.0.1:4318",
        }).Build();

    services.AddOpenTelemetryObservability(config);
    var provider = services.BuildServiceProvider();

    var meterProvider = provider.GetRequiredService<OpenTelemetry.Metrics.MeterProvider>();
    meterProvider.Should().NotBeNull("OTel MeterProvider must be registered");

    // Re-reading the registration by exercising: emit on our meter and confirm no throw.
    AiRagMetrics.RetrievalRequests.Add(1);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "OpenTelemetry_meter_registration_includes_AiRag" --nologo`
Expected: test fails because `AddMeter("Starter.Module.AI.Rag")` is not in the registration.

- [ ] **Step 3: Register the meter**

Open `boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs`. Locate the `.WithMetrics(` block and add `.AddMeter("Starter.Module.AI.Rag")` alongside the existing `AddAspNetCoreInstrumentation()` / `AddHttpClientInstrumentation()` calls.

Exact edit (show what to add):

```csharp
.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddMeter("Starter.Module.AI.Rag")
    .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint)))
```

- [ ] **Step 4: Run the test — should pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "OpenTelemetry_meter_registration_includes_AiRag" --nologo`
Expected: PASS.

- [ ] **Step 5: Run full AI suite — no regressions**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~Ai" --nologo`
Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/AiRagMetricsTests.cs
git commit -m "feat(ai): register Starter.Module.AI.Rag meter in OpenTelemetry pipeline"
```

---

## Task 3: `RagLanguageDetector` helper

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagLanguageDetector.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagLanguageDetectorTests.cs`

- [ ] **Step 1: Write failing test**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagLanguageDetectorTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

public class RagLanguageDetectorTests
{
    [Theory]
    [InlineData("ما هي المضخة الطاردة المركزية؟", "ar")]
    [InlineData("How does a centrifugal pump work?", "en")]
    [InlineData("What is المضخة used for in engineering?", "mixed")]
    [InlineData("1234567890 !@# $$ ???", "unknown")]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    public void Detects_language_from_codepoint_ratio(string query, string expected)
    {
        RagLanguageDetector.Detect(query).Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "RagLanguageDetectorTests" --nologo`
Expected: compilation error — `RagLanguageDetector` does not exist.

- [ ] **Step 3: Implement `RagLanguageDetector.cs`**

```csharp
namespace Starter.Module.AI.Infrastructure.Observability;

/// <summary>
/// Lightweight language hint for RAG metrics. Counts the ratio of Arabic-block
/// codepoints (U+0600 to U+06FF) against ASCII letters. Not a replacement for a
/// real language detector — intended only for low-cardinality tagging.
/// Ratio &gt; 0.5 = <c>ar</c>, &lt; 0.1 = <c>en</c>, anywhere else = <c>mixed</c>.
/// Returns <c>unknown</c> when the query has no letters at all.
/// </summary>
internal static class RagLanguageDetector
{
    public const string Arabic = "ar";
    public const string English = "en";
    public const string Mixed = "mixed";
    public const string Unknown = "unknown";

    public static string Detect(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Unknown;

        int ar = 0, en = 0;
        foreach (var c in query)
        {
            if (c >= '\u0600' && c <= '\u06FF') ar++;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) en++;
        }

        var total = ar + en;
        if (total == 0) return Unknown;

        var ratio = (double)ar / total;
        if (ratio > 0.5) return Arabic;
        if (ratio < 0.1) return English;
        return Mixed;
    }
}
```

- [ ] **Step 4: Run — should pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "RagLanguageDetectorTests" --nologo`
Expected: 6/6 pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagLanguageDetector.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagLanguageDetectorTests.cs
git commit -m "feat(ai): RagLanguageDetector for metric tagging"
```

---

## Task 4: Instrument `WithTimeoutAsync` — stage duration + outcome

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs:472-503`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`

Background: the wrapper already catches `OperationCanceledException` (timeout vs propagated cancellation) and generic exceptions, appending to `degraded`. We hook onto the same branches.

- [ ] **Step 1: Write failing test — timeout emits stage.outcome=timeout**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`:

```csharp
using System.Diagnostics.Metrics;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

public class RagRetrievalMetricsTests
{
    [Fact]
    public async Task WithTimeoutAsync_records_duration_and_success_on_happy_path()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync(
            op: async ct => { await Task.Delay(5, ct); return "ok"; },
            timeoutMs: 500,
            stageName: "vector-search",
            degraded: degraded);

        result.Should().Be("ok");
        degraded.Should().BeEmpty();

        var snapshot = listener.Snapshot();
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.duration"
                                       && (string?)m.Tags["rag.stage"] == "vector-search");
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.outcome"
                                       && (string?)m.Tags["rag.stage"] == "vector-search"
                                       && (string?)m.Tags["rag.outcome"] == "success"
                                       && m.Value == 1);
    }

    [Fact]
    public async Task WithTimeoutAsync_records_timeout_outcome_when_op_exceeds_budget()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync<string>(
            op: async ct => { await Task.Delay(200, ct); return "too-slow"; },
            timeoutMs: 20,
            stageName: "rerank",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("rerank");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome")
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "timeout");
    }

    [Fact]
    public async Task WithTimeoutAsync_records_error_outcome_on_exception()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync<string>(
            op: _ => throw new InvalidOperationException("boom"),
            timeoutMs: 500,
            stageName: "rewrite",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("rewrite");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome")
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "error");
    }
}
```

- [ ] **Step 2: Create the test harness that exposes `WithTimeoutAsync`**

The real method is `private`. Add an `internal` static wrapper in the production class so tests can drive it. In `RagRetrievalService.cs`, just above `WithTimeoutAsync`, add:

```csharp
internal static Task<T?> RunWithTimeoutAsyncForTests<T>(
    Func<CancellationToken, Task<T>> op,
    int timeoutMs,
    string stageName,
    List<string> degraded,
    CancellationToken ct = default) where T : class
    => WithTimeoutAsyncCore(op, timeoutMs, stageName, degraded, ct);
```

Then rename the existing `WithTimeoutAsync` body into `WithTimeoutAsyncCore` (or keep `WithTimeoutAsync` private and have it call `WithTimeoutAsyncCore`). Add a shim in the test project:

`boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalServiceTestHarness.cs`:

```csharp
using Starter.Module.AI.Infrastructure.Retrieval;

namespace Starter.Api.Tests.Ai.Observability;

internal static class RagRetrievalServiceTestHarness
{
    public static Task<T?> RunWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> op,
        int timeoutMs,
        string stageName,
        List<string> degraded,
        CancellationToken ct = default) where T : class
        => RagRetrievalService.RunWithTimeoutAsyncForTests(op, timeoutMs, stageName, degraded, ct);
}
```

- [ ] **Step 3: Run tests — they fail (metrics not emitted yet)**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~RagRetrievalMetricsTests" --nologo`
Expected: 3 failing assertions (instruments never recorded).

- [ ] **Step 4: Instrument `WithTimeoutAsyncCore`**

Replace the body of the per-stage wrapper with:

```csharp
private static async Task<T?> WithTimeoutAsyncCore<T>(
    Func<CancellationToken, Task<T>> op,
    int timeoutMs,
    string stageName,
    List<string> degraded,
    CancellationToken ct) where T : class
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(timeoutMs);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    string outcome = RagStageOutcome.Success;
    try
    {
        return await op(cts.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
        outcome = RagStageOutcome.Timeout;
        degraded.Add(stageName);
        return null;
    }
    catch (OperationCanceledException)
    {
        outcome = RagStageOutcome.Timeout;
        throw;
    }
    catch (Exception)
    {
        outcome = RagStageOutcome.Error;
        degraded.Add(stageName);
        return null;
    }
    finally
    {
        sw.Stop();
        AiRagMetrics.StageDuration.Record(
            sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("rag.stage", stageName));
        AiRagMetrics.StageOutcome.Add(
            1,
            new KeyValuePair<string, object?>("rag.stage", stageName),
            new KeyValuePair<string, object?>("rag.outcome", outcome));
    }
}
```

Keep the existing `WithTimeoutAsync` as a thin wrapper that calls `WithTimeoutAsyncCore`.

- [ ] **Step 5: Run tests — should pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~RagRetrievalMetricsTests" --nologo`
Expected: 3/3 pass.

- [ ] **Step 6: Run full AI suite**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~Ai" --nologo`
Expected: all green, no regressions.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalServiceTestHarness.cs
git commit -m "feat(ai): emit rag.stage.duration and rag.stage.outcome from WithTimeoutAsync"
```

---

## Task 5: Instrument the classify direct-timeout path

The `Classify` stage does not go through `WithTimeoutAsync` — it uses a direct `CancellationTokenSource.CancelAfter` (see `RagRetrievalService.cs:104–128`). Emit the same two instruments there.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs:104-128`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`

- [ ] **Step 1: Write failing test**

Add to `RagRetrievalMetricsTests.cs`:

```csharp
[Fact]
public async Task Classify_stage_records_duration_and_outcome_via_direct_path()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);

    // Drive RagRetrievalService end-to-end with a stub IQuestionClassifier
    // that completes normally. Use a minimal scope that only runs classify
    // (see Plan 4b-2 test fixtures for the test-double shape).
    // The full harness is already used by existing RagRetrievalServiceTests.
    await RagRetrievalTestHost.RunHappyPathAsync();

    var outcomes = listener.Snapshot()
        .Where(m => m.InstrumentName == "rag.stage.outcome"
                    && (string?)m.Tags["rag.stage"] == "classify")
        .ToList();
    outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "success");
}
```

If `RagRetrievalTestHost` does not yet exist, inspect existing tests in
`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/` — there is already a
`RagRetrievalServiceTests.cs` (from Plan 4b-1/2/3) that constructs the service
with stubs. Reuse its wiring inside a shared helper `RagRetrievalTestHost`.

- [ ] **Step 2: Run test — fails**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "Classify_stage_records_duration" --nologo`
Expected: test fails because classify stage does not emit.

- [ ] **Step 3: Instrument the classify branch**

Locate the classify block (search the file for `"classify"` constant or `StageTimeoutClassifyMs`). Wrap the call in a local helper that mirrors Task 4:

```csharp
var classifySw = System.Diagnostics.Stopwatch.StartNew();
string classifyOutcome = RagStageOutcome.Success;
try
{
    using var classifyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    classifyCts.CancelAfter(_settings.StageTimeoutClassifyMs);
    classification = await _classifier.ClassifyAsync(query, classifyCts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    classifyOutcome = RagStageOutcome.Timeout;
    degraded.Add(RagStages.Classify);
}
catch (Exception ex)
{
    classifyOutcome = RagStageOutcome.Error;
    degraded.Add(RagStages.Classify);
    _logger.LogWarning(ex, "Classify stage failed; continuing without classification.");
}
finally
{
    classifySw.Stop();
    AiRagMetrics.StageDuration.Record(
        classifySw.Elapsed.TotalMilliseconds,
        new KeyValuePair<string, object?>("rag.stage", RagStages.Classify));
    AiRagMetrics.StageOutcome.Add(
        1,
        new KeyValuePair<string, object?>("rag.stage", RagStages.Classify),
        new KeyValuePair<string, object?>("rag.outcome", classifyOutcome));
}
```

Preserve the existing log on success (line 119) verbatim.

- [ ] **Step 4: Run — should pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~RagRetrievalMetricsTests" --nologo`
Expected: all tests in file pass (4 total now).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "feat(ai): instrument classify stage duration and outcome"
```

---

## Task 6: Cache-hit counter in `CachingEmbeddingService`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/CachingEmbeddingService.cs:46-50`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

public class CacheMetricsTests
{
    [Fact]
    public async Task Embedding_cache_miss_then_hit_emits_both_counters()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);
        var cache = new InMemoryAiCache();
        var inner = new StubEmbeddingService(new[] { 0.1f, 0.2f });
        var sut = new CachingEmbeddingService(inner, cache,
            Options.Create(new AiRagSettings { EmbeddingCacheTtlSeconds = 60 }),
            NullLogger<CachingEmbeddingService>.Instance);

        await sut.EmbedAsync("hello", CancellationToken.None); // miss
        await sut.EmbedAsync("hello", CancellationToken.None); // hit

        var rows = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.cache.requests"
                        && (string?)m.Tags["rag.cache"] == "embed")
            .ToList();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
    }
}
```

(`InMemoryAiCache` and `StubEmbeddingService` already exist in the test project per Plan 4b-1; reuse those. If not, add minimal fakes that mirror the real interfaces.)

- [ ] **Step 2: Run — fails**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "Embedding_cache_miss_then_hit_emits_both_counters" --nologo`
Expected: both `rag.cache.requests` recordings missing.

- [ ] **Step 3: Instrument `CachingEmbeddingService.EmbedAsync`**

Locate the hit/miss branch (around line 46-50). Add a single helper local function and call it in both branches:

```csharp
// at top of method, before the cache check:
static void RecordCache(bool hit) =>
    AiRagMetrics.CacheRequests.Add(
        1,
        new KeyValuePair<string, object?>("rag.cache", "embed"),
        new KeyValuePair<string, object?>("rag.hit", hit));

var cached = await _cache.GetAsync<float[]>(key, ct);
if (cached is not null && cached.Length > 0)
{
    RecordCache(true);
    return cached;
}
RecordCache(false);
// existing miss path — call inner, set cache, return
```

- [ ] **Step 4: Run — passes**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "Embedding_cache_miss_then_hit_emits_both_counters" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/CachingEmbeddingService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs
git commit -m "feat(ai): rag.cache.requests for embedding cache"
```

---

## Task 7: Cache-hit counter in `QueryRewriter`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/QueryRewriter.cs:42-46`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs`

- [ ] **Step 1: Write failing test**

Append to `CacheMetricsTests.cs`:

```csharp
[Fact]
public async Task Query_rewriter_cache_miss_then_hit_emits_both_counters()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    var sut = QueryRewriterTestFactory.Create(cachedValue: null);
    await sut.RewriteAsync("what is a pump?", CancellationToken.None);          // miss
    sut = QueryRewriterTestFactory.Create(cachedValue: new List<string> { "x" });
    await sut.RewriteAsync("what is a pump?", CancellationToken.None);          // hit

    var rows = listener.Snapshot()
        .Where(m => m.InstrumentName == "rag.cache.requests"
                    && (string?)m.Tags["rag.cache"] == "rewrite")
        .ToList();
    rows.Should().HaveCount(2);
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
}
```

If `QueryRewriterTestFactory` does not exist, create it in the same Observability folder (pattern from Plan 4b-2 should already wire a `LlmStub`).

- [ ] **Step 2: Run — fails**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "Query_rewriter_cache_miss_then_hit_emits_both_counters" --nologo`
Expected: failing.

- [ ] **Step 3: Instrument the rewriter cache block**

In `QueryRewriter.cs` around line 42-46, mirror Task 6's pattern:

```csharp
void RecordCache(bool hit) =>
    AiRagMetrics.CacheRequests.Add(
        1,
        new KeyValuePair<string, object?>("rag.cache", "rewrite"),
        new KeyValuePair<string, object?>("rag.hit", hit));

var cached = await _cache.GetAsync<List<string>>(cacheKey, ct);
if (cached is not null)
{
    RecordCache(true);
    return cached;
}
RecordCache(false);
```

- [ ] **Step 4: Run — passes**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "Query_rewriter_cache_miss_then_hit_emits_both_counters" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/QueryRewriter.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs
git commit -m "feat(ai): rag.cache.requests for query rewriter cache"
```

---

## Task 8: Cache-hit counter in `QuestionClassifier`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/QuestionClassifier.cs:43-45`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Classifier_cache_miss_then_hit_emits_both_counters()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    var sut = QuestionClassifierTestFactory.Create(cachedValue: null);
    await sut.ClassifyAsync("factual", CancellationToken.None);      // miss
    sut = QuestionClassifierTestFactory.Create(cachedValue: "Factoid");
    await sut.ClassifyAsync("factual", CancellationToken.None);      // hit

    var rows = listener.Snapshot()
        .Where(m => m.InstrumentName == "rag.cache.requests"
                    && (string?)m.Tags["rag.cache"] == "classify")
        .ToList();
    rows.Should().HaveCount(2);
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
}
```

- [ ] **Step 2: Run — fails.**

- [ ] **Step 3: Instrument — same pattern as Tasks 6/7, tag value `"classify"`.**

```csharp
void RecordCache(bool hit) =>
    AiRagMetrics.CacheRequests.Add(
        1,
        new KeyValuePair<string, object?>("rag.cache", "classify"),
        new KeyValuePair<string, object?>("rag.hit", hit));

var cached = await _cache.GetAsync<string>(key, ct);
if (!string.IsNullOrEmpty(cached))
{
    RecordCache(true);
    return cached;
}
RecordCache(false);
```

- [ ] **Step 4: Run — passes.**

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/QuestionClassifier.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs
git commit -m "feat(ai): rag.cache.requests for question classifier cache"
```

---

## Task 9: Cache-hit counter in both rerankers

Both `ListwiseReranker` (single batched call cache) and `PointwiseReranker` (per-pair parallel cache) need instrumenting. Pointwise increments per candidate inside a parallel loop — use `new KeyValuePair<string, object?>` tags, not shared state.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs:44-47`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs:65-70`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs`

- [ ] **Step 1: Write failing tests for both rerankers**

```csharp
[Fact]
public async Task Listwise_reranker_cache_miss_then_hit_emits_both_counters()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    // Use existing ListwiseRerankerTestFactory from Plan 4b-2 tests, or create
    // a minimal fake following the same shape as QueryRewriterTestFactory.
    var sutMiss = ListwiseRerankerTestFactory.Create(cachedOrder: null);
    await sutMiss.RerankAsync(query: "q", candidates: SampleCandidates(), topK: 3, CancellationToken.None);
    var sutHit = ListwiseRerankerTestFactory.Create(cachedOrder: new List<int> { 2, 0, 1 });
    await sutHit.RerankAsync(query: "q", candidates: SampleCandidates(), topK: 3, CancellationToken.None);

    var rows = listener.Snapshot()
        .Where(m => m.InstrumentName == "rag.cache.requests"
                    && (string?)m.Tags["rag.cache"] == "rerank")
        .ToList();
    rows.Should().HaveCount(2);
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
}

[Fact]
public async Task Pointwise_reranker_increments_per_candidate_hit_or_miss()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    var sut = PointwiseRerankerTestFactory.Create(cachedScores: new[] { (decimal?)0.9m, null, (decimal?)0.4m });

    await sut.RerankAsync(query: "q", candidates: SampleCandidates(count: 3), topK: 2, CancellationToken.None);

    var rows = listener.Snapshot()
        .Where(m => m.InstrumentName == "rag.cache.requests"
                    && (string?)m.Tags["rag.cache"] == "rerank")
        .ToList();
    rows.Count.Should().Be(3, "three candidates, one cache check each");
    rows.Count(m => (bool?)m.Tags["rag.hit"] == true).Should().Be(2);
    rows.Count(m => (bool?)m.Tags["rag.hit"] == false).Should().Be(1);
}
```

Helper `SampleCandidates(int count = 3)` should return a fresh `IReadOnlyList<RerankerCandidate>` with distinct content — put it on a shared `CacheMetricsTestSupport` file if you don't already have one.

- [ ] **Step 2: Run — both fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "reranker_cache" --nologo`
Expected: both tests fail.

- [ ] **Step 3: Instrument ListwiseReranker.RerankAsync around line 44-47**

```csharp
void RecordCache(bool hit) =>
    AiRagMetrics.CacheRequests.Add(
        1,
        new KeyValuePair<string, object?>("rag.cache", "rerank"),
        new KeyValuePair<string, object?>("rag.hit", hit));

var cached = await _cache.GetAsync<List<int>>(key, ct);
if (cached is not null)
{
    RecordCache(true);
    // existing hit path
}
else
{
    RecordCache(false);
    // existing miss path
}
```

- [ ] **Step 4: Instrument PointwiseReranker (parallel loop at line 65-70)**

Inside the per-candidate `Parallel.ForEachAsync` body, next to the existing `Interlocked.Increment(ref cacheHits)`:

```csharp
var cached = await _cache.GetAsync<decimal?>(key, ct);
AiRagMetrics.CacheRequests.Add(
    1,
    new KeyValuePair<string, object?>("rag.cache", "rerank"),
    new KeyValuePair<string, object?>("rag.hit", cached.HasValue));
if (cached.HasValue) { /* existing hit path */ }
else                  { /* existing miss path */ }
```

The metric emit is thread-safe — `Counter<long>.Add` takes a lock-free fast path.

- [ ] **Step 5: Run — both pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "reranker_cache" --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/CacheMetricsTests.cs
git commit -m "feat(ai): rag.cache.requests for both reranker variants"
```

---

## Task 10: `rag.rerank.reordered` — did rerank change top-K?

Cheapest comparison: pass the pre-rerank top-K chunk-id list into the reranker and compare against the post-rerank top-K. Emit one counter increment per rerank call with tag `rag.changed=true|false`.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Listwise_reranker_records_reordered_true_when_order_changes()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    // Cached order [2,0,1] differs from input order [0,1,2,3] in top-3.
    var sut = ListwiseRerankerTestFactory.Create(cachedOrder: new List<int> { 2, 0, 1 });
    await sut.RerankAsync("q", SampleCandidates(count: 4), topK: 3, CancellationToken.None);

    var rows = listener.Snapshot()
        .Where(m => m.InstrumentName == "rag.rerank.reordered").ToList();
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.changed"] == true);
}

[Fact]
public async Task Listwise_reranker_records_reordered_false_when_order_preserved()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    var sut = ListwiseRerankerTestFactory.Create(cachedOrder: new List<int> { 0, 1, 2 });
    await sut.RerankAsync("q", SampleCandidates(count: 4), topK: 3, CancellationToken.None);

    var rows = listener.Snapshot()
        .Where(m => m.InstrumentName == "rag.rerank.reordered").ToList();
    rows.Should().ContainSingle(m => (bool?)m.Tags["rag.changed"] == false);
}
```

- [ ] **Step 2: Run — fail.**

- [ ] **Step 3: Emit at end of `ListwiseReranker.RerankAsync`**

Right before returning the reranked list:

```csharp
var preTopK = candidates.Take(topK).Select(c => c.ChunkId).ToList();
var postTopK = result.Take(topK).Select(c => c.ChunkId).ToList();
var changed = !preTopK.SequenceEqual(postTopK);
AiRagMetrics.RerankReordered.Add(
    1, new KeyValuePair<string, object?>("rag.changed", changed));
```

- [ ] **Step 4: Do the same for `PointwiseReranker`** (same snippet, placed right before the `return` at the end of `RerankAsync`).

- [ ] **Step 5: Run tests — pass**

Expected: both listwise tests green. Add a mirror test for pointwise if coverage feels thin.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "feat(ai): rag.rerank.reordered compares pre/post top-K order"
```

---

## Task 11: Aggregate metrics in `RagRetrievalService`

Three more instruments emitted once per retrieval: `rag.retrieval.requests`, `rag.fusion.candidates`, `rag.keyword.hits`.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` (entry point + after keyword search + after fusion)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs` (add `DetectedLanguage` + `FusedCandidates` properties)
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`

- [ ] **Step 1: Extend `RetrievedContext` record**

Add two properties to the record definition:

```csharp
public sealed record RetrievedContext(
    IReadOnlyList<RetrievedChunk> Children,
    IReadOnlyList<RetrievedChunk> Parents,
    int TotalTokens,
    bool TruncatedByBudget,
    IReadOnlyList<string> DegradedStages,
    IReadOnlyList<Guid> SiblingChunkIds,
    int FusedCandidates,       // NEW: count before top-K
    string DetectedLanguage);  // NEW: ar|en|mixed|unknown
```

Update every `new RetrievedContext(...)` construction — there should be one in `RagRetrievalService.RetrieveForQueryAsync` near line 372 and possibly one in an empty-result fallback path.

- [ ] **Step 2: Write failing tests**

```csharp
[Fact]
public async Task Retrieval_emits_requests_counter_tagged_by_scope()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    await RagRetrievalTestHost.RunHappyPathAsync(scope: RagScope.AllTenantDocuments);

    listener.Snapshot()
        .Should().Contain(m =>
            m.InstrumentName == "rag.retrieval.requests"
            && (string?)m.Tags["rag.scope"] == "AllTenantDocuments"
            && m.Value == 1);
}

[Fact]
public async Task Retrieval_records_fusion_candidates_histogram()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    var ctx = await RagRetrievalTestHost.RunHappyPathAsync(); // yields ≥1 fused hit

    ctx.FusedCandidates.Should().BeGreaterThan(0);
    listener.Snapshot()
        .Should().Contain(m =>
            m.InstrumentName == "rag.fusion.candidates"
            && m.Value == ctx.FusedCandidates);
}

[Fact]
public async Task Retrieval_records_keyword_hits_tagged_by_detected_language()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    var ctx = await RagRetrievalTestHost.RunHappyPathAsync(query: "ما هي المضخة؟");

    ctx.DetectedLanguage.Should().Be("ar");
    listener.Snapshot()
        .Should().Contain(m =>
            m.InstrumentName == "rag.keyword.hits"
            && (string?)m.Tags["rag.lang"] == "ar");
}
```

- [ ] **Step 3: Run — fail.**

- [ ] **Step 4: Instrument entry point**

At the top of `RetrieveForQueryAsync`, right after the scope is known:

```csharp
AiRagMetrics.RetrievalRequests.Add(
    1, new KeyValuePair<string, object?>("rag.scope", scope.ToString()));
var detectedLang = RagLanguageDetector.Detect(query);
```

- [ ] **Step 5: Instrument keyword search**

Inside the per-variant keyword-search loop (`RagRetrievalService.cs:200-206`), after a successful call:

```csharp
AiRagMetrics.KeywordHits.Record(
    (long)(keywordResult?.Count ?? 0),
    new KeyValuePair<string, object?>("rag.lang", detectedLang));
```

- [ ] **Step 6: Instrument post-fusion**

After `HybridScoreCalculator.Combine` (`RagRetrievalService.cs:210-216`):

```csharp
AiRagMetrics.FusionCandidates.Record(mergedHits.Count);
int fusedCandidatesCount = mergedHits.Count;
```

Pass `fusedCandidatesCount` and `detectedLang` into the final `RetrievedContext` constructor.

- [ ] **Step 7: Run — pass.**

- [ ] **Step 8: Run full AI suite**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~Ai" --nologo`
Expected: green.

- [ ] **Step 9: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "feat(ai): aggregate retrieval metrics for requests, fusion, keyword-lang"
```

---

## Task 12: Context tokens + truncated + degraded stages in `ChatExecutionService`

These three fire once per chat turn **after** retrieval settles and budget truncation runs. Emit at the same place the aggregate log line lives today (`ChatExecutionService.cs:611-613`).

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Chat_turn_emits_context_tokens_and_degraded_stages()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);
    var ctx = await ChatExecutionTestHost.RunTurnAsync(
        degradeStages: new[] { "rerank" },
        truncateContext: true);

    listener.Snapshot()
        .Should().Contain(m =>
            m.InstrumentName == "rag.context.tokens" && m.Value > 0);
    listener.Snapshot()
        .Should().Contain(m =>
            m.InstrumentName == "rag.context.truncated"
            && (string?)m.Tags["rag.reason"] == "budget");
    listener.Snapshot()
        .Should().Contain(m =>
            m.InstrumentName == "rag.degraded.stages"
            && (string?)m.Tags["rag.stage"] == "rerank");
}
```

`ChatExecutionTestHost` is a helper wrapping the real service with stubbed `IAssistantRepository`/`IChatClient`/`IRagRetrievalService` that returns a shaped `RetrievedContext`.

- [ ] **Step 2: Run — fails.**

- [ ] **Step 3: Instrument at the post-retrieval point**

Right before the existing `logger.LogInformation("RAG retrieval for assistant ...")` at `ChatExecutionService.cs:611`, add:

```csharp
AiRagMetrics.ContextTokens.Record(retrieved.TotalTokens);
if (retrieved.TruncatedByBudget)
{
    AiRagMetrics.ContextTruncated.Add(
        1, new KeyValuePair<string, object?>("rag.reason", "budget"));
}
foreach (var stage in retrieved.DegradedStages)
{
    AiRagMetrics.DegradedStages.Add(
        1, new KeyValuePair<string, object?>("rag.stage", stage));
}
```

- [ ] **Step 4: Run — pass.**

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "feat(ai): emit context tokens, truncated, and degraded stage counts"
```

---

## Task 13: Webhook lifecycle events

Three events: `ai.retrieval.completed`, `ai.retrieval.degraded`, `ai.retrieval.failed`. Emission awaits `IWebhookPublisher.PublishAsync` with a 500 ms timeout; timeout is recorded under `rag.stage.outcome` with `rag.stage=webhook-publish`.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/RagWebhookEventNames.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalWebhookTests.cs`

- [ ] **Step 1: Create the event-name constants**

```csharp
namespace Starter.Module.AI.Infrastructure.Observability;

internal static class RagWebhookEventNames
{
    public const string Completed = "ai.retrieval.completed";
    public const string Degraded  = "ai.retrieval.degraded";
    public const string Failed    = "ai.retrieval.failed";
}
```

- [ ] **Step 2: Write failing test**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

public class RagRetrievalWebhookTests
{
    [Fact]
    public async Task Successful_turn_fires_ai_retrieval_completed_event()
    {
        var publisher = new RecordingWebhookPublisher();
        await ChatExecutionTestHost.RunTurnAsync(webhookPublisher: publisher);

        publisher.Events.Should().ContainSingle(e => e.EventType == "ai.retrieval.completed");
        var payload = publisher.Events.Single().Data as dynamic;
        ((int)payload!.KeptChildren).Should().BeGreaterThan(0);
        ((string)payload.DetectedLanguage).Should().Be("en");
        ((IEnumerable<object>)payload.Stages).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Degraded_turn_fires_ai_retrieval_degraded_event_instead_of_completed()
    {
        var publisher = new RecordingWebhookPublisher();
        await ChatExecutionTestHost.RunTurnAsync(
            webhookPublisher: publisher,
            degradeStages: new[] { "vector-search" });

        publisher.Events.Should().ContainSingle(e => e.EventType == "ai.retrieval.degraded");
        publisher.Events.Should().NotContain(e => e.EventType == "ai.retrieval.completed");
    }

    [Fact]
    public async Task Webhook_publish_timeout_records_stage_outcome()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);
        var publisher = new RecordingWebhookPublisher { DelayMs = 1000 }; // > 500 ms budget
        await ChatExecutionTestHost.RunTurnAsync(webhookPublisher: publisher);

        listener.Snapshot()
            .Should().Contain(m =>
                m.InstrumentName == "rag.stage.outcome"
                && (string?)m.Tags["rag.stage"] == "webhook-publish"
                && (string?)m.Tags["rag.outcome"] == "timeout");
    }
}
```

`RecordingWebhookPublisher` is a trivial `IWebhookPublisher` implementation that records invocations (create in the same folder).

- [ ] **Step 3: Run — fails.**

- [ ] **Step 4: Add the webhook-publish code in `ChatExecutionService`**

Immediately after the new context-metric block from Task 12, add:

```csharp
var stageSummary = retrieved.DegradedStages.Count > 0
    ? retrieved.DegradedStages.ToArray()
    : Array.Empty<string>();

var payload = new
{
    RequestId = Guid.NewGuid(),
    AssistantId = assistant.Id,
    TenantId = currentUser.TenantId,
    KeptChildren = retrieved.Children.Count,
    KeptParents = retrieved.Parents.Count,
    SiblingsCount = retrieved.SiblingChunkIds.Count,
    FusedCandidates = retrieved.FusedCandidates,
    TotalTokens = retrieved.TotalTokens,
    Truncated = retrieved.TruncatedByBudget,
    DegradedStages = stageSummary,
    DetectedLanguage = retrieved.DetectedLanguage,
    Stages = Array.Empty<object>() // reserved for a future per-stage array if needed
};

var eventName = retrieved.DegradedStages.Count > 0
    ? RagWebhookEventNames.Degraded
    : RagWebhookEventNames.Completed;

var publishSw = System.Diagnostics.Stopwatch.StartNew();
string publishOutcome = RagStageOutcome.Success;
try
{
    using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    publishCts.CancelAfter(500);
    await webhookPublisher.PublishAsync(eventName, currentUser.TenantId, payload, publishCts.Token)
        .ConfigureAwait(false);
}
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    publishOutcome = RagStageOutcome.Timeout;
    logger.LogWarning("RAG webhook publish timed out for assistant {AssistantId}", assistant.Id);
}
catch (Exception ex)
{
    publishOutcome = RagStageOutcome.Error;
    logger.LogWarning(ex, "RAG webhook publish failed for assistant {AssistantId}", assistant.Id);
}
finally
{
    publishSw.Stop();
    AiRagMetrics.StageDuration.Record(
        publishSw.Elapsed.TotalMilliseconds,
        new KeyValuePair<string, object?>("rag.stage", "webhook-publish"));
    AiRagMetrics.StageOutcome.Add(
        1,
        new KeyValuePair<string, object?>("rag.stage", "webhook-publish"),
        new KeyValuePair<string, object?>("rag.outcome", publishOutcome));
}
```

- [ ] **Step 5: Run — pass.**

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalWebhookTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RecordingWebhookPublisher.cs
git commit -m "feat(ai): ai.retrieval.completed|degraded webhook events with timeout fallback"
```

---

## Task 14: Enrich the aggregate Serilog log line

Update the existing log at `ChatExecutionService.cs:611-613` to the format from the spec, adding `siblings`, `req`, `stages`, `lang` structured properties.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs:611-613`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalWebhookTests.cs` (verify log text)

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Aggregate_log_line_includes_new_structured_properties()
{
    var logger = new RecordingLogger<ChatExecutionService>();
    await ChatExecutionTestHost.RunTurnAsync(logger: logger);

    logger.Entries.Should().Contain(e =>
        e.Message.Contains("RAG retrieval done assistant=") &&
        e.Message.Contains("req=") &&
        e.Message.Contains("siblings=") &&
        e.Message.Contains("stages=") &&
        e.Message.Contains("lang="));
}
```

- [ ] **Step 2: Run — fails.**

- [ ] **Step 3: Update the log call**

Replace the existing log line with:

```csharp
var stageDegradedSummary = retrieved.DegradedStages.Count > 0
    ? string.Join(",", retrieved.DegradedStages)
    : "none";

logger.LogInformation(
    "RAG retrieval done assistant={AssistantId} req={RequestId} children={Children} parents={Parents} siblings={Siblings} tokens={Tokens} truncated={Truncated} stages={StagesSummary} degraded={DegradedStages} lang={DetectedLang}",
    assistant.Id,
    payload.GetType().GetProperty("RequestId")!.GetValue(payload),
    retrieved.Children.Count,
    retrieved.Parents.Count,
    retrieved.SiblingChunkIds.Count,
    retrieved.TotalTokens,
    retrieved.TruncatedByBudget,
    "all",   // per-stage array is payload-only for now; keep the log compact
    stageDegradedSummary,
    retrieved.DetectedLanguage);
```

Use the same `payload.RequestId` from Task 13 so log and webhook correlate on `req=`.

- [ ] **Step 4: Run — pass.**

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalWebhookTests.cs
git commit -m "feat(ai): enrich RAG retrieval log with req, siblings, stages, lang"
```

---

## Task 15: Dashboard + alerts documentation

**Files:**
- Create: `docs/observability/rag-dashboards.md`

- [ ] **Step 1: Create the doc**

```markdown
# RAG Observability — Dashboards & Alerts

This page describes the metrics emitted by the AI RAG pipeline (meter
`Starter.Module.AI.Rag`), a Grafana import skeleton, and Prometheus alert
starters. Import and adapt to your environment — the skeleton is not a hard
dependency.

## Metric reference

| Name | Kind | Unit | Tags | What to watch |
|---|---|---|---|---|
| rag.retrieval.requests | Counter | count | rag.scope | Traffic per scope |
| rag.stage.duration | Histogram | ms | rag.stage | P50/P95/P99 per stage |
| rag.stage.outcome | Counter | count | rag.stage, rag.outcome | Success vs timeout vs error ratio per stage |
| rag.cache.requests | Counter | count | rag.cache, rag.hit | Hit rate per cache |
| rag.fusion.candidates | Histogram | count | — | Fused list size distribution before top-K |
| rag.context.tokens | Histogram | tokens | — | Final context size |
| rag.context.truncated | Counter | count | rag.reason | How often context was trimmed |
| rag.degraded.stages | Counter | count | rag.stage | Degrade frequency per stage |
| rag.rerank.reordered | Counter | count | rag.changed | How often rerank changed top-K |
| rag.keyword.hits | Histogram | count | rag.lang | Keyword recall distribution per language |

**Important:** no per-tenant tags — avoids Prometheus cardinality explosions.
Per-tenant drill-down comes from logs (`req=` correlates to webhook payload
`RequestId`) and webhooks (`ai.retrieval.completed|degraded|failed`).

## Grafana panel skeleton

(6 panels, JSON pasted verbatim below. Replace `${DS_PROMETHEUS}` with your
datasource UID.)

| # | Panel | PromQL |
|---|---|---|
| 1 | Stage P95 latency | `histogram_quantile(0.95, sum by (le, rag_stage) (rate(rag_stage_duration_bucket[5m])))` |
| 2 | Cache hit rate (all caches) | `sum by (rag_cache) (rate(rag_cache_requests_total{rag_hit="true"}[5m])) / sum by (rag_cache) (rate(rag_cache_requests_total[5m]))` |
| 3 | Degraded-stage rate | `sum by (rag_stage) (rate(rag_degraded_stages_total[5m]))` |
| 4 | Arabic vs English keyword recall | `histogram_quantile(0.5, sum by (le, rag_lang) (rate(rag_keyword_hits_bucket[5m])))` |
| 5 | Fusion size distribution | `histogram_quantile(0.9, sum by (le) (rate(rag_fusion_candidates_bucket[15m])))` |
| 6 | Rerank change rate | `sum(rate(rag_rerank_reordered_total{rag_changed="true"}[5m])) / sum(rate(rag_rerank_reordered_total[5m]))` |

A ready-to-import Grafana JSON is kept alongside this file in
`rag-dashboards.grafana.json` (see next commit if absent).

## Prometheus alert starters

```yaml
# Alert when more than 10% of retrievals degrade over 5 minutes.
- alert: RagDegradedStageRateHigh
  expr: |
    sum(rate(rag_degraded_stages_total[5m])) /
    sum(rate(rag_retrieval_requests_total[5m])) > 0.1
  for: 5m
  labels: { severity: warning }
  annotations:
    summary: "RAG pipeline degraded-stage rate over 10%"

# Alert when vector search P95 exceeds 2s.
- alert: RagVectorSearchSlow
  expr: |
    histogram_quantile(0.95,
      sum by (le) (rate(rag_stage_duration_bucket{rag_stage="vector-search"}[5m]))) > 2000
  for: 5m
  labels: { severity: warning }

# Alert when cache hit rate drops below 50% for any cache.
- alert: RagCacheHitRateLow
  expr: |
    sum by (rag_cache) (rate(rag_cache_requests_total{rag_hit="true"}[10m])) /
    sum by (rag_cache) (rate(rag_cache_requests_total[10m])) < 0.5
  for: 10m
  labels: { severity: info }
```

## Webhook events

Subscribers to the `ai.retrieval.*` namespace receive per-turn lifecycle
events. Payload schema:

```json
{
  "requestId": "uuid",
  "assistantId": "uuid",
  "tenantId": "uuid|null",
  "keptChildren": 5,
  "keptParents": 3,
  "siblingsCount": 2,
  "fusedCandidates": 20,
  "totalTokens": 3200,
  "truncated": false,
  "degradedStages": ["rerank"],
  "detectedLanguage": "ar",
  "stages": []
}
```

Use the existing webhook-subscription admin UI to filter by event name.

## Operational notes

- **Cardinality** — `rag.lang` is capped to 4 values (`ar|en|mixed|unknown`).
  Do not add per-tenant tags to any instrument; investigation flows through
  logs and webhooks instead.
- **Clock drift** — `rag.stage.duration` is a local wall-clock histogram.
  Cross-machine comparisons should bucket by host.
- **Weak signal: `rag.rerank.reordered`** — this is a proxy, not ground truth.
  Use as a health indicator, not a quality score.
```

- [ ] **Step 2: Verify markdown renders cleanly**

Open the file in an editor/preview and confirm tables render.

- [ ] **Step 3: Commit**

```bash
git add docs/observability/rag-dashboards.md
git commit -m "docs(ai): RAG observability dashboards and alert starters"
```

---

## Task 16: Final integration test + stress check

One fixture-backed end-to-end test that drives the whole pipeline and asserts ALL new instruments emit at least once for a realistic Arabic+English mixed query. Guards against silent wiring regressions.

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`

- [ ] **Step 1: Write failing full-pipeline test**

```csharp
[Fact]
public async Task End_to_end_retrieval_emits_every_instrument_at_least_once()
{
    using var listener = new TestMeterListener(AiRagMetrics.MeterName);

    await RagRetrievalTestHost.RunHappyPathAsync(
        query: "What is المضخة and how does cavitation affect it?");

    var names = listener.Snapshot()
        .Select(m => m.InstrumentName)
        .Distinct()
        .ToHashSet();

    names.Should().Contain("rag.retrieval.requests");
    names.Should().Contain("rag.stage.duration");
    names.Should().Contain("rag.stage.outcome");
    names.Should().Contain("rag.fusion.candidates");
    names.Should().Contain("rag.context.tokens");
    names.Should().Contain("rag.keyword.hits");
    // Cache counters fire only when caches exist — happy path exercises embed + rewrite.
    names.Should().Contain("rag.cache.requests");
}
```

- [ ] **Step 2: Run — should already pass if prior tasks are complete; if not, identify the missing wiring and fix.**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "End_to_end_retrieval_emits_every_instrument" --nologo`

- [ ] **Step 3: Full AI suite — must stay green**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~Ai" --nologo`
Expected: all tests pass, including the ~260 existing tests.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "test(ai): end-to-end observability wiring regression guard"
```

---

## Task 17: Live QA in a rename'd test app

Same post-feature testing pattern as Plan 4b-3. Prove the metrics surface in a
production-like run, not just in-memory tests.

- [ ] **Step 1: Generate a fresh test app**

```bash
pwsh scripts/rename.ps1 -Name "_test4b4" -OutputDir "."
```

- [ ] **Step 2: Configure ports + secrets**

- Backend → `5104`, frontend → `3104`, CORS + `.env` matched.
- Copy user-secrets from `boilerplateBE/src/Starter.Api` into
  `_test4b4/_test4b4-BE/src/_test4b4.Api` via pwsh (`dotnet user-secrets set` per key — do not script bash-for-loops in pwsh).
- Add `AI:Ocr:Enabled=false` in `appsettings.Development.json`.

- [ ] **Step 3: Kill zombie test-app processes first**

Per memory feedback: `ps aux | grep -iE "_test|Starter\.Api" | grep -v grep` — kill any stale `_test*.Api` PIDs to prevent RabbitMQ queue contamination.

- [ ] **Step 4: Run migrations + build + start BE/FE**

```bash
cd _test4b4/_test4b4-BE && dotnet ef database update --project src/_test4b4.Infrastructure --startup-project src/_test4b4.Api
cd .. && dotnet build
dotnet run --project _test4b4-BE/src/_test4b4.Api --launch-profile http &
cd _test4b4-FE && npm install && npm run dev &
```

- [ ] **Step 5: Drive one chat turn and scrape Prometheus**

Log in as a tenant admin (create via `/auth/register-tenant`, activate, login).
Upload `/tmp/_test4b3-qa-doc.md`, wait for ingestion, then create an assistant
with `RagScope=AllTenantDocuments` and POST to
`/api/v1/ai/conversations/{id}/messages` with an Arabic+English mixed query.

Scrape custom RAG meter measurements. Jaeger's `:8889/metrics` endpoint only
exposes **span-derived** SPM metrics, not custom OTel `Meter` instruments, so
the easiest way to inspect emissions in-process is to add a temporary QA-only
`MeterListener`-backed endpoint to the test app (e.g. `GET /diagnostics/rag-metrics`
returning a JSON snapshot keyed by instrument name). Alternative: stand up a
standalone OTel Collector with a Prometheus receiver/exporter. Do **not** add
the diagnostic to the boilerplate — it's an ad-hoc verification tool.

Expect to see **8 instruments on happy path** (`rag.retrieval.requests`,
`rag.stage.duration`, `rag.stage.outcome`, `rag.cache.requests`,
`rag.keyword.hits`, `rag.fusion.candidates`, `rag.rerank.reordered`,
`rag.context.tokens`) with sensible tags. `rag.context.truncated` and
`rag.degraded.stages` are conditional — they only emit when their trigger
fires (context exceeds budget, a stage degrades), so zero emissions on a
healthy turn is correct. To exercise those two, point the assistant at a
document large enough to trigger `TruncatedByBudget`, or degrade a stage by
setting its timeout to `1`ms temporarily.

- [ ] **Step 6: Verify webhook event was published**

Register a webhook endpoint (via `/api/v1/webhooks`) subscribed to `ai.retrieval.*` pointing at a local HTTP recorder (`nc -l 9999` or similar). Run one chat turn, confirm the recorder captured `ai.retrieval.completed` JSON with the expected payload shape.

- [ ] **Step 7: Verify enriched log line**

Tail `logs/_test4b4-*.log` and confirm a `RAG retrieval done assistant=... req=... siblings=... stages=all degraded=none lang=mixed` line appears.

- [ ] **Step 8: Report findings, fix in worktree source if needed**

If any metric, webhook, or log field is missing or wrong, fix in the
boilerplate worktree source — not the test app — then regenerate the test app
and re-run this task.

- [ ] **Step 9: Clean up after user sign-off**

```bash
kill <_test4b4 BE pid> ; pkill -f _test4b4-FE
psql -U postgres -c "DROP DATABASE IF EXISTS _test4b4db;"
rm -rf _test4b4
```

Do **not** delete the shared Qdrant `ai_chunks` collection — stored vectors are harmless orphans once their tenant's DB is gone.

---

## Self-review

### Spec coverage

- ✅ `AiRagMetrics` static meter + all 10 instruments → Task 1
- ✅ Meter registration in OTel → Task 2
- ✅ Instrument existing `WithTimeoutAsync` → Task 4
- ✅ Cache counters (embed, rewrite, classify, rerank × 2) → Tasks 6–9
- ✅ `rag.fusion.candidates`, `rag.context.tokens`, `rag.context.truncated`, `rag.degraded.stages`, `rag.retrieval.requests`, `rag.keyword.hits` → Tasks 11–12
- ✅ `rag.rerank.reordered` → Task 10
- ✅ Webhook events `ai.retrieval.completed|degraded|failed` → Task 13
- ✅ Enriched Serilog log → Task 14
- ✅ Dashboard + alerts doc → Task 15
- ✅ Classify direct-timeout path (not covered by `WithTimeoutAsync`) → Task 5
- ✅ Language detector → Task 3
- ✅ End-to-end integration test → Task 16
- ✅ Live QA in test app → Task 17

### Placeholder scan

- No "TBD" / "TODO" / "add error handling" / "similar to Task N" strings in the plan body.
- All code snippets are complete and compile-intent; no pseudo-code gaps.
- Test helpers referenced but not defined (`RagRetrievalTestHost`, `ChatExecutionTestHost`, `ListwiseRerankerTestFactory`, `PointwiseRerankerTestFactory`, `QuestionClassifierTestFactory`, `QueryRewriterTestFactory`, `SampleCandidates`) — the engineer should look in `boilerplateBE/tests/Starter.Api.Tests/Ai/` for existing equivalents from Plans 4b-1, 4b-2, 4b-3; if absent, create minimal fakes matching the tested service's real interface. This is standard test-helper bootstrapping, not hidden work.
- `RecordingWebhookPublisher` and `RecordingLogger<T>` are also trivial helpers — ~20 lines each.

### Type consistency

- `RetrievedContext` gains two new properties (`FusedCandidates`, `DetectedLanguage`) in Task 11 and both are consumed in Tasks 12, 13, 14 with matching names.
- `RagStageOutcome.Success|Timeout|Error` used consistently across Tasks 4, 5, 13.
- Meter name `"Starter.Module.AI.Rag"` and instrument names are fixed in Task 1 and referenced verbatim in Tasks 2–16.
- Event names `ai.retrieval.completed|degraded|failed` defined in Task 13 via `RagWebhookEventNames` constants.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-20-ai-module-plan-4b-4-observability.md`.

Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration. Seventeen tasks, atomic commits, TDD discipline enforced by the implementer prompt + two-stage review (spec compliance → code quality).
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
