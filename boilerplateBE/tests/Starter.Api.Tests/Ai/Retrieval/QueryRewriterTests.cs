using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class QueryRewriterTests
{
    private static QueryRewriter Build(
        FakeAiProvider provider,
        FakeCacheService cache,
        AiRagSettings? settings = null)
    {
        var factory = new FakeAiProviderFactory(provider);
        return new QueryRewriter(
            factory,
            cache,
            Options.Create(settings ?? new AiRagSettings { EnableQueryExpansion = true }),
            NullLogger<QueryRewriter>.Instance);
    }

    [Fact]
    public async Task Disabled_ReturnsRuleLayerOutputOnly()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache, new AiRagSettings { EnableQueryExpansion = false });

        var result = await svc.RewriteAsync(Guid.Empty, "what is photosynthesis?", "en", CancellationToken.None);

        result[0].Should().Be("what is photosynthesis?");
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Enabled_AppendsLlmVariants()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"define photosynthesis\", \"photosynthesis explanation\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync(Guid.Empty, "what is photosynthesis?", "en", CancellationToken.None);

        result.Should().Contain("what is photosynthesis?");
        result.Should().Contain("define photosynthesis");
        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task LlmFailure_FallsBackToRuleLayer()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("provider down"));
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync(Guid.Empty, "what is photosynthesis?", "en", CancellationToken.None);

        result[0].Should().Be("what is photosynthesis?");
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LlmMalformedJson_FallsBackToRuleLayer()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("sorry, I cannot produce an array");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync(Guid.Empty, "what is photosynthesis?", "en", CancellationToken.None);

        result.Should().NotBeEmpty();
        result[0].Should().Be("what is photosynthesis?");
    }

    [Fact]
    public async Task SecondCallSameQuery_HitsCache_NoProviderCall()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"v1\", \"v2\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        _ = await svc.RewriteAsync(Guid.Empty, "الضوء", "ar", CancellationToken.None);
        _ = await svc.RewriteAsync(Guid.Empty, "الضوء", "ar", CancellationToken.None);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task CapsAtMaxVariants()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache, new AiRagSettings
        {
            EnableQueryExpansion = true,
            QueryRewriteMaxVariants = 3
        });

        var result = await svc.RewriteAsync(Guid.Empty, "root", "en", CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ArabicVariants_AreNormalized_NotDuplicated()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"إضاءة\", \"اضاءة\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync(Guid.Empty, "الضوء", "ar", CancellationToken.None);

        result.Should().HaveCount(c => c <= 2);
    }
}
