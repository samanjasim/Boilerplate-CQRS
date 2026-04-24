using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Queries.GetAssistants;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Starter.Shared.Results;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class GetAssistantsPersonaFilterTests
{
    private static (GetAssistantsQueryHandler handler, AiDbContext db) Setup(
        Guid tenant,
        Guid user,
        PersonaContext? setAccessor = null,
        bool personasEnabled = true,
        PersonaContext? resolverReturns = null)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.SetupGet(x => x.UserId).Returns(user);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"list-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var access = new Mock<IResourceAccessService>();
        access.Setup(x => x.ResolveAccessibleResourcesAsync(
                It.IsAny<ICurrentUserService>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessResolution(IsAdminBypass: true, ExplicitGrantedResourceIds: Array.Empty<Guid>()));

        var accessor = new PersonaContextAccessor();
        if (setAccessor is not null) accessor.Set(setAccessor);

        var resolver = new Mock<IPersonaResolver>();
        if (resolverReturns is not null)
            resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(resolverReturns));
        else
            resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Failure<PersonaContext>(
                    Starter.Module.AI.Domain.Errors.PersonaErrors.NoDefaultForUser));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Personas:Enabled"] = personasEnabled.ToString()
            })
            .Build();

        return (new GetAssistantsQueryHandler(db, access.Object, cu.Object, resolver.Object, accessor, config), db);
    }

    private static AiAssistant SeedTenantWideAssistant(AiDbContext db, Guid tenant, string slug, params string[] personaTargets)
    {
        var a = AiAssistant.Create(tenant, slug, null, "prompt", Guid.NewGuid(), slug: slug);
        if (personaTargets.Length > 0) a.SetPersonaTargets(personaTargets);
        a.SetVisibility(ResourceVisibility.TenantWide);
        db.AiAssistants.Add(a);
        db.SaveChanges();
        return a;
    }

    [Fact]
    public async Task Filters_By_Current_Persona_When_Accessor_Populated()
    {
        var tenant = Guid.NewGuid();
        var persona = new PersonaContext(Guid.NewGuid(), "student",
            PersonaAudienceType.Internal, SafetyPreset.ChildSafe, Array.Empty<string>());
        var (h, db) = Setup(tenant, Guid.NewGuid(), setAccessor: persona);

        SeedTenantWideAssistant(db, tenant, "tutor", "student");
        SeedTenantWideAssistant(db, tenant, "admin-copilot", "teacher");

        var r = await h.Handle(new GetAssistantsQuery(1, 50, null, null), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Should().ContainSingle();
        r.Value.Items[0].Slug.Should().Be("tutor");
    }

    [Fact]
    public async Task Resolves_Default_Persona_When_Accessor_Empty()
    {
        var tenant = Guid.NewGuid();
        var persona = new PersonaContext(Guid.NewGuid(), "default",
            PersonaAudienceType.Internal, SafetyPreset.Standard, Array.Empty<string>());
        var (h, db) = Setup(tenant, Guid.NewGuid(), setAccessor: null, resolverReturns: persona);

        SeedTenantWideAssistant(db, tenant, "brand-content-agent", "default");
        SeedTenantWideAssistant(db, tenant, "teacher-tutor", "teacher");

        var r = await h.Handle(new GetAssistantsQuery(1, 50, null, null), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Should().ContainSingle(a => a.Slug == "brand-content-agent");
    }

    [Fact]
    public async Task Feature_Flag_Off_Returns_All_Assistants_Unfiltered()
    {
        var tenant = Guid.NewGuid();
        var (h, db) = Setup(tenant, Guid.NewGuid(), personasEnabled: false);

        SeedTenantWideAssistant(db, tenant, "tutor", "student");
        SeedTenantWideAssistant(db, tenant, "admin-copilot", "teacher");

        var r = await h.Handle(new GetAssistantsQuery(1, 50, null, null), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Resolver_Failure_Falls_Through_To_Unfiltered_List()
    {
        // When the user has no default persona, the list still works but returns everything.
        // The spec keeps the resolver as a best-effort narrowing filter here; hard-failing
        // a list endpoint because the caller has no default persona would be user-hostile.
        var tenant = Guid.NewGuid();
        var (h, db) = Setup(tenant, Guid.NewGuid(), setAccessor: null, resolverReturns: null);

        SeedTenantWideAssistant(db, tenant, "tutor", "student");

        var r = await h.Handle(new GetAssistantsQuery(1, 50, null, null), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Should().ContainSingle();
    }
}
