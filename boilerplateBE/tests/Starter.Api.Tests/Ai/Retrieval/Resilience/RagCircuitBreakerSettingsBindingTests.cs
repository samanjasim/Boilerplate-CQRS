using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagCircuitBreakerSettingsBindingTests
{
    [Fact]
    public void Defaults_match_spec_when_section_is_absent()
    {
        var settings = BuildSettings(new Dictionary<string, string?>());

        settings.CircuitBreakers.Should().NotBeNull();
        settings.CircuitBreakers.Qdrant.Enabled.Should().BeTrue();
        settings.CircuitBreakers.Qdrant.MinimumThroughput.Should().Be(10);
        settings.CircuitBreakers.Qdrant.FailureRatio.Should().Be(0.5);
        settings.CircuitBreakers.Qdrant.BreakDurationMs.Should().Be(30_000);

        settings.CircuitBreakers.PostgresFts.Enabled.Should().BeTrue();
        settings.CircuitBreakers.PostgresFts.MinimumThroughput.Should().Be(10);
        settings.CircuitBreakers.PostgresFts.FailureRatio.Should().Be(0.5);
        settings.CircuitBreakers.PostgresFts.BreakDurationMs.Should().Be(30_000);
    }

    [Fact]
    public void Configured_values_override_defaults()
    {
        var settings = BuildSettings(new Dictionary<string, string?>
        {
            ["AI:Rag:CircuitBreakers:Qdrant:Enabled"] = "false",
            ["AI:Rag:CircuitBreakers:Qdrant:MinimumThroughput"] = "20",
            ["AI:Rag:CircuitBreakers:Qdrant:FailureRatio"] = "0.75",
            ["AI:Rag:CircuitBreakers:Qdrant:BreakDurationMs"] = "60000",
            ["AI:Rag:CircuitBreakers:PostgresFts:BreakDurationMs"] = "10000",
        });

        settings.CircuitBreakers.Qdrant.Enabled.Should().BeFalse();
        settings.CircuitBreakers.Qdrant.MinimumThroughput.Should().Be(20);
        settings.CircuitBreakers.Qdrant.FailureRatio.Should().Be(0.75);
        settings.CircuitBreakers.Qdrant.BreakDurationMs.Should().Be(60_000);
        settings.CircuitBreakers.PostgresFts.BreakDurationMs.Should().Be(10_000);
    }

    private static AiRagSettings BuildSettings(IDictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<AiRagSettings>().Bind(config.GetSection(AiRagSettings.SectionName));
        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<AiRagSettings>>().Value;
    }
}
