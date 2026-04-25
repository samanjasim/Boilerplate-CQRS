using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.Personas.GetMePersonas;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class GetMePersonasQueryTests
{
    [Fact]
    public async Task Returns_Assigned_Personas_With_DefaultId()
    {
        var t = Guid.NewGuid();
        var u = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(u);
        cu.SetupGet(x => x.TenantId).Returns(t);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"me-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var p1 = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        var p2 = AiPersona.Create(t, "student", "Student", null,
            PersonaAudienceType.Internal, SafetyPreset.ChildSafe, Guid.NewGuid());
        db.AiPersonas.AddRange(p1, p2);
        db.UserPersonas.Add(UserPersona.Create(u, p1.Id, t, true, null));
        db.UserPersonas.Add(UserPersona.Create(u, p2.Id, t, false, null));
        await db.SaveChangesAsync();

        var h = new GetMePersonasQueryHandler(db, cu.Object);
        var r = await h.Handle(new GetMePersonasQuery(), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Personas.Should().HaveCount(2);
        r.Value.DefaultPersonaId.Should().Be(p1.Id);
    }
}
