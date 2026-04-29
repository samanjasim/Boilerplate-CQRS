using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Costs;
using Xunit;

namespace Starter.Api.Tests.Ai.Costs;

public sealed class CostCapResolverTests
{
    private static (AiDbContext db, Mock<IFeatureFlagService> ff, Mock<ICacheService> cache) NewSetup()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"capres-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var ff = new Mock<IFeatureFlagService>();
        var cache = new Mock<ICacheService>();
        // Pass-through: invoke factory and return its result, no caching.
        cache.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<It.IsAnyType>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var factory = invocation.Arguments[1];
                return factory.GetType().GetMethod("Invoke")!.Invoke(factory, null)!;
            }));
        return (db, ff, cache);
    }

    private static void SetupPlan(Mock<IFeatureFlagService> ff)
    {
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", default)).ReturnsAsync(20m);
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", default)).ReturnsAsync(2m);
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.platform_monthly_usd", default)).ReturnsAsync(10m);
        ff.Setup(f => f.GetValueAsync<decimal>("ai.cost.platform_daily_usd", default)).ReturnsAsync(1m);
        ff.Setup(f => f.GetValueAsync<int>("ai.agents.requests_per_minute_default", default)).ReturnsAsync(60);
    }

    [Fact]
    public async Task Resolve_Returns_Plan_Caps_When_No_Per_Agent_Override()
    {
        var (db, ff, cache) = NewSetup();
        var assistant = AiAssistant.Create(Guid.NewGuid(), "Tutor", null, "prompt", Guid.NewGuid());
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        SetupPlan(ff);

        var sut = new CostCapResolver(db, ff.Object, cache.Object);
        var caps = await sut.ResolveAsync(assistant.TenantId!.Value, assistant.Id);

        caps.MonthlyUsd.Should().Be(20m);
        caps.DailyUsd.Should().Be(2m);
        caps.Rpm.Should().Be(60);
        caps.PlatformMonthlyUsd.Should().Be(10m);
        caps.PlatformDailyUsd.Should().Be(1m);
    }

    [Fact]
    public async Task Resolve_Per_Agent_Cap_Wins_When_Lower_Than_Plan()
    {
        var (db, ff, cache) = NewSetup();
        var assistant = AiAssistant.Create(Guid.NewGuid(), "Tutor", null, "prompt", Guid.NewGuid());
        assistant.SetBudget(monthlyUsd: 5m, dailyUsd: null, requestsPerMinute: 30);
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        SetupPlan(ff);

        var sut = new CostCapResolver(db, ff.Object, cache.Object);
        var caps = await sut.ResolveAsync(assistant.TenantId!.Value, assistant.Id);

        caps.MonthlyUsd.Should().Be(5m);   // per-agent wins (lower)
        caps.DailyUsd.Should().Be(2m);     // plan wins (per-agent null)
        caps.Rpm.Should().Be(30);          // per-agent wins
        caps.PlatformMonthlyUsd.Should().Be(5m);
        caps.PlatformDailyUsd.Should().Be(1m);
    }

    [Fact]
    public async Task Resolve_Plan_Cap_Wins_When_Lower_Than_Per_Agent()
    {
        var (db, ff, cache) = NewSetup();
        var assistant = AiAssistant.Create(Guid.NewGuid(), "Tutor", null, "prompt", Guid.NewGuid());
        assistant.SetBudget(monthlyUsd: 50m, dailyUsd: 10m, requestsPerMinute: 100);
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        SetupPlan(ff);

        var sut = new CostCapResolver(db, ff.Object, cache.Object);
        var caps = await sut.ResolveAsync(assistant.TenantId!.Value, assistant.Id);

        // Lowest wins: plan ceilings cap each dimension below the per-agent override.
        caps.MonthlyUsd.Should().Be(20m);
        caps.DailyUsd.Should().Be(2m);
        caps.Rpm.Should().Be(60);
    }

    [Fact]
    public async Task Resolve_Includes_Tenant_Self_Limits_Below_Plan()
    {
        var (db, ff, cache) = NewSetup();
        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "Tutor", null, "prompt", Guid.NewGuid());
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdateCostSelfLimits(
            monthlyCostCapUsd: 12m,
            dailyCostCapUsd: 1.5m,
            platformMonthlyCostCapUsd: 8m,
            platformDailyCostCapUsd: 0.75m,
            requestsPerMinute: 40);
        db.AiAssistants.Add(assistant);
        db.AiTenantSettings.Add(settings);
        await db.SaveChangesAsync();
        SetupPlan(ff);

        var sut = new CostCapResolver(db, ff.Object, cache.Object);
        var caps = await sut.ResolveAsync(tenantId, assistant.Id);

        caps.MonthlyUsd.Should().Be(12m);
        caps.DailyUsd.Should().Be(1.5m);
        caps.Rpm.Should().Be(40);
        caps.PlatformMonthlyUsd.Should().Be(8m);
        caps.PlatformDailyUsd.Should().Be(0.75m);
    }

    [Fact]
    public async Task Resolve_Platform_Credit_Caps_Are_Separate_From_Total_Caps()
    {
        var (db, ff, cache) = NewSetup();
        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "Tutor", null, "prompt", Guid.NewGuid());
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdateCostSelfLimits(
            monthlyCostCapUsd: 12m,
            dailyCostCapUsd: 1.5m,
            platformMonthlyCostCapUsd: 4m,
            platformDailyCostCapUsd: 0.25m,
            requestsPerMinute: null);
        db.AiAssistants.Add(assistant);
        db.AiTenantSettings.Add(settings);
        await db.SaveChangesAsync();
        SetupPlan(ff);

        var sut = new CostCapResolver(db, ff.Object, cache.Object);
        var caps = await sut.ResolveAsync(tenantId, assistant.Id);

        caps.MonthlyUsd.Should().Be(12m);
        caps.DailyUsd.Should().Be(1.5m);
        caps.PlatformMonthlyUsd.Should().Be(4m);
        caps.PlatformDailyUsd.Should().Be(0.25m);
    }

    [Fact]
    public async Task InvalidateAsync_Removes_Cache_Entry()
    {
        var (db, ff, cache) = NewSetup();
        var sut = new CostCapResolver(db, ff.Object, cache.Object);
        var tenantId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        await sut.InvalidateAsync(tenantId, assistantId);

        cache.Verify(c => c.RemoveAsync($"ai:cap:{tenantId}:{assistantId}", default), Times.Once);
    }
}
