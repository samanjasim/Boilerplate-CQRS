using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.SetAgentBudget;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-1 acid test M4: plan tier integration. The cap-validation logic is exercised
/// indirectly via the SetAgentBudget command path. Per-agent caps validated against
/// plan ceilings; Free plan blocks all spend (cap = 0).
/// </summary>
public sealed class Plan5d1PlanLimitAcidTests
{
    private static AiDbContext NewAiDb()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"plan-acid-{Guid.NewGuid()}").Options;
        return new AiDbContext(opts, cu.Object);
    }

    [Fact]
    public async Task Acid_M4_1_PerAgent_Cap_Set_Above_Plan_Is_Validation_Error()
    {
        // Plan permits $20/mo. Per-agent attempts to set $50/mo. Validator should reject.
        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", It.IsAny<CancellationToken>()))
          .ReturnsAsync(20m);
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", It.IsAny<CancellationToken>()))
          .ReturnsAsync(2m);
        ff.Setup(f => f.GetValueAsync<int>("ai.agents.requests_per_minute_default", It.IsAny<CancellationToken>()))
          .ReturnsAsync(60);

        var validator = new SetAgentBudgetCommandValidator(ff.Object);
        var command = new SetAgentBudgetCommand(
            AssistantId: Guid.NewGuid(),
            MonthlyCostCapUsd: 50m,        // above plan ceiling
            DailyCostCapUsd: 1m,
            RequestsPerMinute: 30);

        var result = await validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SetAgentBudgetCommand.MonthlyCostCapUsd));
    }

    [Fact]
    public async Task Acid_M4_2_PerAgent_Cap_Below_Plan_Validates_Successfully()
    {
        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", It.IsAny<CancellationToken>()))
          .ReturnsAsync(200m);
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", It.IsAny<CancellationToken>()))
          .ReturnsAsync(20m);
        ff.Setup(f => f.GetValueAsync<int>("ai.agents.requests_per_minute_default", It.IsAny<CancellationToken>()))
          .ReturnsAsync(60);

        var validator = new SetAgentBudgetCommandValidator(ff.Object);
        var command = new SetAgentBudgetCommand(
            AssistantId: Guid.NewGuid(),
            MonthlyCostCapUsd: 50m,
            DailyCostCapUsd: 5m,
            RequestsPerMinute: 30);

        var result = await validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Acid_M4_3_Setting_Budget_Persists_And_Cap_Resolver_Sees_New_Values()
    {
        await using var db = NewAiDb();
        var assistant = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Tutor",
            description: null,
            systemPrompt: "Be helpful.",
            createdByUserId: Guid.NewGuid());
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(f => f.GetValueAsync<decimal>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(200m);
        ff.Setup(f => f.GetValueAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(60);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<EffectiveCaps>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var factory = invocation.Arguments[1];
                return factory.GetType().GetMethod("Invoke")!.Invoke(factory, null)!;
            }));

        var resolver = new Starter.Module.AI.Infrastructure.Services.Costs.CostCapResolver(
            db, ff.Object, cache.Object);

        // Cap resolver before budget is set — defaults to plan ceilings.
        var before = await resolver.ResolveAsync(assistant.TenantId!.Value, assistant.Id);
        before.MonthlyUsd.Should().Be(200m);

        // Set per-agent budget below plan; resolver should now return the lower value.
        assistant.SetBudget(monthlyUsd: 5m, dailyUsd: 1m, requestsPerMinute: 30);
        await db.SaveChangesAsync();
        await resolver.InvalidateAsync(assistant.TenantId!.Value, assistant.Id);

        var after = await resolver.ResolveAsync(assistant.TenantId!.Value, assistant.Id);
        after.MonthlyUsd.Should().Be(5m);
        after.Rpm.Should().Be(30);
    }
}
