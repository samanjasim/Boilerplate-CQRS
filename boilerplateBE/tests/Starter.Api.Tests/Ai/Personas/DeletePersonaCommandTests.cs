using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.DeletePersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class DeletePersonaCommandTests
{
    private static (DeletePersonaCommandHandler h, AiDbContext db) Setup(Guid tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"del-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new DeletePersonaCommandHandler(db), db);
    }

    [Fact]
    public async Task Deletes_Ordinary_Persona()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var p = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        await db.SaveChangesAsync();

        var r = await h.Handle(new DeletePersonaCommand(p.Id), default);

        r.IsSuccess.Should().BeTrue();
        (await db.AiPersonas.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_System_Reserved()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var p = AiPersona.CreateAnonymous(t, Guid.NewGuid());
        db.AiPersonas.Add(p);
        await db.SaveChangesAsync();

        var r = await h.Handle(new DeletePersonaCommand(p.Id), default);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Persona.CannotDeleteSystemReserved");
    }

    [Fact]
    public async Task Rejects_Persona_With_Assignments()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var p = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        db.UserPersonas.Add(UserPersona.Create(Guid.NewGuid(), p.Id, t, true, null));
        await db.SaveChangesAsync();

        var r = await h.Handle(new DeletePersonaCommand(p.Id), default);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Persona.HasActiveAssignments");
    }
}
