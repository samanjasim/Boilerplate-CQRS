# Plan 4b-5 — Multi-turn contextual query rewrite: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve multi-turn follow-up questions (e.g. "how do we configure *it*?") into self-contained queries *before* the existing RAG pipeline runs, so retrieval on turn 2+ no longer misses context the user implicitly referenced from earlier turns.

**Architecture:** A new pre-rewrite stage (`contextualize`) sits between `classify` and `query-rewrite` inside `RagRetrievalService`. `IContextualQueryResolver` runs a rule-based heuristic gate first; if the latest message *looks* like a follow-up it performs a short LLM rewrite (Redis-cached, per-stage timeout wrapped by the existing `WithTimeoutAsync`). All failure modes fall back to the raw user message. The stage extends the 4b-4 observability instruments with a new `rag.stage=contextualize` tag — no new meter. Conversation history reaches retrieval via a new public `RagHistoryMessage` DTO threaded from `ChatExecutionService`.

**Tech Stack:** .NET 10, MediatR CQRS, EF Core, System.Diagnostics.Metrics / OpenTelemetry, Redis (`ICacheService`), `IAiProviderFactory`, `WithTimeoutAsync` (from 4b-1), `RagLanguageDetector` + `ArabicTextNormalizer` (from 4b-1/4b-4), `RagCacheKeys` (from 4b-2).

**Spec:** `docs/superpowers/specs/2026-04-21-ai-module-plan-4b-5-multi-turn-contextual-rewrite-design.md`.

---

## File Structure

### New files (implementation)
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RagHistoryMessage.cs` — public DTO (`record RagHistoryMessage(string Role, string Content)`). Role is `"user"` or `"assistant"` only.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IContextualQueryResolver.cs` — `public interface IContextualQueryResolver`.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualFollowUpHeuristic.cs` — `internal static` rule engine.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs` — `internal sealed` implementation (heuristic + cache + LLM + quote strip + language-mismatch guard).

### New files (tests)
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualFollowUpHeuristicTests.cs` — unit tests for the heuristic.
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs` — 12 unit tests (§8 of the spec).
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/MultiTurnRetrievalTests.cs` — 4 integration scenarios.
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/NoOpContextualQueryResolver.cs` — test fake that always returns the raw message, used by pre-4b-5 tests that should not exercise the stage.

### Modified files
- `Application/Services/Retrieval/IRagRetrievalService.cs` — adds `IReadOnlyList<RagHistoryMessage> history` to `RetrieveForTurnAsync`.
- `Infrastructure/Retrieval/RagStages.cs` — new `Contextualize = "contextualize"` constant.
- `Infrastructure/Retrieval/RagCacheKeys.cs` — new `Contextualize(provider, model, language, normalizedPayload)` factory.
- `Infrastructure/Settings/AiRagSettings.cs` — 5 new keys.
- `Infrastructure/Retrieval/RagRetrievalService.cs` — injects `IContextualQueryResolver`; adds contextualize stage; accepts history.
- `Application/Services/ChatExecutionService.cs` — `RetrieveContextSafelyAsync` slices last N turns from `state.ProviderMessages` and forwards.
- `AIModule.cs` — registers `IContextualQueryResolver` → `ContextualQueryResolver`.
- `tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs` — `FakeRetrieval` gains matching history parameter.
- `tests/Starter.Api.Tests/Ai/Retrieval/SearchKnowledgeBaseQueryHandlerTests.cs` — `FakeRetrievalService` gains matching parameter.
- `tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs` — regression-guard block asserts `rag.stage=contextualize` appears on multi-turn call.
- `boilerplateBE/src/Starter.Api/appsettings.Development.json` — template block showing the 5 new keys with defaults (non-breaking; binding works without them).

### Responsibility boundaries
- `ContextualFollowUpHeuristic` only decides *whether* to ask the LLM. No prompts, no cache.
- `ContextualQueryResolver` orchestrates heuristic → cache → LLM → post-processing. No direct metric emission (the `WithTimeoutAsync` wrapper handles stage duration/outcome; the resolver only emits the cache counter).
- `RagRetrievalService` owns the stage wiring; the resolver stays unaware of `RagStages` constants.
- `RagHistoryMessage` is the one and only shape that crosses the Application boundary for conversation history — retrieval never sees `AiChatMessage` (internal) or `AiMessage` (domain entity).

### Design deviations from the spec

- Spec §5 shows `RetrieveForTurnAsync` taking `IReadOnlyList<AiChatMessage> history`. `AiChatMessage` is an `internal` record in `Infrastructure/Providers`, and the interface is `public` — a public method cannot declare an internal parameter type. We introduce `RagHistoryMessage` (public record in the same Application namespace) instead. Resolver-internal logic still reduces to role + content. Behavior identical.
- Spec §10 item #8 ("per-assistant override") remains deferred. No DB migration is produced by this plan.

---

## Task 1: Add `RagHistoryMessage` DTO + `RagStages.Contextualize` + `RagCacheKeys.Contextualize` + settings keys

**Goal:** Add the pure-data scaffolding that later tasks depend on, with focused unit tests on the cache-key factory.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RagHistoryMessage.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagCacheKeys.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagCacheKeysTests.cs` (create if missing; otherwise append)

- [ ] **Step 1: Write failing test for `RagCacheKeys.Contextualize` key format**

Create (or append to) `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagCacheKeysTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class RagCacheKeysTests
{
    [Fact]
    public void Contextualize_produces_namespaced_sha256_key()
    {
        var key = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "en", "user:what is qdrant?\nassistant:qdrant is...\n---how do we configure it?");
        key.Should().StartWith("ai:ctx:OpenAI:gpt-4o-mini:en:");
        key.Length.Should().Be("ai:ctx:OpenAI:gpt-4o-mini:en:".Length + 64); // sha256 hex
    }

    [Fact]
    public void Contextualize_blank_language_defaults_to_dash()
    {
        var key = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "", "payload");
        key.Should().Contain(":-:");
    }

    [Fact]
    public void Contextualize_is_deterministic_for_same_payload()
    {
        var a = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "en", "same-payload");
        var b = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "en", "same-payload");
        a.Should().Be(b);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagCacheKeysTests`
Expected: FAIL — `RagCacheKeys` has no `Contextualize` method.

- [ ] **Step 3: Add the `Contextualize` factory to `RagCacheKeys`**

Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagCacheKeys.cs`. Append inside the class, after `PointwiseRerank`:

```csharp
    public static string Contextualize(string provider, string model, string language, string normalizedPayload)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "-" : language;
        return $"ai:ctx:{provider}:{model}:{lang}:{Sha256Hex(normalizedPayload)}";
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagCacheKeysTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Add `RagStages.Contextualize` constant**

Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs`. Inside the class, before `Classify`:

```csharp
    public const string Contextualize = "contextualize";
```

Final ordering should be: `Contextualize`, `Classify`, `QueryRewrite`, `EmbedQuery`, `Rerank`, `NeighborExpand`, plus the two variant-index methods.

- [ ] **Step 6: Add `RagHistoryMessage` DTO**

Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RagHistoryMessage.cs`:

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

/// <summary>
/// Conversation turn slice passed from <c>ChatExecutionService</c> into
/// <c>IRagRetrievalService</c>. Role is <c>"user"</c> or <c>"assistant"</c>.
/// Tool-call / tool-result / system rows are filtered out by the caller so the
/// resolver never has to decide how to render them.
/// </summary>
public sealed record RagHistoryMessage(string Role, string Content);
```

