using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Application.Common.Interfaces;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
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
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();

        // Plan 5d-1 dependencies — runtime is now wrapped by CostCapEnforcingAgentRuntime
        // which needs these services. Mock everything; tests only assert factory dispatch.
        services.AddSingleton<ICostCapResolver>(Mock.Of<ICostCapResolver>());
        services.AddSingleton<ICostCapAccountant>(Mock.Of<ICostCapAccountant>());
        services.AddSingleton<IAgentRateLimiter>(Mock.Of<IAgentRateLimiter>());
        services.AddSingleton<IModelPricingService>(Mock.Of<IModelPricingService>());
        services.AddSingleton<IAgentPermissionResolver>(Mock.Of<IAgentPermissionResolver>());

        // Plan 5d-2 dependencies — factory now wraps cost-cap with ContentModerationEnforcingAgentRuntime.
        services.AddSingleton<IContentModerator>(Mock.Of<IContentModerator>());
        services.AddSingleton<IPiiRedactor>(Mock.Of<IPiiRedactor>());
        services.AddSingleton<ISafetyProfileResolver>(Mock.Of<ISafetyProfileResolver>());
        services.AddSingleton<IModerationRefusalProvider>(Mock.Of<IModerationRefusalProvider>());
        services.AddSingleton<IWebhookPublisher>(Mock.Of<IWebhookPublisher>());
        services.AddSingleton<CurrentAgentRunContextAccessor>();

        var cu = new Mock<ICurrentUserService>();
        services.AddSingleton(cu.Object);
        services.AddDbContext<AiDbContext>(o =>
            o.UseInMemoryDatabase($"factory-{Guid.NewGuid()}"));

        services.AddScoped<OpenAiAgentRuntime>();
        services.AddScoped<AnthropicAgentRuntime>();
        services.AddScoped<OllamaAgentRuntime>();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(AiProviderType.OpenAI)]
    [InlineData(AiProviderType.Anthropic)]
    [InlineData(AiProviderType.Ollama)]
    public void Create_Returns_NonNull_Runtime_For_Provider(AiProviderType provider)
    {
        using var scope = BuildServices().CreateScope();
        var factory = new AiAgentRuntimeFactory(scope.ServiceProvider);

        var runtime = factory.Create(provider);

        // Plan 5d-1: factory wraps the provider-specific runtime in CostCapEnforcingAgentRuntime.
        // The contract is that a usable IAiAgentRuntime is returned; the concrete wrapping is internal.
        runtime.Should().NotBeNull();
        runtime.Should().BeAssignableTo<IAiAgentRuntime>();
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
