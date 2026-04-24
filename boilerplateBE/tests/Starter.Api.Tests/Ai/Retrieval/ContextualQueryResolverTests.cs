using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
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
        ICacheService cache,
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

        var result = await svc.ResolveAsync(Guid.Empty, "how do we configure it?", history: Array.Empty<RagHistoryMessage>(), language: null, CancellationToken.None);

        result.Should().Be("how do we configure it?");
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Feature_flag_off_returns_raw_no_provider_call()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache, new AiRagSettings { EnableContextualRewrite = false });

        var result = await svc.ResolveAsync(Guid.Empty, "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: null, CancellationToken.None);

        result.Should().Be("how do we configure it?");
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Heuristic_skips_self_contained_question_returns_raw()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            Guid.Empty,
            "What is the default RRF constant used in hybrid fusion?",
            Hist(("user", "hi"), ("assistant", "hello")),
            language: "en", CancellationToken.None);

        result.Should().Be("What is the default RRF constant used in hybrid fusion?");
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Heuristic_positive_cache_miss_calls_llm_and_caches_result()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("How do we configure Qdrant?");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            Guid.Empty,
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        result.Should().Be("How do we configure Qdrant?");
        provider.Calls.Should().Be(1);
        var second = await svc.ResolveAsync(
            Guid.Empty,
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);
        second.Should().Be("How do we configure Qdrant?");
        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Strips_surrounding_quotes_from_llm_output()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("\"How do we configure Qdrant?\"");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            Guid.Empty,
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
            Guid.Empty,
            "how do we configure it?",
            Hist(("user", "what is qdrant?"), ("assistant", "qdrant is a vector db.")),
            language: "en", CancellationToken.None);

        result.Should().Be("how do we configure it?");
    }

    [Fact]
    public async Task Llm_throws_returns_raw()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("provider down"));
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            Guid.Empty,
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
            Guid.Empty,
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
            Guid.Empty,
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
            Guid.Empty,
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
        provider.EnqueueContent("How do we configure Qdrant?");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.ResolveAsync(
            Guid.Empty,
            "كيف نضبطه؟",
            Hist(("user", "ما هو Qdrant؟"), ("assistant", "Qdrant هو قاعدة بيانات متجهية.")),
            language: "ar", CancellationToken.None);

        result.Should().Be("كيف نضبطه؟");
    }
}
