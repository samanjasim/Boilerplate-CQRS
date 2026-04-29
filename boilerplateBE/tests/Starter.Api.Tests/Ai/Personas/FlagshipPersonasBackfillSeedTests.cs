using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Persistence;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Persistence.Seed;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public class FlagshipPersonasBackfillSeedTests
{
    [Fact]
    public async Task Adds_missing_flagship_personas_for_each_tenant()
    {
        var (db, appDb, tenantA, tenantB) = await SetupAsync();

        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantA, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantA, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateStudent(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateTeacher(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateParent(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateEditor(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateApprover(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateClient(tenantB, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await FlagshipPersonasBackfillSeed.SeedAsync(db, appDb, default);

        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantA)).Should().Be(8);
        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantB)).Should().Be(8);
    }

    [Fact]
    public async Task Is_idempotent_on_second_run()
    {
        var (db, appDb, tenantA, _) = await SetupAsync();
        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantA, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantA, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await FlagshipPersonasBackfillSeed.SeedAsync(db, appDb, default);
        await FlagshipPersonasBackfillSeed.SeedAsync(db, appDb, default);

        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantA)).Should().Be(8);
    }

    private static async Task<(AiDbContext db, IApplicationDbContext appDb, Guid tenantA, Guid tenantB)> SetupAsync()
    {
        var dbName = $"backfill-{Guid.NewGuid():N}";
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns((Guid?)null);

        var appOpts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{dbName}-app")
            .Options;
        var appDb = new ApplicationDbContext(appOpts, cu.Object);

        var aiOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"{dbName}-ai")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var aiDb = new AiDbContext(aiOpts, cu.Object);

        var tenantA = Starter.Domain.Tenants.Entities.Tenant.Create("Acme", "acme");
        var tenantB = Starter.Domain.Tenants.Entities.Tenant.Create("Globex", "globex");
        appDb.Tenants.Add(tenantA);
        appDb.Tenants.Add(tenantB);
        await appDb.SaveChangesAsync();

        return (aiDb, appDb, tenantA.Id, tenantB.Id);
    }
}
