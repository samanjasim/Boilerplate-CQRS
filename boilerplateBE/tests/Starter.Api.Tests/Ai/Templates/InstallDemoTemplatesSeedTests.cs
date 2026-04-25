using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Persistence;
using Starter.Module.AI;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallDemoTemplatesSeedTests
{
    [Fact]
    public async Task Flag_off_does_nothing()
    {
        await using var sp = BuildServiceProvider(flagOn: false);

        var module = new AIModule();
        await module.SeedDataAsync(sp);

        var db = sp.GetRequiredService<AiDbContext>();
        (await db.AiAssistants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Flag_on_with_no_tenants_creates_no_assistants()
    {
        await using var sp = BuildServiceProvider(flagOn: true);

        var module = new AIModule();
        await module.SeedDataAsync(sp);

        var db = sp.GetRequiredService<AiDbContext>();
        (await db.AiAssistants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private static ServiceProvider BuildServiceProvider(bool flagOn)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:InstallDemoTemplatesOnStartup"] = flagOn ? "true" : "false",
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns((Guid?)null);
        cu.SetupGet(x => x.TenantId).Returns((Guid?)null);
        services.AddSingleton(cu.Object);

        var dbName = $"seed-{Guid.NewGuid()}";
        services.AddDbContext<AiDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddDbContext<ApplicationDbContext>(o => o
            .UseInMemoryDatabase($"{dbName}-app")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        var registry = new AiAgentTemplateRegistry(Array.Empty<IAiAgentTemplate>());
        services.AddSingleton<IAiAgentTemplateRegistry>(registry);

        var mediator = new Mock<IMediator>();
        services.AddSingleton(mediator.Object);

        return services.BuildServiceProvider();
    }
}