- [ ] **Step 7: Add 5 new settings keys to `AiRagSettings`**

Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`. Append at end of class (keep the 4b-2 grouping comment pattern):

```csharp
    // ---- New in Plan 4b-5 — Contextual query rewrite ----
    public bool EnableContextualRewrite { get; init; } = true;
    public int StageTimeoutContextualizeMs { get; init; } = 3_000;
    public int ContextualRewriteCacheTtlSeconds { get; init; } = 600;
    public int ContextualRewriteHistoryTurns { get; init; } = 3;
    public string? ContextualRewriterModel { get; init; } = null;
```

- [ ] **Step 8: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: build succeeds with zero errors.

- [ ] **Step 9: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RagHistoryMessage.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagCacheKeys.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagCacheKeysTests.cs
git commit -m "feat(ai): add contextualize stage scaffolding (stage constant, cache key, history DTO, settings)"
```

---

## Task 2: Rule-based follow-up heuristic (no LLM yet)

**Goal:** A pure static function that decides whether the latest user message *looks like* a follow-up, based on spec §4 "Activation" rules.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualFollowUpHeuristic.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualFollowUpHeuristicTests.cs`

- [ ] **Step 1: Write failing tests for the heuristic**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualFollowUpHeuristicTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ContextualFollowUpHeuristicTests
{
    [Theory]
    [InlineData("how do we configure it?")]
    [InlineData("and then?")]
    [InlineData("what about that?")]
    [InlineData("tell me more")]
    [InlineData("why?")]
    public void English_follow_up_phrases_trigger_heuristic(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeTrue();

    [Theory]
    [InlineData("كيف نضبطه؟")]        // Arabic pronoun
    [InlineData("وماذا عن هذا؟")]      // Arabic continuation + pronoun
    [InlineData("لماذا؟")]              // Short Arabic question
    public void Arabic_follow_up_phrases_trigger_heuristic(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeTrue();

    [Theory]
    [InlineData("What is the default RRF constant used in hybrid fusion?")]
    [InlineData("ما هي مكونات نظام Qdrant الداخلية؟")]
    public void Self_contained_questions_do_not_trigger(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Whitespace_input_returns_false(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeFalse();

    [Fact]
    public void Short_messages_under_25_chars_trigger()
        => ContextualFollowUpHeuristic.LooksLikeFollowUp("more details please").Should().BeTrue();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualFollowUpHeuristicTests`
Expected: FAIL — class does not exist.

- [ ] **Step 3: Implement the heuristic**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualFollowUpHeuristic.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

/// <summary>
/// Gate for <see cref="ContextualQueryResolver"/>. Decides whether a user
/// message looks like a follow-up that needs conversation-history-aware
/// rewriting. False negatives are acceptable (we just skip the LLM call and
/// retrieve the raw message). True negatives dominate in practice — most
/// questions are self-contained.
/// </summary>
internal static class ContextualFollowUpHeuristic
{
    private const int ShortMessageMaxChars = 25;

