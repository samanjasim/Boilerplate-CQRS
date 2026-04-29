using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Costs;

public sealed class CostCapEnforcingAgentRuntimeTests
{
    [Fact]
    public async Task Runtime_Platform_Source_Claims_Total_And_Platform_Credit()
    {
        var tenantId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var accountant = AccountantGranted();
        var sut = Build(accountant);

        var result = await sut.RunAsync(Ctx(tenantId, assistantId, ProviderCredentialSource.Platform), Mock.Of<IAgentRunSink>());

        result.Status.Should().Be(AgentRunStatus.Completed);
        accountant.Verify(a => a.TryClaimAsync(tenantId, assistantId, 1m, CapWindow.Monthly, 20m, CostCapBucket.Total, It.IsAny<CancellationToken>()), Times.Once);
        accountant.Verify(a => a.TryClaimAsync(tenantId, assistantId, 1m, CapWindow.Daily, 2m, CostCapBucket.Total, It.IsAny<CancellationToken>()), Times.Once);
        accountant.Verify(a => a.TryClaimAsync(tenantId, assistantId, 1m, CapWindow.Monthly, 10m, CostCapBucket.PlatformCredit, It.IsAny<CancellationToken>()), Times.Once);
        accountant.Verify(a => a.TryClaimAsync(tenantId, assistantId, 1m, CapWindow.Daily, 1m, CostCapBucket.PlatformCredit, It.IsAny<CancellationToken>()), Times.Once);
        accountant.Verify(a => a.RecordActualAsync(tenantId, assistantId, -0.4m, It.IsAny<CapWindow>(), It.IsAny<CostCapBucket>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task Runtime_Tenant_Source_Claims_Total_Only()
    {
        var tenantId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var accountant = AccountantGranted();
        var sut = Build(accountant);

        var result = await sut.RunAsync(Ctx(tenantId, assistantId, ProviderCredentialSource.Tenant), Mock.Of<IAgentRunSink>());

        result.Status.Should().Be(AgentRunStatus.Completed);
        accountant.Verify(a => a.TryClaimAsync(tenantId, assistantId, 1m, CapWindow.Monthly, 20m, CostCapBucket.Total, It.IsAny<CancellationToken>()), Times.Once);
        accountant.Verify(a => a.TryClaimAsync(tenantId, assistantId, 1m, CapWindow.Daily, 2m, CostCapBucket.Total, It.IsAny<CancellationToken>()), Times.Once);
        accountant.Verify(a => a.TryClaimAsync(
            tenantId,
            assistantId,
            It.IsAny<decimal>(),
            It.IsAny<CapWindow>(),
            It.IsAny<decimal>(),
            CostCapBucket.PlatformCredit,
            It.IsAny<CancellationToken>()), Times.Never);
        accountant.Verify(a => a.RecordActualAsync(tenantId, assistantId, -0.4m, It.IsAny<CapWindow>(), CostCapBucket.Total, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static CostCapEnforcingAgentRuntime Build(Mock<ICostCapAccountant> accountant)
    {
        var caps = new Mock<ICostCapResolver>();
        caps.Setup(c => c.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveCaps(20m, 2m, 60, 10m, 1m));

        var rateLimiter = new Mock<IAgentRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var pricing = new Mock<IModelPricingService>();
        pricing.SetupSequence(p => p.EstimateCostAsync(
                It.IsAny<AiProviderType>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1m)
            .ReturnsAsync(0.6m);

        return new CostCapEnforcingAgentRuntime(
            new CompletedRuntime(),
            caps.Object,
            accountant.Object,
            rateLimiter.Object,
            pricing.Object,
            NullLogger<CostCapEnforcingAgentRuntime>.Instance);
    }

    private static Mock<ICostCapAccountant> AccountantGranted()
    {
        var accountant = new Mock<ICostCapAccountant>();
        accountant.Setup(a => a.TryClaimAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<CapWindow>(),
                It.IsAny<decimal>(),
                It.IsAny<CostCapBucket>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, decimal amount, CapWindow _, decimal cap, CostCapBucket _, CancellationToken _) =>
                new ClaimResult(true, amount, cap));
        return accountant;
    }

    private static AgentRunContext Ctx(Guid tenantId, Guid assistantId, ProviderCredentialSource source) =>
        new(
            Messages: new[] { new AiChatMessage("user", "hello") },
            SystemPrompt: "system",
            ModelConfig: new AgentModelConfig(AiProviderType.OpenAI, "gpt-4o-mini", 0.7, 100),
            Tools: new ToolResolutionResult(
                ProviderTools: Array.Empty<AiToolDefinitionDto>(),
                DefinitionsByName: new Dictionary<string, IAiToolDefinition>()),
            MaxSteps: 1,
            LoopBreak: LoopBreakPolicy.Default,
            AssistantId: assistantId,
            TenantId: tenantId,
            ProviderCredentialSource: source);

    private sealed class CompletedRuntime : IAiAgentRuntime
    {
        public Task<AgentRunResult> RunAsync(AgentRunContext context, IAgentRunSink sink, CancellationToken ct = default)
        {
            return Task.FromResult(new AgentRunResult(
                AgentRunStatus.Completed,
                FinalContent: "ok",
                Steps: Array.Empty<AgentStepEvent>(),
                TotalInputTokens: 10,
                TotalOutputTokens: 10,
                TerminationReason: null));
        }
    }
}
