using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.UnassignPersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class UnassignPersonaCommandTests
{
    private static (UnassignPersonaCommandHandler h, AiDbContext db) Setup(Guid tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"unassign-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new UnassignPersonaCommandHandler(db), db);
    }

    [Fact]
    public async Task Cannot_Remove_Last_Assignment()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var u = Guid.NewGuid();
        var p = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        db.UserPersonas.Add(UserPersona.Create(u, p.Id, t, true, null));
        await db.SaveChangesAsync();

        var r = await h.Handle(new UnassignPersonaCommand(p.Id, u), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Persona.CannotRemoveLastAssignment");
    }

    [Fact]
    public async Task Cross_Tenant_Unassign_Returns_NotFound()
    {
        // Tenant A owns the assignment; Tenant B admin tries to unassign.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var user = Guid.NewGuid();

        // Build with tenantB's context (caller)
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenantB);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"xt-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        // Tenant A's persona + assignment (written under null tenant context to bypass filter)
        var cuNull = new Mock<ICurrentUserService>();
        cuNull.SetupGet(x => x.TenantId).Returns((Guid?)null);
        using (var seedDb = new AiDbContext(opts, cuNull.Object))
        {
            var p = AiPersona.Create(tenantA, "teacher", "Teacher", null,
                PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
            seedDb.AiPersonas.Add(p);
            seedDb.UserPersonas.Add(UserPersona.Create(user, p.Id, tenantA, true, null));
            await seedDb.SaveChangesAsync();
        }

        // Tenant B calls unassign — should NotFound via query filter
        var handler = new UnassignPersonaCommandHandler(db);
        var result = await handler.Handle(new UnassignPersonaCommand(
            PersonaId: await db.AiPersonas.IgnoreQueryFilters().Select(p => p.Id).FirstAsync(),
            UserId: user), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Persona.NotFound");
    }

    [Fact]
    public async Task Removing_Default_Promotes_Other_To_Default()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var u = Guid.NewGuid();
        var p1 = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        var p2 = AiPersona.Create(t, "student", "Student", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.AddRange(p1, p2);
        db.UserPersonas.Add(UserPersona.Create(u, p1.Id, t, true, null));
        db.UserPersonas.Add(UserPersona.Create(u, p2.Id, t, false, null));
        await db.SaveChangesAsync();

        var r = await h.Handle(new UnassignPersonaCommand(p1.Id, u), default);

        r.IsSuccess.Should().BeTrue();
        var remaining = await db.UserPersonas.IgnoreQueryFilters().SingleAsync();
        remaining.PersonaId.Should().Be(p2.Id);
        remaining.IsDefault.Should().BeTrue();
    }
}