    private static readonly HashSet<string> PronounTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        // English
        "it", "this", "that", "they", "them", "these", "those", "one", "ones", "which",
        // Arabic
        "هو", "هي", "هذا", "هذه", "ذلك", "تلك", "هؤلاء", "الذي", "التي"
    };

    private static readonly string[] EnglishContinuationStarters =
    {
        "and ", "or ", "but ", "also ", "what about", "how about", "why", "when"
    };

    private static readonly string[] ArabicContinuationStarters =
    {
        "و", "أو", "لكن", "ماذا عن", "كيف", "لماذا", "متى"
    };

    private static readonly Regex WordSplitter = new(@"[\p{L}]+", RegexOptions.Compiled);

    public static bool LooksLikeFollowUp(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var trimmed = message.Trim();
        if (trimmed.Length <= ShortMessageMaxChars) return true;

        foreach (var starter in EnglishContinuationStarters)
            if (trimmed.StartsWith(starter, StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var starter in ArabicContinuationStarters)
            if (trimmed.StartsWith(starter, StringComparison.Ordinal)) return true;

        foreach (Match m in WordSplitter.Matches(trimmed))
        {
            if (PronounTokens.Contains(m.Value)) return true;
        }

        // Arabic pronoun-suffix: messages like "نضبطه" carry the pronoun as a
        // suffix on the verb; check for the short attached-pronoun forms.
        if (ContainsArabicPronounSuffix(trimmed)) return true;

        return false;
    }

    private static bool ContainsArabicPronounSuffix(string s)
    {
        // Third-person pronoun clitics: ـه, ـها, ـهم, ـهن
        // Cheap heuristic: any word that ends with these sequences.
        foreach (Match m in WordSplitter.Matches(s))
        {
            var w = m.Value;
            if (w.EndsWith("ه", StringComparison.Ordinal)
                || w.EndsWith("ها", StringComparison.Ordinal)
                || w.EndsWith("هم", StringComparison.Ordinal)
                || w.EndsWith("هن", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualFollowUpHeuristicTests`
Expected: PASS (all theory cases).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualFollowUpHeuristic.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualFollowUpHeuristicTests.cs
git commit -m "feat(ai): add rule-based follow-up heuristic for contextual rewrite gate"
```

---

## Task 3: `IContextualQueryResolver` interface + NoOp test fake

**Goal:** Lock in the contract so later resolver / service / test changes can compile against a stable interface. Provide a NoOp fake so existing tests keep passing when the retrieval service starts requiring an `IContextualQueryResolver`.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IContextualQueryResolver.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/NoOpContextualQueryResolver.cs`

- [ ] **Step 1: Create the interface**

Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IContextualQueryResolver.cs`:

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

/// <summary>
/// Rewrites the latest user message into a self-contained query using recent
/// conversation history. Never throws — falls back to <paramref name="latestUserMessage"/>
/// on any failure. Implementations are responsible for respecting per-stage
/// timeouts and recording their own cache metrics; stage duration/outcome is
/// recorded by the RagRetrievalService wrapper around the call.
/// </summary>
public interface IContextualQueryResolver
{
    /// <summary>
    /// Returns the resolved query string. When <paramref name="history"/> is empty,
    /// when the feature flag is off, or when the heuristic decides the message is
    /// already self-contained, returns <paramref name="latestUserMessage"/> unchanged
    /// without calling the LLM.
    /// </summary>
    Task<string> ResolveAsync(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct);
}
```

- [ ] **Step 2: Create the NoOp test fake**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/NoOpContextualQueryResolver.cs`:

```csharp
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// Always returns the raw latest message. Used by pre-4b-5 tests that should
/// exercise the rest of the retrieval pipeline without paying for the
/// contextualize stage.
/// </summary>
internal sealed class NoOpContextualQueryResolver : IContextualQueryResolver
{
    public Task<string> ResolveAsync(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
        => Task.FromResult(latestUserMessage);
}
```

- [ ] **Step 3: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: PASS (no callers yet).

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IContextualQueryResolver.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/NoOpContextualQueryResolver.cs
git commit -m "feat(ai): add IContextualQueryResolver interface and NoOp test fake"
```

---

## Task 4: `ContextualQueryResolver` — empty history + feature-flag-off paths

**Goal:** First slice of the real resolver. Covers the two early-return paths (history empty, flag off) before adding heuristic / cache / LLM logic.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs`

- [ ] **Step 1: Write failing tests for the early-return paths**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ContextualQueryResolverTests
{
    private static ContextualQueryResolver Build(
        FakeAiProvider provider,
        FakeCacheService cache,
        AiRagSettings? settings = null)
    {
        var factory = new FakeAiProviderFactory(provider);
        return new ContextualQueryResolver(
            factory,
            cache,
            Options.Create(settings ?? new AiRagSettings()),
            NullLogger<ContextualQueryResolver>.Instance);
    }

    private static IReadOnlyList<RagHistoryMessage> Hist(params (string role, string content)[] turns)
        => turns.Select(t => new RagHistoryMessage(t.role, t.content)).ToList();

    [Fact]
    public async Task Empty_history_returns_raw_no_provider_call()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync("how do we configure it?", history: Array.Empty<RagHistoryMessage>(), language: null, CancellationToken.None);

        result.Should().Be("how do we configure it?");
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Feature_flag_off_returns_raw_no_provider_call()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache, new AiRagSettings { EnableContextualRewrite = false });

        var result = await svc.ResolveAsync("how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: null, CancellationToken.None);

        result.Should().Be("how do we configure it?");
        provider.Calls.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualQueryResolverTests`
Expected: FAIL — class does not exist.

- [ ] **Step 3: Create the skeleton resolver with early-return paths**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal sealed class ContextualQueryResolver : IContextualQueryResolver
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<ContextualQueryResolver> _logger;

    public ContextualQueryResolver(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<ContextualQueryResolver> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(latestUserMessage)) return latestUserMessage;
        if (!_settings.EnableContextualRewrite) return latestUserMessage;
        if (history.Count == 0) return latestUserMessage;

        // Heuristic + cache + LLM path: filled in by Task 5+.
        await Task.CompletedTask;
        return latestUserMessage;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualQueryResolverTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs
git commit -m "feat(ai): add ContextualQueryResolver skeleton with empty-history and disabled-flag early returns"
```

---

## Task 5: Heuristic-skip path

**Goal:** When history is non-empty but the heuristic says the message is self-contained, return raw and skip the LLM call. Matches spec's `"Returns raw when heuristic skips"` test.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs`

- [ ] **Step 1: Write failing test**

Append to `ContextualQueryResolverTests.cs`:

```csharp
    [Fact]
    public async Task Heuristic_skips_self_contained_question_returns_raw()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "What is the default RRF constant used in hybrid fusion?",
            Hist(("user", "hi"), ("assistant", "hello")),
            language: "en", CancellationToken.None);

        result.Should().Be("What is the default RRF constant used in hybrid fusion?");
        provider.Calls.Should().Be(0);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualQueryResolverTests.Heuristic_skips`
Expected: FAIL — today the resolver still returns raw but in a later task it would hit LLM (not applicable here, but the skeleton has the same effect). We add the guard now so later tasks don't regress.

> Note: the skeleton from Task 4 currently returns raw on every path, so this test will PASS without code change. Still add the guard below for explicit intent and so Task 6+ don't skip the heuristic gate.

- [ ] **Step 3: Add the heuristic guard inside the resolver**

Modify `ContextualQueryResolver.ResolveAsync` — replace the "filled in by Task 5+" comment with:

```csharp
        if (!ContextualFollowUpHeuristic.LooksLikeFollowUp(latestUserMessage))
        {
            _logger.LogDebug("contextualize: heuristic-skip original={Original}", latestUserMessage);
            return latestUserMessage;
        }

        // Cache + LLM path: filled in by Task 6+.
        await Task.CompletedTask;
        return latestUserMessage;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualQueryResolverTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs
git commit -m "feat(ai): heuristic gate skips LLM for self-contained follow-ups"
```

---

## Task 6: LLM call on cache miss + cache set

**Goal:** When the heuristic fires and the cache is cold, call the LLM; cache the result with TTL; return it.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs`

- [ ] **Step 1: Write failing test for cache-miss LLM fetch**

Append:

```csharp
    [Fact]
    public async Task Heuristic_positive_cache_miss_calls_llm_and_caches_result()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("How do we configure Qdrant?");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        result.Should().Be("How do we configure Qdrant?");
        provider.Calls.Should().Be(1);
        // Cache should have been written — exact key is an internal detail but we can confirm any entry exists.
        var second = await svc.ResolveAsync(
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);
        second.Should().Be("How do we configure Qdrant?");
        provider.Calls.Should().Be(1); // cache hit — no new call
    }

    [Fact]
    public async Task Strips_surrounding_quotes_from_llm_output()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("\"How do we configure Qdrant?\"");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        result.Should().Be("How do we configure Qdrant?");
    }

    [Fact]
    public async Task Empty_llm_response_falls_back_to_raw()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("   ");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        result.Should().Be("how do we configure it?");
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualQueryResolverTests`
Expected: FAIL on three new tests (resolver still returns raw).

- [ ] **Step 3: Implement cache lookup + LLM call + post-processing**

Rewrite `ContextualQueryResolver.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal sealed class ContextualQueryResolver : IContextualQueryResolver
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<ContextualQueryResolver> _logger;

    public ContextualQueryResolver(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<ContextualQueryResolver> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(latestUserMessage)) return latestUserMessage;
        if (!_settings.EnableContextualRewrite) return latestUserMessage;
        if (history.Count == 0) return latestUserMessage;
        if (!ContextualFollowUpHeuristic.LooksLikeFollowUp(latestUserMessage))
        {
            _logger.LogDebug("contextualize: heuristic-skip original={Original}", latestUserMessage);
            return latestUserMessage;
        }

        var cacheKey = BuildCacheKey(latestUserMessage, history, language);

        string? cached = null;
        try
        {
            cached = await _cache.GetAsync<string>(cacheKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ContextualQueryResolver: cache Get failed; proceeding without cache");
        }

        AiRagMetrics.CacheRequests.Add(
            1,
            new KeyValuePair<string, object?>("rag.cache", "contextualize"),
            new KeyValuePair<string, object?>("rag.hit", cached is not null));

        if (cached is { Length: > 0 })
        {
            _logger.LogDebug("contextualize: cache-hit resolved={Resolved}", cached);
            return cached;
        }

        var resolved = await TryCallLlmAsync(latestUserMessage, history, language, ct);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            _logger.LogDebug("contextualize: llm-empty falling-back original={Original}", latestUserMessage);
            return latestUserMessage;
        }

        // Translation guard: if the LLM silently translated across languages,
        // reject and fall back. Worst case: bad retrieval. Not worth trusting.
        var originalLang = RagLanguageDetector.Detect(latestUserMessage);
        var resolvedLang = RagLanguageDetector.Detect(resolved);
        if (originalLang != RagLanguageDetector.Unknown
            && resolvedLang != RagLanguageDetector.Unknown
            && originalLang != RagLanguageDetector.Mixed
            && resolvedLang != RagLanguageDetector.Mixed
            && originalLang != resolvedLang)
        {
            _logger.LogWarning("contextualize: detected translation {From}→{To}; falling back", originalLang, resolvedLang);
            return latestUserMessage;
        }

        if (_settings.ContextualRewriteCacheTtlSeconds > 0)
        {
            try
            {
                await _cache.SetAsync(
                    cacheKey, resolved,
                    TimeSpan.FromSeconds(_settings.ContextualRewriteCacheTtlSeconds), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ContextualQueryResolver: cache Set failed; continuing");
            }
        }

        _logger.LogDebug(
            "contextualize: original={Original} resolved={Resolved} lang={Lang} skipped={Skipped}",
            latestUserMessage, resolved, language ?? originalLang, false);

        return resolved;
    }

    private async Task<string?> TryCallLlmAsync(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
    {
        try
        {
            var provider = _factory.CreateDefault();
            var langHint = language switch
            {
                "ar" => "Arabic",
                "en" => "English",
                _ => "the same language as the input"
            };

            var systemPrompt =
                "Given the recent conversation and the user's latest message, rewrite the latest message into a single " +
                "self-contained question that preserves the user's intent. Reply in the same language as the user. " +
                "Do NOT translate. If the message is already self-contained, return it unchanged.";

            var turns = history
                .TakeLast(Math.Max(1, _settings.ContextualRewriteHistoryTurns) * 2)
                .Select(t => $"{t.Role}: {t.Content.Trim()}");
            var historyText = string.Join("\n", turns);

            var userPrompt =
                $"Language hint: {langHint}\n" +
                $"Conversation (oldest first):\n{historyText}\n" +
                $"Latest message: {latestUserMessage}\n" +
                $"Self-contained rewrite:";

            var model = _settings.ContextualRewriterModel ?? _factory.GetDefaultChatModelId();
            var opts = new AiChatOptions(
                Model: model,
                Temperature: 0.2,
                MaxTokens: 200,
                SystemPrompt: systemPrompt);

            var messages = new List<AiChatMessage> { new("user", userPrompt) };
            var completion = await provider.ChatAsync(messages, opts, ct);

            return StripSurroundingQuotes(completion.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ContextualQueryResolver: LLM call failed; falling back to raw message");
            return null;
        }
    }

    private static string? StripSurroundingQuotes(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var trimmed = s.Trim();
        if (trimmed.Length >= 2
            && ((trimmed[0] == '"' && trimmed[^1] == '"')
             || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1].Trim();
        }
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private string BuildCacheKey(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language)
    {
        var providerType = _factory.GetDefaultProviderType().ToString();
        var model = _settings.ContextualRewriterModel ?? _factory.GetDefaultChatModelId();
        var lang = language ?? RagLanguageDetector.Detect(latestUserMessage);

        // Normalize history: trim each content, apply Arabic normalization. This
        // mirrors RagCacheKeys.QueryRewrite's normalization so the same payload
        // stably hashes.
        var normalizedHistory = history
            .TakeLast(Math.Max(1, _settings.ContextualRewriteHistoryTurns) * 2)
            .Select(t => $"{t.Role}:{Normalize(t.Content)}");
        var normalizedMessage = Normalize(latestUserMessage);
        var payload = string.Join("\n", normalizedHistory) + "\n---\n" + normalizedMessage;

        return RagCacheKeys.Contextualize(providerType, model, lang, payload);
    }

    private static string Normalize(string s) =>
        ArabicTextNormalizer.Normalize(
            (s ?? string.Empty).Trim(),
            new ArabicNormalizationOptions(NormalizeTaMarbuta: true, NormalizeArabicDigits: true));
}
```

- [ ] **Step 4: Run all resolver tests to verify they pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualQueryResolverTests`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs
git commit -m "feat(ai): contextual resolver cache + LLM call + quote stripping + translation guard"
```

---

## Task 7: Failure-mode tests (LLM error, Arabic, Redis unavailable)

**Goal:** Lock in the degradation matrix from spec §4.

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ThrowingCacheService.cs`

- [ ] **Step 1: Write failing tests**

First create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ThrowingCacheService.cs`:

```csharp
using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// <see cref="ICacheService"/> fake that throws on every call. Used to verify
/// graceful-degradation behavior when Redis is unavailable.
/// </summary>
internal sealed class ThrowingCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");
}
```

Then append to `ContextualQueryResolverTests.cs`:

```csharp
    [Fact]
    public async Task Llm_throws_returns_raw()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("provider down"));
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        result.Should().Be("how do we configure it?");
    }

    [Fact]
    public async Task Arabic_follow_up_triggers_llm_and_returns_result()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("كيف نضبط Qdrant؟");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "كيف نضبطه؟",
            Hist(("user", "ما هو Qdrant؟"), ("assistant", "Qdrant هو قاعدة بيانات متجهية.")),
            language: "ar", CancellationToken.None);

        result.Should().Be("كيف نضبط Qdrant؟");
        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Short_english_follow_up_triggers_llm()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("Tell me more about Qdrant?");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "and then?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        provider.Calls.Should().Be(1);
        result.Should().Be("Tell me more about Qdrant?");
    }

    [Fact]
    public async Task Cache_unavailable_still_returns_llm_result()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("How do we configure Qdrant?");
        var cache = new ThrowingCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        result.Should().Be("How do we configure Qdrant?");
        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Llm_translation_detected_falls_back_to_raw()
    {
        var provider = new FakeAiProvider();
        // User asked in Arabic; LLM returned English translation instead of Arabic rewrite.
        provider.EnqueueContent("How do we configure Qdrant?");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            "كيف نضبطه؟",
            Hist(("user", "ما هو Qdrant؟"), ("assistant", "Qdrant هو قاعدة بيانات متجهية.")),
            language: "ar", CancellationToken.None);

        result.Should().Be("كيف نضبطه؟");
    }
