using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Module.AI;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagCircuitBreakerDIRegistrationTests
{
    [Fact]
    public void Registered_IVectorStore_is_the_circuit_breaking_decorator()
    {
        using var sp = BuildProvider();

        var resolved = sp.GetRequiredService<IVectorStore>();
        resolved.Should().BeOfType<CircuitBreakingVectorStore>();
    }

    [Fact]
    public void Registered_IKeywordSearchService_is_the_circuit_breaking_decorator()
    {
        using var sp = BuildProvider();
        using var scope = sp.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKeywordSearchService>();
        resolved.Should().BeOfType<CircuitBreakingKeywordSearch>();
    }

    [Fact]
    public void Registry_is_singleton()
    {
        using var sp = BuildProvider();

        var a = sp.GetRequiredService<RagCircuitBreakerRegistry>();
        var b = sp.GetRequiredService<RagCircuitBreakerRegistry>();
        a.Should().BeSameAs(b);
    }

    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=x;Username=x;Password=x",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Starter.Application.Common.Interfaces.ICacheService, Starter.Api.Tests.Ai.Fakes.FakeCacheService>();
        new AIModule().ConfigureServices(services, config);
        return services.BuildServiceProvider();
    }
}
