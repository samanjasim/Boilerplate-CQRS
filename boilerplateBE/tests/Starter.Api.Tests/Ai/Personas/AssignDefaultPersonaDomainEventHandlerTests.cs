using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.EventHandlers;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AssignDefaultPersonaDomainEventHandlerTests
{
    private static (AssignDefaultPersonaDomainEventHandler h, AiDbContext db) Setup()
    {
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"assign-{Guid.NewGuid()}").Options;
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns((Guid?)null);
        var db = new AiDbContext(opts, cu.Object);
        var h = new AssignDefaultPersonaDomainEventHandler(db,
            NullLogger<AssignDefaultPersonaDomainEventHandler>.Instance);
        return (h, db);
    }

    [Fact]
    public async Task Assigns_Default_Persona_If_Present()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        var def = AiPersona.CreateDefault(tenant, Guid.NewGuid());
        db.AiPersonas.Add(def);
        await db.SaveChangesAsync();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "User", tenant), CancellationToken.None);

        var row = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == user);
        row.Should().NotBeNull();
        row!.PersonaId.Should().Be(def.Id);
        row.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Falls_Back_To_Active_Internal_Persona_When_No_Default()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        var teacher = AiPersona.Create(tenant, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(teacher);
        await db.SaveChangesAsync();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "User", tenant), CancellationToken.None);

        var row = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == user);
        row.Should().NotBeNull();
        row!.PersonaId.Should().Be(teacher.Id);
    }

    [Fact]
    public async Task No_Tenant_Id_Skips_Assignment()
    {
        var (h, db) = Setup();
        var user = Guid.NewGuid();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "Name", null), CancellationToken.None);

        var any = await db.UserPersonas.IgnoreQueryFilters().AnyAsync();
        any.Should().BeFalse();
    }

    [Fact]
    public async Task No_Internal_Personas_Skips_Assignment()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenant, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "Name", tenant), CancellationToken.None);

        var any = await db.UserPersonas.IgnoreQueryFilters().AnyAsync(up => up.UserId == user);
        any.Should().BeFalse();
    }
}
