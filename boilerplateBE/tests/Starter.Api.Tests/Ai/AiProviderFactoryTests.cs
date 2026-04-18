using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;
using Xunit;

namespace Starter.Api.Tests.Ai;

public sealed class AiProviderFactoryTests
{
    private static AiProviderFactory Build(Dictionary<string, string?> settings) =>
        new(new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    [Fact]
    public void GetEmbeddingProviderType_Uses_EmbeddingProvider_When_Set()
    {
        var factory = Build(new()
        {
            ["AI:DefaultProvider"] = "Anthropic",
            ["AI:EmbeddingProvider"] = "OpenAI",
        });

        factory.GetEmbeddingProviderType().Should().Be(AiProviderType.OpenAI);
        factory.GetDefaultProviderType().Should().Be(AiProviderType.Anthropic);
    }

    [Fact]
    public void GetEmbeddingProviderType_Falls_Back_To_DefaultProvider_When_Unset()
    {
        var factory = Build(new()
        {
            ["AI:DefaultProvider"] = "OpenAI",
        });

        factory.GetEmbeddingProviderType().Should().Be(AiProviderType.OpenAI);
    }

    [Fact]
    public void GetEmbeddingProviderType_Falls_Back_When_Empty()
    {
        var factory = Build(new()
        {
            ["AI:DefaultProvider"] = "Anthropic",
            ["AI:EmbeddingProvider"] = "",
        });

        factory.GetEmbeddingProviderType().Should().Be(AiProviderType.Anthropic);
    }
}
