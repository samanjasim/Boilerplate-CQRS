using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.Personas.GetPersonas;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class GetPersonasQueryTests
{
    [Fact]
    public async Task Returns_All_When_IncludeSystem_And_IncludeInactive()
    {
        var t = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(t);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"q-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        db.AiPersonas.Add(AiPersona.CreateAnonymous(t, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(t, Guid.NewGuid()));
        var teacher = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        teacher.SetActive(false);
        db.AiPersonas.Add(teacher);
        await db.SaveChangesAsync();

        var h = new GetPersonasQueryHandler(db);
        var r = await h.Handle(new GetPersonasQuery(true, true), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Excludes_System_And_Inactive_By_Default()
    {
        var t = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(t);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"q-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        db.AiPersonas.Add(AiPersona.CreateAnonymous(t, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(t, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var h = new GetPersonasQueryHandler(db);
        var r = await h.Handle(new GetPersonasQuery(IncludeSystem: false, IncludeInactive: false), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().ContainSingle();
        r.Value[0].Slug.Should().Be(AiPersona.DefaultSlug);
    }
}
