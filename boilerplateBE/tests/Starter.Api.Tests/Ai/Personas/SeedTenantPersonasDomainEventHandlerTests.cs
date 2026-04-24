using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.EventHandlers;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class SeedTenantPersonasDomainEventHandlerTests
{
    private static (SeedTenantPersonasDomainEventHandler h, AiDbContext db) Setup()
    {
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"seed-{Guid.NewGuid()}").Options;

        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns((Guid?)null);
        cu.SetupGet(x => x.UserId).Returns((Guid?)null);

        var db = new AiDbContext(opts, cu.Object);
        var h = new SeedTenantPersonasDomainEventHandler(db, NullLogger<SeedTenantPersonasDomainEventHandler>.Instance);
        return (h, db);
    }

    [Fact]
    public async Task First_Call_Creates_Anonymous_And_Default()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();

        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);

        var personas = await db.AiPersonas.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant).ToListAsync();
        personas.Should().HaveCount(2);
        personas.Should().Contain(p => p.Slug == AiPersona.AnonymousSlug && p.IsSystemReserved);
        personas.Should().Contain(p => p.Slug == AiPersona.DefaultSlug && !p.IsSystemReserved);
    }

    [Fact]
    public async Task Repeated_Call_Is_Idempotent()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();

        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);
        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);

        var count = await db.AiPersonas.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenant);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Missing_Default_Is_Added_When_Only_Anonymous_Exists()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenant, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);

        var slugs = await db.AiPersonas.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant).Select(p => p.Slug).ToListAsync();
        slugs.Should().BeEquivalentTo(new[] { AiPersona.AnonymousSlug, AiPersona.DefaultSlug });
    }
}
