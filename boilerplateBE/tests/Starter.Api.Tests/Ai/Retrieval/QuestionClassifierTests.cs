using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class QuestionClassifierTests
{
    private static QuestionClassifier Build(FakeAiProvider provider, FakeCacheService cache) =>
        new(new FakeAiProviderFactory(provider), cache,
            Options.Create(new AiRagSettings()), NullLogger<QuestionClassifier>.Instance);

    [Fact]
    public async Task Regex_match_skips_llm()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync(Guid.Empty, "hello", CancellationToken.None);

        type.Should().Be(QuestionType.Greeting);
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Llm_called_when_regex_does_not_match()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("Reasoning");
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync(Guid.Empty, "the forecast for Q3 is ambiguous", CancellationToken.None);

        type.Should().Be(QuestionType.Reasoning);
        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Llm_result_is_cached()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("Definition");
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        await c.ClassifyAsync(Guid.Empty, "what about concurrent queues in .NET", CancellationToken.None);
        await c.ClassifyAsync(Guid.Empty, "what about concurrent queues in .NET", CancellationToken.None);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Llm_failure_returns_null()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("boom"));
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync(Guid.Empty, "ambiguous prose input", CancellationToken.None);

        type.Should().BeNull();
    }

    [Fact]
    public async Task Unknown_llm_label_maps_to_Other()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("Nonsense");
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync(Guid.Empty, "ambiguous prose input", CancellationToken.None);

        type.Should().Be(QuestionType.Other);
    }
}
