using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Runtime;

public sealed class AiAgentRuntimeFactoryTests
{
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAiProviderFactory>(new FakeAiProviderFactory(new FakeAiProvider()));
        services.AddSingleton<IAgentToolDispatcher>(Mock.Of<IAgentToolDispatcher>());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<AgentRuntimeBase>>(NullLogger<AgentRuntimeBase>.Instance);
        services.AddScoped<OpenAiAgentRuntime>();
        services.AddScoped<AnthropicAgentRuntime>();
        services.AddScoped<OllamaAgentRuntime>();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(AiProviderType.OpenAI, typeof(OpenAiAgentRuntime))]
    [InlineData(AiProviderType.Anthropic, typeof(AnthropicAgentRuntime))]
    [InlineData(AiProviderType.Ollama, typeof(OllamaAgentRuntime))]
    public void Create_Returns_Expected_Runtime_For_Provider(AiProviderType provider, Type expected)
    {
        using var scope = BuildServices().CreateScope();
        var factory = new AiAgentRuntimeFactory(scope.ServiceProvider);

        var runtime = factory.Create(provider);

        runtime.Should().BeOfType(expected);
    }

    [Fact]
    public void Create_Throws_NotSupported_For_Unknown_Provider_Type()
    {
        using var scope = BuildServices().CreateScope();
        var factory = new AiAgentRuntimeFactory(scope.ServiceProvider);

        Action act = () => factory.Create((AiProviderType)int.MaxValue);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("agent runtime");
    }
}
