using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Pricing;
using Xunit;

namespace Starter.Api.Tests.Ai.Pricing;

public sealed class ModelPricingServiceTests
{
    private static (AiDbContext db, Mock<ICacheService> cache) NewSetup()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"pricing-svc-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

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
        return (db, cache);
    }

    [Fact]
    public async Task EstimateCost_Uses_Latest_Effective_Pricing()
    {
        var (db, cache) = NewSetup();
        db.AiModelPricings.Add(AiModelPricing.Create(
            AiProviderType.OpenAI, "gpt-4o", 0.0025m, 0.01m,
            DateTimeOffset.UtcNow.AddDays(-1), null));
        await db.SaveChangesAsync();

        var sut = new ModelPricingService(db, cache.Object, NullLogger<ModelPricingService>.Instance);
        // 1k input * $0.0025/1K = $0.0025; 1k output * $0.01/1K = $0.01; total $0.0125
        var cost = await sut.EstimateCostAsync(AiProviderType.OpenAI, "gpt-4o", 1000, 1000);
        cost.Should().Be(0.0125m);
    }

    [Fact]
    public async Task GetPricing_Returns_Most_Recent_Effective()
    {
        var (db, cache) = NewSetup();
        db.AiModelPricings.AddRange(
            AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o", 0.0025m, 0.01m,
                DateTimeOffset.UtcNow.AddDays(-30), null),
            AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o", 0.003m, 0.012m,
                DateTimeOffset.UtcNow.AddDays(-1), null));
        await db.SaveChangesAsync();

        var sut = new ModelPricingService(db, cache.Object, NullLogger<ModelPricingService>.Instance);
        var (input, output) = await sut.GetPricingAsync(AiProviderType.OpenAI, "gpt-4o");
        input.Should().Be(0.003m);
        output.Should().Be(0.012m);
    }

    [Fact]
    public async Task GetPricing_Throws_When_Missing()
    {
        var (db, cache) = NewSetup();
        var sut = new ModelPricingService(db, cache.Object, NullLogger<ModelPricingService>.Instance);
        var act = async () => await sut.GetPricingAsync(AiProviderType.OpenAI, "no-such-model");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pricing*no-such-model*");
    }

    [Fact]
    public async Task GetPricing_Ignores_Future_EffectiveFrom()
    {
        var (db, cache) = NewSetup();
        db.AiModelPricings.AddRange(
            AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o", 0.0025m, 0.01m,
                DateTimeOffset.UtcNow.AddDays(-30), null),
            AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o", 0.999m, 0.999m,
                DateTimeOffset.UtcNow.AddDays(30), null));
        await db.SaveChangesAsync();

        var sut = new ModelPricingService(db, cache.Object, NullLogger<ModelPricingService>.Instance);
        var (input, _) = await sut.GetPricingAsync(AiProviderType.OpenAI, "gpt-4o");
        input.Should().Be(0.0025m);
    }
}