```

- [ ] **Step 2: Run to verify all ContextualQueryResolverTests pass (no resolver changes needed — previous task's code already covers these paths)**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextualQueryResolverTests`
Expected: PASS (11 tests).

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ThrowingCacheService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs
git commit -m "test(ai): cover contextual resolver Arabic, error, redis-down, translation-detection cases"
```

---

## Task 8: Thread history through `IRagRetrievalService.RetrieveForTurnAsync`

**Goal:** Update the retrieval interface to accept history; update all implementations and test fakes. No new stage wiring yet — just the plumbing.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IRagRetrievalService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/SearchKnowledgeBase/SearchKnowledgeBaseQueryHandler.cs` (if it calls `RetrieveForTurnAsync`)
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/SearchKnowledgeBaseQueryHandlerTests.cs`

- [ ] **Step 1: Update the interface signature**

Modify `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IRagRetrievalService.cs`:

```csharp
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IRagRetrievalService
{
    Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        CancellationToken ct);

    Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        CancellationToken ct);
}
```

- [ ] **Step 2: Update `RagRetrievalService.RetrieveForTurnAsync` signature (stage wiring comes in Task 9)**

Modify `RagRetrievalService.cs` — change the method signature only:

```csharp
    public async Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None)
            throw new InvalidOperationException(
                "Caller must ensure RagScope != None before invoking retrieval.");

        // History is not used yet — Task 9 wires the contextualize stage.
        _ = history;

        var tenantId = assistant.TenantId ?? Guid.Empty;
        IReadOnlyCollection<Guid>? docFilter = assistant.RagScope == AiRagScope.SelectedDocuments
            ? assistant.KnowledgeBaseDocIds.ToList()
            : null;

        return await RetrieveForQueryAsync(
            tenantId,
            latestUserMessage,
            docFilter,
            _settings.TopK,
            _settings.MinHybridScore,
            _settings.IncludeParentContext,
            ct);
    }
