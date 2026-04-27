using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Pricing;

public sealed class AiModelPricingTests
{
    private static AiDbContext NewDb()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"pricing-{Guid.NewGuid()}").Options;
        return new AiDbContext(opts, cu.Object);
    }

    [Fact]
    public async Task Pricing_Round_Trip_And_Effective_Selection()
    {
        await using var db = NewDb();
        var older = AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o", 0.0025m, 0.01m,
            effectiveFrom: DateTimeOffset.UtcNow.AddDays(-30), createdByUserId: null);
        var newer = AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o", 0.003m, 0.012m,
            effectiveFrom: DateTimeOffset.UtcNow.AddDays(-1), createdByUserId: null);
        db.AiModelPricings.AddRange(older, newer);
        await db.SaveChangesAsync();

        var current = await db.AiModelPricings
            .Where(p => p.Provider == AiProviderType.OpenAI && p.Model == "gpt-4o" &&
                        p.IsActive && p.EffectiveFrom <= DateTimeOffset.UtcNow)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstAsync();

        current.InputUsdPer1KTokens.Should().Be(0.003m);
    }

    [Fact]
    public void Create_Throws_On_Negative_Pricing()
    {
        var act = () => AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o", -0.001m, 0.01m,
            DateTimeOffset.UtcNow, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_Throws_On_Empty_Model()
    {
        var act = () => AiModelPricing.Create(AiProviderType.OpenAI, "  ", 0.001m, 0.01m,
            DateTimeOffset.UtcNow, null);
        act.Should().Throw<ArgumentException>();
    }
}
