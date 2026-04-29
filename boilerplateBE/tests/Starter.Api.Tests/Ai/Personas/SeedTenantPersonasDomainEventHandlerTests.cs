using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.EventHandlers;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public class SeedTenantPersonasDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_for_new_tenant_seeds_all_eight_personas()
    {
        var (db, handler, tenantId) = Setup();

        await handler.Handle(new TenantCreatedEvent(tenantId), default);

        var slugs = await db.AiPersonas
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Slug)
            .OrderBy(s => s)
            .ToListAsync();

        slugs.Should().BeEquivalentTo(new[]
        {
            "anonymous", "approver", "client", "default",
            "editor", "parent", "student", "teacher",
        });
    }

    [Fact]
    public async Task Handle_is_idempotent_when_some_personas_already_exist()
    {
        var (db, handler, tenantId) = Setup();
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateStudent(tenantId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await handler.Handle(new TenantCreatedEvent(tenantId), default);

        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantId)).Should().Be(8);
    }

    private static (
        AiDbContext db,
        SeedTenantPersonasDomainEventHandler handler,
        Guid tenantId)
        Setup()
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(tenantId);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"persona-seed-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(opts, cu.Object);
        var handler = new SeedTenantPersonasDomainEventHandler(
            db, NullLogger<SeedTenantPersonasDomainEventHandler>.Instance);
        return (db, handler, tenantId);
    }
}
