using FluentAssertions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiEntitlementResolverTests
{
    [Fact]
    public async Task ResolveAsync_Reads_All_Ai_Entitlements()
    {
        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", default)).ReturnsAsync(20m);
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", default)).ReturnsAsync(2m);
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.platform_monthly_usd", default)).ReturnsAsync(10m);
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.platform_daily_usd", default)).ReturnsAsync(1m);
        ff.Setup(x => x.GetValueAsync<int>("ai.agents.requests_per_minute_default", default)).ReturnsAsync(60);
        ff.Setup(x => x.GetValueAsync<bool>("ai.provider_keys.byok_enabled", default)).ReturnsAsync(true);
        ff.Setup(x => x.GetValueAsync<bool>("ai.widgets.enabled", default)).ReturnsAsync(true);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.max_count", default)).ReturnsAsync(3);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.monthly_tokens", default)).ReturnsAsync(50_000);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.daily_tokens", default)).ReturnsAsync(5_000);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.requests_per_minute", default)).ReturnsAsync(30);
        ff.Setup(x => x.GetValueAsync<string[]>("ai.providers.allowed", default)).ReturnsAsync(new[] { "OpenAI", "Anthropic" });
        ff.Setup(x => x.GetValueAsync<string[]>("ai.models.allowed", default)).ReturnsAsync(new[] { "gpt-4o-mini" });

        var sut = new AiEntitlementResolver(ff.Object);
        var entitlements = await sut.ResolveAsync();

        entitlements.TotalMonthlyUsd.Should().Be(20m);
        entitlements.PlatformMonthlyUsd.Should().Be(10m);
        entitlements.ByokEnabled.Should().BeTrue();
        entitlements.WidgetsEnabled.Should().BeTrue();
        entitlements.WidgetMaxCount.Should().Be(3);
        entitlements.AllowedProviders.Should().Equal("OpenAI", "Anthropic");
        entitlements.AllowedModels.Should().Equal("gpt-4o-mini");
    }
}
