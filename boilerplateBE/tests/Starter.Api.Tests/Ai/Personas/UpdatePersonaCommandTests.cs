using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.UpdatePersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class UpdatePersonaCommandTests
{
    private static (UpdatePersonaCommandHandler h, AiDbContext db) Setup(Guid tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"upd-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new UpdatePersonaCommandHandler(db), db);
    }

    [Fact]
    public async Task Updates_Mutable_Fields()
    {
        var tenant = Guid.NewGuid();
        var (h, db) = Setup(tenant);

        var p = AiPersona.Create(tenant, "client", "Client", null,
            PersonaAudienceType.EndCustomer, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        await db.SaveChangesAsync();

        var result = await h.Handle(new UpdatePersonaCommand(
            p.Id, "External Client", "desc", SafetyPreset.ProfessionalModerated,
            new[] { "brand-content-agent" }, IsActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("External Client");
        result.Value.SafetyPreset.Should().Be(SafetyPreset.ProfessionalModerated);
    }

    [Fact]
    public async Task Missing_Persona_Returns_NotFound()
    {
        var (h, _) = Setup(Guid.NewGuid());

        var r = await h.Handle(new UpdatePersonaCommand(
            Guid.NewGuid(), "x", null, SafetyPreset.Standard, null, true), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Persona.NotFound");
    }
}