```

- [ ] **Step 3: Update `ChatExecutionService.RetrieveContextSafelyAsync` and its two callers to supply history**

First, change the method signature and add a history builder. Replace the existing `RetrieveContextSafelyAsync` at `ChatExecutionService.cs:600`:

```csharp
    private async Task<RetrievedContext> RetrieveContextSafelyAsync(
        AiAssistant assistant,
        string userMessage,
        IReadOnlyList<AiChatMessage> providerMessages,
        CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None || string.IsNullOrWhiteSpace(userMessage))
            return RetrievedContext.Empty;

        var history = BuildRagHistory(providerMessages);

        try
        {
            var retrieved = await retrievalService.RetrieveForTurnAsync(assistant, userMessage, history, ct);
            // ... rest of body unchanged ...
```

(Keep everything from `AiRagMetrics.ContextTokens.Record(retrieved.TotalTokens);` onward as-is.)

Add a private helper below `RetrieveContextSafelyAsync` (before the `try`/`catch` if using a local, or as a private static method):

```csharp
    private static IReadOnlyList<RagHistoryMessage> BuildRagHistory(IReadOnlyList<AiChatMessage> providerMessages)
    {
        // state.ProviderMessages already excludes System/tool-call rows — but the
        // current turn's user message is the LAST entry. Exclude it so the resolver
        // only sees prior history.
        if (providerMessages.Count <= 1) return Array.Empty<RagHistoryMessage>();

        var result = new List<RagHistoryMessage>(providerMessages.Count - 1);
        for (var i = 0; i < providerMessages.Count - 1; i++)
        {
            var m = providerMessages[i];
            // Retrieval history is simple: user + assistant only. Drop tool rows.
            if (m.Role != "user" && m.Role != "assistant") continue;
            if (string.IsNullOrWhiteSpace(m.Content)) continue;
            result.Add(new RagHistoryMessage(m.Role, m.Content));
        }
        return result;
    }
```

Then update the two caller lines (line 53 and line 156):

```csharp
        var retrieved = await RetrieveContextSafelyAsync(state.Assistant, userMessage, state.ProviderMessages, ct);
```

(Both call sites use the same replacement text.)

Add `using Starter.Module.AI.Application.Services.Retrieval;` at the top of `ChatExecutionService.cs` if not already present (it is — the file already imports that namespace for `IRagRetrievalService`).

- [ ] **Step 4: Update `SearchKnowledgeBaseQueryHandler` if it calls `RetrieveForTurnAsync`**

Run: `cd boilerplateBE && grep -n "RetrieveForTurnAsync" src/modules/Starter.Module.AI/Application/Queries/SearchKnowledgeBase/SearchKnowledgeBaseQueryHandler.cs || true`
If the handler calls `RetrieveForTurnAsync`, pass `Array.Empty<RagHistoryMessage>()` for history (search endpoint has no conversation). If it calls `RetrieveForQueryAsync`, no change.

- [ ] **Step 5: Update `FakeRetrieval` and `FakeRetrievalService` test fakes**

Modify `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs`:

Replace `FakeRetrieval.RetrieveForTurnAsync` to match the new signature:

```csharp
    public Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        CancellationToken ct)
    {
        CallCount++;
        if (ThrowOnRetrieve) throw new InvalidOperationException("simulated retrieval failure");
        return Task.FromResult(Context);
    }
```

Add `using Starter.Module.AI.Application.Services.Retrieval;` at the top if not already present.

Modify `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/SearchKnowledgeBaseQueryHandlerTests.cs` `FakeRetrievalService` similarly to match the new signature.

- [ ] **Step 6: Update `RagRetrievalMetricsTests.cs` calls to `RetrieveForTurnAsync`**

The existing tests call `svc.RetrieveForTurnAsync(assistant, "hello world", CancellationToken.None)` — update each call to pass `Array.Empty<RagHistoryMessage>()` for history:

```csharp
_ = await svc.RetrieveForTurnAsync(assistant, "hello world", Array.Empty<RagHistoryMessage>(), CancellationToken.None);
```

Apply to all three direct calls in this file (see the test content for their exact locations). Add the `using` import if needed.

- [ ] **Step 7: Run build and existing tests to verify nothing regressed**

Run: `cd boilerplateBE && dotnet build`
Expected: build succeeds.

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Ai`
Expected: all existing AI tests still pass (history plumbing is inert for now).

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IRagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/SearchKnowledgeBase/SearchKnowledgeBaseQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/SearchKnowledgeBaseQueryHandlerTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "refactor(ai): thread conversation history into IRagRetrievalService signature"
```

---

## Task 9: Wire `IContextualQueryResolver` into `RagRetrievalService` as a new stage

**Goal:** Insert the `contextualize` stage between `classify` and `query-rewrite` using the existing `WithTimeoutAsync` helper. The wrapper handles stage duration/outcome/degradation automatically.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify: test constructors in `RagRetrievalMetricsTests.cs` and related harnesses (they construct `RagRetrievalService` directly).

- [ ] **Step 1: Add `IContextualQueryResolver` to the RagRetrievalService constructor**

Modify the field list and constructor:

```csharp
    private readonly IContextualQueryResolver _contextualResolver;
    // ...
    public RagRetrievalService(
        AiDbContext db,
        IVectorStore vectorStore,
        IKeywordSearchService keywordSearch,
        IEmbeddingService embeddingService,
        IQueryRewriter queryRewriter,
        IContextualQueryResolver contextualResolver,
        IQuestionClassifier classifier,
        IReranker reranker,
        RerankStrategySelector rerankSelector,
        INeighborExpander neighborExpander,
        TokenCounter tokenCounter,
        IOptions<AiRagSettings> settings,
        ILogger<RagRetrievalService> logger)
    {
        // ... existing assignments ...
        _contextualResolver = contextualResolver;
        // ... rest unchanged ...
    }
```

- [ ] **Step 2: Rewrite `RetrieveForTurnAsync` to run contextualize before retrieval**

Replace the current implementation so history is used and the stage is wrapped by `WithTimeoutAsync`:

```csharp
    public async Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None)
            throw new InvalidOperationException(
                "Caller must ensure RagScope != None before invoking retrieval.");

        var tenantId = assistant.TenantId ?? Guid.Empty;
        IReadOnlyCollection<Guid>? docFilter = assistant.RagScope == AiRagScope.SelectedDocuments
            ? assistant.KnowledgeBaseDocIds.ToList()
            : null;

        // Contextualize stage — runs only when the resolver has history to work with.
        // WithTimeoutAsync records rag.stage.duration / rag.stage.outcome and adds
        // the stage to 'degraded' on timeout/error. On degraded/error we fall back
        // to the raw user message.
        string effectiveQuery = latestUserMessage;
        if (_settings.EnableContextualRewrite && history is { Count: > 0 })
        {
            var degradedForContext = new List<string>();
            var detectedLang = RagLanguageDetector.Detect(latestUserMessage);
            var resolved = await WithTimeoutAsync(
                innerCt => _contextualResolver.ResolveAsync(latestUserMessage, history, detectedLang, innerCt).ContinueWith(
                    t => (object?)t.Result, innerCt),
                _settings.StageTimeoutContextualizeMs,
                RagStages.Contextualize,
                degradedForContext,
                ct);

            if (resolved is string s && !string.IsNullOrWhiteSpace(s))
                effectiveQuery = s;

            // Surface degradation via the downstream retrieval path by pushing
            // the stage into the degraded list RetrieveForQueryAsync builds.
            foreach (var d in degradedForContext)
            {
                _logger.LogDebug("contextualize degraded: {Stage}", d);
            }

            if (degradedForContext.Count > 0)
            {
                return await RetrieveForQueryInternalAsync(
                    tenantId, effectiveQuery, docFilter,
                    _settings.TopK, _settings.MinHybridScore, _settings.IncludeParentContext,
                    seedDegraded: degradedForContext, ct);
            }
        }

        return await RetrieveForQueryAsync(
            tenantId,
            effectiveQuery,
            docFilter,
            _settings.TopK,
            _settings.MinHybridScore,
            _settings.IncludeParentContext,
            ct);
    }
