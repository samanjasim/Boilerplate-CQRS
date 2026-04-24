using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

public sealed class AiToolRegistrationCollisionTests
{
    [Fact]
    public void Registry_Throws_When_Two_Definitions_Share_A_Name()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>().Object;
        var defs = new IAiToolDefinition[]
        {
            StubDef("dup_tool"),
            StubDef("dup_tool"),
        };

        var act = () => new AiToolRegistryService(defs, scopeFactory);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate*dup_tool*");
    }

    [Fact]
    public async Task SyncHostedService_Throws_When_Duplicates_Present()
    {
        var defs = new IAiToolDefinition[]
        {
            StubDef("dup_tool"),
            StubDef("dup_tool"),
        };

        var services = new ServiceCollection();
        services.AddDbContext<AiDbContext>(o => o.UseInMemoryDatabase($"sync-{Guid.NewGuid()}"));
        var sp = services.BuildServiceProvider();

        var sync = new AiToolRegistrySyncHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            defs,
            NullLogger<AiToolRegistrySyncHostedService>.Instance);

        var act = () => sync.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicate*dup_tool*");
    }

    private static IAiToolDefinition StubDef(string name)
    {
        var mock = new Mock<IAiToolDefinition>();
        mock.SetupGet(m => m.Name).Returns(name);
        mock.SetupGet(m => m.Description).Returns("d");
        mock.SetupGet(m => m.Category).Returns("c");
        mock.SetupGet(m => m.RequiredPermission).Returns("p");
        mock.SetupGet(m => m.IsReadOnly).Returns(true);
        mock.SetupGet(m => m.CommandType).Returns(typeof(object));
        mock.SetupGet(m => m.ParameterSchema)
            .Returns(JsonDocument.Parse("""{ "type": "object" }""").RootElement);
        return mock.Object;
    }
}
