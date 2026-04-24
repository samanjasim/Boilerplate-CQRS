using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class PersonaResolverTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static (PersonaResolver resolver, AiDbContext db) Setup(
        Guid? currentUserId,
        Guid? currentTenantId,
        Action<AiDbContext>? seed = null)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(currentUserId);
        cu.SetupGet(x => x.TenantId).Returns(currentTenantId);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"resolver-{Guid.NewGuid()}")
            .Options;
        var db = new AiDbContext(opts, cu.Object);
        seed?.Invoke(db);
        db.SaveChanges();

        return (new PersonaResolver(db, cu.Object), db);
    }

    [Fact]
    public async Task Authenticated_User_Default_Persona_Is_Returned_When_No_Override()
    {
        var (resolver, _) = Setup(User, Tenant, db =>
        {
            var p = AiPersona.CreateDefault(Tenant, User);
            db.AiPersonas.Add(p);
            db.UserPersonas.Add(UserPersona.Create(User, p.Id, Tenant, isDefault: true, null));
        });

        var result = await resolver.ResolveAsync(explicitPersonaId: null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("default");
    }

    [Fact]
    public async Task Authenticated_User_No_Default_Returns_Error()
    {
        var (resolver, _) = Setup(User, Tenant);

        var result = await resolver.ResolveAsync(null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.NoDefaultForUser.Code);
    }

    [Fact]
    public async Task Override_Not_Assigned_To_User_Returns_Error()
    {
        Guid pid = default!;
        var (resolver, _) = Setup(User, Tenant, db =>
        {
            var p = AiPersona.Create(Tenant, "student", "Student", null,
                PersonaAudienceType.Internal, SafetyPreset.ChildSafe, User);
            db.AiPersonas.Add(p);
            pid = p.Id;
        });

        var result = await resolver.ResolveAsync(pid, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.NotAssignedToUser.Code);
    }

    [Fact]
    public async Task Override_Assigned_To_User_Returns_That_Persona()
    {
        Guid pid = default!;
        var (resolver, _) = Setup(User, Tenant, db =>
        {
            var def = AiPersona.CreateDefault(Tenant, User);
            var s = AiPersona.Create(Tenant, "student", "Student", null,
                PersonaAudienceType.Internal, SafetyPreset.ChildSafe, User);
            db.AiPersonas.AddRange(def, s);
            db.UserPersonas.Add(UserPersona.Create(User, def.Id, Tenant, true, null));
            db.UserPersonas.Add(UserPersona.Create(User, s.Id, Tenant, false, null));
            pid = s.Id;
        });

        var result = await resolver.ResolveAsync(pid, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("student");
    }

    [Fact]
    public async Task Unauthenticated_Falls_Back_To_Anonymous()
    {
        var (resolver, _) = Setup(currentUserId: null, currentTenantId: Tenant, db =>
        {
            var anon = AiPersona.CreateAnonymous(Tenant, Guid.NewGuid());
            anon.Update("Anonymous", null, SafetyPreset.Standard,
                Array.Empty<string>(), isActive: true);
            db.AiPersonas.Add(anon);
        });

        var result = await resolver.ResolveAsync(null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Audience.Should().Be(PersonaAudienceType.Anonymous);
    }

    [Fact]
    public async Task Unauthenticated_Without_Anonymous_Returns_Error()
    {
        var (resolver, _) = Setup(null, Tenant);

        var result = await resolver.ResolveAsync(null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.AnonymousNotAvailable.Code);
    }

    [Fact]
    public async Task Override_NotFound_Returns_Error()
    {
        var (resolver, _) = Setup(User, Tenant);

        var result = await resolver.ResolveAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.NotFound.Code);
    }
}