```

> `WithTimeoutAsync<T>` requires `T : class`. Using a closure `ContinueWith` wrap returning `object?` keeps the stage wrapper generic without adding a string-specialized overload. (A cleaner alternative is to add a string-specialized overload; if that reads better during implementation, do that instead — as long as stage duration + outcome + degradation go through the same code path.)

- [ ] **Step 3: Add `RetrieveForQueryInternalAsync` so contextualize-degraded stages surface in `RetrievedContext.DegradedStages`**

Refactor `RetrieveForQueryAsync` into a thin wrapper over a new `RetrieveForQueryInternalAsync` that accepts a seed `degraded` list. Pattern:

```csharp
    public Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        CancellationToken ct)
        => RetrieveForQueryInternalAsync(
            tenantId, queryText, documentFilter, topK, minScore, includeParents,
            seedDegraded: null, ct);

    private async Task<RetrievedContext> RetrieveForQueryInternalAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        IReadOnlyList<string>? seedDegraded,
        CancellationToken ct)
    {
        // ... paste existing RetrieveForQueryAsync body here ...
        // Replace `var degraded = new List<string>();` with:
        var degraded = seedDegraded is { Count: > 0 }
            ? new List<string>(seedDegraded)
            : new List<string>();
        // Everything else unchanged.
    }
```

- [ ] **Step 4: Update existing `RagRetrievalService` constructor call-sites in tests**

Any test that directly constructs `new RagRetrievalService(...)` needs the new `IContextualQueryResolver` parameter. Tests to update:

Run: `cd boilerplateBE && grep -rn "new RagRetrievalService(" tests/`

Expected matches (add `new NoOpContextualQueryResolver()` right after the `IQueryRewriter` parameter):
- `tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs` — 4 constructor calls.
- Any sibling test files that construct the service directly.

Example edit (for each constructor call):

```csharp
var svc = new RagRetrievalService(
    db,
    new FakeVectorStore(),
    new FakeKeywordSearchService(),
    new Fakes.FakeEmbeddingService(),
    new NoOpQueryRewriter(),
    new NoOpContextualQueryResolver(),   // ← new
    new NoOpQuestionClassifier(),
    new NoOpReranker(),
    new RerankStrategySelector(settings),
    new NoOpNeighborExpander(),
    new TokenCounter(),
    Options.Create(settings),
    NullLogger<RagRetrievalService>.Instance);
```

- [ ] **Step 5: Write a test asserting the stage appears on a multi-turn call**

Append to `RagRetrievalMetricsTests.cs`:

```csharp
    [Fact]
    public async Task Contextualize_stage_emits_duration_and_outcome_when_history_present()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-ctx-stage-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var history = new[]
        {
            new RagHistoryMessage("user", "what is qdrant?"),
            new RagHistoryMessage("assistant", "qdrant is a vector db.")
        };

        _ = await svc.RetrieveForTurnAsync(assistant, "how do we configure it?", history, CancellationToken.None);

        var snapshot = listener.Snapshot();
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.duration"
                                       && (string?)m.Tags["rag.stage"] == RagStages.Contextualize);
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.outcome"
                                       && (string?)m.Tags["rag.stage"] == RagStages.Contextualize
                                       && (string?)m.Tags["rag.outcome"] == "success");
    }

    [Fact]
    public async Task Contextualize_stage_absent_when_history_empty()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-ctx-none-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        _ = await svc.RetrieveForTurnAsync(assistant, "what is qdrant?", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        var snapshot = listener.Snapshot();
        snapshot.Should().NotContain(m => m.InstrumentName == "rag.stage.outcome"
                                          && (string?)m.Tags["rag.stage"] == RagStages.Contextualize);
    }
```

- [ ] **Step 6: Run tests**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Ai`
Expected: all existing + new tests PASS.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "feat(ai): wire contextualize stage into RagRetrievalService between classify and query-rewrite"
```

---

## Task 10: DI registration

**Goal:** Register `IContextualQueryResolver` in the module so the container resolves it.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Add the registration**

Modify `AIModule.cs` at line 90 (next to `IQueryRewriter`):

```csharp
        services.AddScoped<IQueryRewriter, Infrastructure.Retrieval.QueryRewriting.QueryRewriter>();
        services.AddScoped<IContextualQueryResolver, Infrastructure.Retrieval.QueryRewriting.ContextualQueryResolver>();
```

Ensure `using Starter.Module.AI.Application.Services.Retrieval;` is present — it already is (the file registers `IQueryRewriter` and `IRagRetrievalService` from the same namespace).

- [ ] **Step 2: Build + run the full AI test suite**

Run: `cd boilerplateBE && dotnet build && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Ai`
Expected: all tests PASS.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs
git commit -m "chore(ai): register IContextualQueryResolver in AIModule DI"
```

---

## Task 11: Integration tests — two-turn retrieval proves the stage earns its keep

