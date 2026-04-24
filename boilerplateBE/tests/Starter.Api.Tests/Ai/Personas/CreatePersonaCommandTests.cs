using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.CreatePersona;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class CreatePersonaCommandTests
{
    private static (CreatePersonaCommandHandler h, AiDbContext db) Setup(Guid tenant, Guid user)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.SetupGet(x => x.UserId).Returns(user);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"create-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new CreatePersonaCommandHandler(db, cu.Object, new SlugGenerator()), db);
    }

    [Fact]
    public async Task Happy_Path_Creates_Persona_With_Auto_Slug()
    {
        var tenant = Guid.NewGuid();
        var (h, _) = Setup(tenant, Guid.NewGuid());

        var result = await h.Handle(new CreatePersonaCommand(
            "Brand Content Agent", "desc", Slug: null,
            PersonaAudienceType.Internal, SafetyPreset.Standard,
            PermittedAgentSlugs: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("brand-content-agent");
    }

    [Fact]
    public async Task Slug_Collision_Returns_Error()
    {
        var tenant = Guid.NewGuid();
        var (h, _) = Setup(tenant, Guid.NewGuid());
        await h.Handle(new CreatePersonaCommand("Teacher", null, "teacher",
            PersonaAudienceType.Internal, SafetyPreset.Standard, null), CancellationToken.None);

        var result = await h.Handle(new CreatePersonaCommand("Teacher Again", null, "teacher",
            PersonaAudienceType.Internal, SafetyPreset.Standard, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Persona.SlugAlreadyExists");
    }

    [Fact]
    public async Task PermittedAgentSlugs_Are_Persisted()
    {
        var tenant = Guid.NewGuid();
        var (h, _) = Setup(tenant, Guid.NewGuid());

        var result = await h.Handle(new CreatePersonaCommand(
            "Student", null, "student",
            PersonaAudienceType.Internal, SafetyPreset.ChildSafe,
            new[] { "tutor", "reading-coach" }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PermittedAgentSlugs.Should().BeEquivalentTo(new[] { "tutor", "reading-coach" });
    }
}
