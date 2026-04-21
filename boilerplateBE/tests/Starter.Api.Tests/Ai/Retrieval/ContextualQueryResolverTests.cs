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
}