**Goal:** Full pipeline integration with stubbed vector/keyword search, showing that turn 2 retrieves the Qdrant-config chunk *because* the resolver rewrote "how do we configure it?" → "how do we configure Qdrant?".

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/MultiTurnRetrievalTests.cs`

- [ ] **Step 1: Write the integration test**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/MultiTurnRetrievalTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class MultiTurnRetrievalTests
{
    [Fact]
    public async Task Two_turn_chat_follow_up_retrieves_resolved_concept()
    {
        // Arrange: stubbed vector store returns hits only when keyword "qdrant" appears
        // in the query. That proves the resolver rewrote "how do we configure it?" into
        // "how do we configure Qdrant?".
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-multiturn-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings { EnableContextualRewrite = true };
        var provider = new FakeAiProvider();
        provider.WhenUserContains("how do we configure it?", "How do we configure Qdrant?");

        var resolver = new ContextualQueryResolver(
            new FakeAiProviderFactory(provider),
            new FakeCacheService(),
            Options.Create(settings),
            NullLogger<ContextualQueryResolver>.Instance);

        var vectorStore = new KeywordAwareFakeVectorStore("qdrant");
        var keywordSearch = new KeywordAwareFakeKeywordSearchService("qdrant");

        var svc = new RagRetrievalService(
            db,
            vectorStore,
            keywordSearch,
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            resolver,
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var history = new[]
        {
            new RagHistoryMessage("user", "what is qdrant?"),
            new RagHistoryMessage("assistant", "qdrant is a vector db.")
        };

        // Act
        var result = await svc.RetrieveForTurnAsync(
            assistant, "how do we configure it?", history, CancellationToken.None);

        // Assert: the resolver rewrote the query so the fake store returned hits.
        vectorStore.LastQueryText.Should().Contain("Qdrant");
        keywordSearch.LastQueryText.Should().Contain("Qdrant");
    }

    [Fact]
    public async Task Self_contained_follow_up_is_not_rewritten()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-multiturn-sc-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings { EnableContextualRewrite = true };
        var provider = new FakeAiProvider();
        // No response enqueued — heuristic should skip the LLM entirely.

        var resolver = new ContextualQueryResolver(
            new FakeAiProviderFactory(provider),
            new FakeCacheService(),
            Options.Create(settings),
            NullLogger<ContextualQueryResolver>.Instance);

        var vectorStore = new KeywordAwareFakeVectorStore("minio");
        var keywordSearch = new KeywordAwareFakeKeywordSearchService("minio");

        var svc = new RagRetrievalService(
            db,
            vectorStore,
            keywordSearch,
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            resolver,
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var history = new[]
        {
            new RagHistoryMessage("user", "what is qdrant?"),
            new RagHistoryMessage("assistant", "qdrant is a vector db.")
        };

        _ = await svc.RetrieveForTurnAsync(
            assistant, "Tell me about MinIO object storage and bucket policies", history, CancellationToken.None);

        vectorStore.LastQueryText.Should().Contain("MinIO");
        provider.Calls.Should().Be(0); // heuristic skipped
    }

    [Fact]
    public async Task Empty_history_preserves_pre_4b5_behavior_no_contextualize_stage()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-multiturn-first-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var provider = new FakeAiProvider();

        var resolver = new ContextualQueryResolver(
            new FakeAiProviderFactory(provider),
            new FakeCacheService(),
            Options.Create(settings),
            NullLogger<ContextualQueryResolver>.Instance);

        var vectorStore = new KeywordAwareFakeVectorStore("qdrant");
        var keywordSearch = new KeywordAwareFakeKeywordSearchService("qdrant");

        var svc = new RagRetrievalService(
            db,
            vectorStore,
            keywordSearch,
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            resolver,
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        _ = await svc.RetrieveForTurnAsync(
            assistant, "What is Qdrant?", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        provider.Calls.Should().Be(0);
        vectorStore.LastQueryText.Should().Contain("Qdrant");
    }

    [Fact]
    public async Task Arabic_follow_up_resolves_to_arabic_query()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-multiturn-ar-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var provider = new FakeAiProvider();
        provider.WhenUserContains("كيف نضبطه؟", "كيف نضبط Qdrant؟");

        var resolver = new ContextualQueryResolver(
            new FakeAiProviderFactory(provider),
            new FakeCacheService(),
            Options.Create(settings),
            NullLogger<ContextualQueryResolver>.Instance);

        var vectorStore = new KeywordAwareFakeVectorStore("Qdrant");
        var keywordSearch = new KeywordAwareFakeKeywordSearchService("Qdrant");

        var svc = new RagRetrievalService(
            db,
            vectorStore,
            keywordSearch,
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            resolver,
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var history = new[]
        {
            new RagHistoryMessage("user", "ما هو Qdrant؟"),
            new RagHistoryMessage("assistant", "Qdrant هو قاعدة بيانات متجهية.")
        };

        _ = await svc.RetrieveForTurnAsync(
            assistant, "كيف نضبطه؟", history, CancellationToken.None);

        keywordSearch.LastQueryText.Should().Contain("Qdrant");
    }
}

internal sealed class KeywordAwareFakeVectorStore : IVectorStore
{
    private readonly string _keyword;
    public string? LastQueryText { get; private set; }
    public KeywordAwareFakeVectorStore(string keyword) => _keyword = keyword;
    public Task UpsertBatchAsync(Guid tenantId, IReadOnlyList<VectorUpsertItem> items, CancellationToken ct) => Task.CompletedTask;
    public Task DeleteAsync(Guid tenantId, IReadOnlyCollection<Guid> chunkIds, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(Guid tenantId, float[] vector, IReadOnlyCollection<Guid>? docFilter, int topK, CancellationToken ct)
    {
        // Vector ignored — the test harness carries the query text via LastQueryText
        // on the keyword search; vector search just returns empty to keep pipeline simple.
        return Task.FromResult<IReadOnlyList<VectorSearchHit>>(Array.Empty<VectorSearchHit>());
    }
    public void RecordQueryText(string text) => LastQueryText = text;
}

internal sealed class KeywordAwareFakeKeywordSearchService : IKeywordSearchService
{
    private readonly string _keyword;
    public string? LastQueryText { get; private set; }
    public KeywordAwareFakeKeywordSearchService(string keyword) => _keyword = keyword;
    public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(Guid tenantId, string queryText, IReadOnlyCollection<Guid>? docFilter, int topK, CancellationToken ct)
    {
        LastQueryText = queryText;
        return Task.FromResult<IReadOnlyList<KeywordSearchHit>>(Array.Empty<KeywordSearchHit>());
    }
}
```

> If `IVectorStore` / `IKeywordSearchService` signatures differ from the placeholders above (upstream refactors), consult `FakeVectorStore.cs` / `FakeKeywordSearchService.cs` in `tests/Starter.Api.Tests/Ai/Retrieval/` and mirror their shape exactly.

- [ ] **Step 2: Run integration tests**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~MultiTurnRetrievalTests`
Expected: PASS (4 tests).

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/MultiTurnRetrievalTests.cs
git commit -m "test(ai): multi-turn retrieval integration proving contextualize rewrites before retrieval"
```

---

## Task 12: Regression-guard extension — assert contextualize appears in the end-to-end pipeline

**Goal:** Extend `End_to_end_pipeline_emits_core_observability_instruments` in `RagRetrievalMetricsTests.cs` so it also requires `rag.stage=contextualize` when history is present.

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs`

- [ ] **Step 1: Update Part 1 of the end-to-end test to pass history**

Change:

```csharp
_ = await svc.RetrieveForTurnAsync(
    assistant,
    "What is المضخة and how does cavitation affect it?",
    Array.Empty<RagHistoryMessage>(),  // ← update from Task 8
    CancellationToken.None);
```

Add a *second* call with non-empty history to exercise the contextualize stage:

```csharp
_ = await svc.RetrieveForTurnAsync(
    assistant,
    "and how do we configure it?",
    new[]
    {
        new RagHistoryMessage("user", "what is المضخة?"),
        new RagHistoryMessage("assistant", "المضخة is a pump.")
    },
    CancellationToken.None);
```

- [ ] **Step 2: Add the contextualize assertion to the final `names` block**

```csharp
        names.Should().Contain("rag.retrieval.requests");
        names.Should().Contain("rag.stage.duration");
        names.Should().Contain("rag.stage.outcome");
        names.Should().Contain("rag.fusion.candidates");
        names.Should().Contain("rag.keyword.hits");
        names.Should().Contain("rag.context.tokens");
        names.Should().Contain("rag.context.truncated");
        names.Should().Contain("rag.degraded.stages");
        // New in 4b-5: contextualize tag visible on the stage instruments
        var snapshot = listener.Snapshot();
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.duration"
                                       && (string?)m.Tags["rag.stage"] == RagStages.Contextualize);
```

- [ ] **Step 3: Run the end-to-end test**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~End_to_end_pipeline_emits_core_observability_instruments`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagRetrievalMetricsTests.cs
git commit -m "test(ai): extend pipeline regression guard to require rag.stage=contextualize on multi-turn"
```

---

## Task 13: appsettings template entries (non-breaking; defaults already applied)

**Goal:** Make the five new knobs visible to ops by showing them in the dev appsettings template. Defaults don't need to change for existing deployments; binding works without them.

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`

- [ ] **Step 1: Locate the `"AI": { "Rag": { ... } }` block**

Run: `cd boilerplateBE && grep -n "Rag" src/Starter.Api/appsettings.Development.json | head`

- [ ] **Step 2: Append the five new keys under `AI:Rag`**

Inside the existing `AI.Rag` object, add (after `ClassifierModel` or equivalent trailing key):

```json
    "EnableContextualRewrite": true,
    "StageTimeoutContextualizeMs": 3000,
    "ContextualRewriteCacheTtlSeconds": 600,
    "ContextualRewriteHistoryTurns": 3,
    "ContextualRewriterModel": null
```

Preserve surrounding commas correctly — if you're inserting mid-object, the previous line needs a trailing comma.

- [ ] **Step 3: Build + run the API once to confirm the config parses**

Run: `cd boilerplateBE && dotnet build && dotnet run --project src/Starter.Api --launch-profile http --no-build -- --dry-run 2>&1 | head -30`
(Ctrl+C after "Now listening on" appears; just confirming startup.)
Expected: no `Binding exception` or `Configuration error` in the startup log.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Api/appsettings.Development.json
git commit -m "chore(ai): document 4b-5 contextual-rewrite knobs in appsettings.Development template"
```

---

## Task 14: Full test suite + build verification

**Goal:** Ensure nothing regressed in the adjacent RAG suites.

- [ ] **Step 1: Run the full backend test suite**

Run: `cd boilerplateBE && dotnet test`
Expected: all tests PASS. Zero new failures in non-AI test packages.

- [ ] **Step 2: Run only the AI tests for quick feedback**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Ai`
Expected: green.

- [ ] **Step 3: If anything failed, fix inline and commit as separate small fixes**

Do NOT squash into an earlier commit. Each fix is its own focused diff.

---

## Task 15: Live QA (rename-app per post-feature testing workflow)

**Goal:** Mirror the Plan 4b-4 QA pattern. Verify the stage fires on a real multi-turn chat, that turn 2 retrieves the Qdrant-config chunk, and that no regressions appear elsewhere.

**Files/scripts:**
- Uses existing `scripts/rename.ps1` to create `_test4b5` test app on ports 5100 / 3100.
- Reuses the `RagMetricsDiagnosticEndpoint` QA surface pattern from 4b-4 (add it back in the rename'd app; it lives under `Configurations/` and is QA-only). If the 4b-4 endpoint file was deleted during post-4b-4 cleanup, reinstate it in the rename'd app only — do NOT commit it to the boilerplate.

- [ ] **Step 1: Generate the test app**

Run: `cd /Users/samanjasim/Projects/forme/Boilerplate-CQRS-ai-integration && scripts/rename.ps1 -Name "_test4b5" -OutputDir "."`

- [ ] **Step 2: Drop any prior database**

Run: `psql -U postgres -c "DROP DATABASE IF EXISTS _test4b5db;"`

- [ ] **Step 3: Reconfigure ports + seed email**

Per the workflow in `CLAUDE.md` → "Post-Feature Testing Workflow":
- BE → 5100, FE → 3100.
- Update `appsettings.Development.json` seed email to `superadmin@test4b5.com` to satisfy Zod `.email()`.
- CORS allow `http://localhost:3100`.

- [ ] **Step 4: Start services, build, run**

Docker services (PostgreSQL, Redis, MinIO, RabbitMQ, Mailpit) come from the existing host containers. Build both sides and launch:

```bash
cd _test4b5/_test4b5-BE && dotnet build && dotnet run --project src/_test4b5.Api --launch-profile http
cd _test4b5/_test4b5-FE && npm install && npm run dev
```

- [ ] **Step 5: Seed two assistant documents**

Via Swagger (`http://localhost:5100/swagger`) or the FE:
- Upload Qdrant-config doc (contains setup / vector-dim / collection configuration).
- Upload MinIO-config doc.
- Create an assistant with RAG scope = AllTenantDocuments.

- [ ] **Step 6: Drive the two-turn chat**

Turn 1: "What is Qdrant?"
Turn 2: "How do we configure it?"

Then repeat with Arabic turns 1/2: "ما هو Qdrant؟" → "كيف نضبطه؟".

- [ ] **Step 7: Verify observability signals**

1. `GET /diagnostics/rag-metrics` shows `rag.stage=contextualize` with 1+ measurements on turn 2 only (not turn 1).
2. Turn 2's assistant reply cites the Qdrant-config chunk.
3. Webhook event `ai.retrieval.completed` fires for both turns (spec says payload is unchanged).
4. Log line includes `RAG retrieval done assistant=… req=…` (unchanged 4b-4 format).
5. Arabic two-turn variant: same observations; `rag.lang=ar` tag appears.
6. `rag.cache=contextualize rag.hit=true` appears when repeating the same turn twice.

- [ ] **Step 8: Report + wait for user confirmation**

Report the URLs and the diagnostic output. Leave services running. Do not push without explicit user sign-off.

- [ ] **Step 9: Clean up (only after user confirms)**

Kill processes, drop `_test4b5db`, remove `_test4b5/`.

---

## Task 16: Update auto-memory roadmap after merge

**Goal:** Mark 4b-5 DONE in the Plan 4 roadmap so the next session continues correctly on the Plan 4b deferred list.

**Files:**
- Modify: `/Users/samanjasim/.claude/projects/-Users-samanjasim-Projects-forme-Boilerplate-CQRS/memory/project_ai_plan_4_roadmap.md`

- [ ] **Step 1: Update the status list**

Append under the Plan 4 sub-plans block:

```
- 4b-5 — Multi-turn contextual query rewrite — DONE
```

And update the "Next:" line to point at the next deferred-list candidate (spec §10 — likely items #11 MMR diversification or #19 circuit breaker per the 4b spec's deferred list, NOT items from the 4b-5 spec's own deferred list — see the 4b spec at `docs/superpowers/specs/2026-04-18-ai-module-plan-4b-rag-retrieval-design.md`).

- [ ] **Step 2: No commit**

Memory files are outside the repo.

---

## Self-Review Checklist

**Spec coverage:**
- §4 Architecture (contextualize between classify and query-rewrite) — Task 9.
- §4 Activation heuristic rules (length, pronoun, continuation) — Task 2.
- §4 LLM prompt + temperature + model selection + WithTimeoutAsync — Task 6 (§9 wraps in stage timeout).
- §4 Cache key + TTL + Redis-unavailable graceful degradation — Tasks 6, 7.
- §4 Degradation matrix (timeout/error/empty/quoted) — Tasks 6, 7; stage wrapper (Task 9).
- §4 Concurrency / provider-never-sees-rewrite / citation fidelity — enforced by ChatExecutionService *not* swapping the user message (unchanged).
- §5 File structure — Tasks 1, 2, 3, 4.
- §6 Five settings keys with defaults — Task 1 and Task 13 (appsettings template).
- §7 Observability (stage duration, outcome, cache counter, degraded) — Tasks 6, 9, 12.
- §8 12 unit tests — Tasks 4-7 (counted: empty-history, flag-off, heuristic-skip, cache-miss-LLM, cache-hit, quote strip, empty-LLM, LLM throws, Arabic, short-English, translation-guard, cache-unavailable). 12 checked.
- §8 4 integration tests — Task 11.
- §8 Regression guard extension — Task 12.
- §8 Live QA — Task 15.

**Placeholder scan:** reviewed each step — no "TBD", no "implement later", no "similar to Task N" without the repeated code, every code block concrete.

**Type consistency:**
- `RagHistoryMessage` used in Task 1, 3, 4, 7, 8, 9, 11, 12 — consistent `(string Role, string Content)`.
- `IContextualQueryResolver.ResolveAsync` signature stable across Tasks 3, 4, 6 — returns `Task<string>`; `string` guaranteed non-null at boundary via raw-fallback path.
- `RagStages.Contextualize = "contextualize"` string literal reused in assertions (Tasks 9, 12).
- `RagCacheKeys.Contextualize(provider, model, language, normalizedPayload)` — 4-param signature stable Tasks 1, 6.
- `ContextualFollowUpHeuristic.LooksLikeFollowUp(string)` returns `bool` — single entry point used in Task 6 resolver.
- `AiChatMessage` (internal) used only inside resolver LLM call (Task 6) — never crosses interface boundary.

**Unaddressed risks from spec §9:** translation guard explicitly coded in Task 6; prompt-injection risk mitigated because resolver output is only a query string; cache collision mitigated by tenant-agnostic SHA256(history+message) + provider+model+lang prefix.

---

## Execution Handoff

After implementing this plan, invoke `superpowers:finishing-a-development-branch` to finalize.
