using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallTemplateCommandHandlerTests
{
    private static (
        InstallTemplateCommandHandler handler,
        AiDbContext db,
        Mock<ICurrentUserService> currentUser,
        Mock<IAiToolRegistry> tools)
        Setup(
            Guid? callerTenantId,
            IEnumerable<IAiAgentTemplate>? templates = null,
            IEnumerable<string>? toolSlugs = null,
            bool callerIsSuperAdmin = false)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(callerTenantId);
        cu.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(callerIsSuperAdmin);

        var dbOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"install-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(dbOpts, cu.Object);

        var registry = new AiAgentTemplateRegistry(templates ?? Array.Empty<IAiAgentTemplate>());

        var toolReg = new Mock<IAiToolRegistry>();
        var registeredSlugs = new HashSet<string>(toolSlugs ?? Array.Empty<string>(), StringComparer.Ordinal);
        toolReg.Setup(x => x.FindByName(It.IsAny<string>()))
            .Returns<string>(name =>
            {
                if (!registeredSlugs.Contains(name))
                    return null;
                var def = new Mock<IAiToolDefinition>();
                def.SetupGet(x => x.Name).Returns(name);
                return def.Object;
            });

        var handler = new InstallTemplateCommandHandler(
            db, registry, toolReg.Object, cu.Object);

        return (handler, db, cu, toolReg);
    }

    private static IAiAgentTemplate MakeTemplate(
        string slug = "my_template",
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? personas = null) =>
        new TestTemplate(
            slug: slug,
            tools: tools ?? Array.Empty<string>(),
            personas: personas ?? new[] { "default" });

    private static AiPersona MakeDefaultPersona(Guid tenantId) =>
        AiPersona.CreateDefault(tenantId, Guid.NewGuid());

    [Fact]
    public async Task Happy_path_creates_assistant_with_provenance_stamped()
    {
        var tenantId = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", tools: new[] { "list_users" }, personas: new[] { "default" });
        var (handler, db, _, _) = Setup(
            callerTenantId: tenantId,
            templates: new[] { template },
            toolSlugs: new[] { "list_users" });

        db.AiPersonas.Add(MakeDefaultPersona(tenantId));
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new InstallTemplateCommand("x"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.Slug.Should().Be("x");
        assistant.TenantId.Should().Be(tenantId);
        assistant.TemplateSourceSlug.Should().Be("x");
        assistant.TemplateSourceVersion.Should().BeNull();
        assistant.EnabledToolNames.Should().Equal(new[] { "list_users" });
        assistant.PersonaTargetSlugs.Should().Equal(new[] { "default" });
    }

    [Fact]
    public async Task Returns_NotFound_when_template_slug_unknown()
    {
        var tenantId = Guid.NewGuid();
        var (handler, _, _, _) = Setup(callerTenantId: tenantId);

        var result = await handler.Handle(
            new InstallTemplateCommand("does_not_exist"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TemplateErrors.NotFound("x").Code);
    }

    [Fact]
    public async Task Returns_AlreadyInstalled_on_second_install_into_same_tenant()
    {
        var tenantId = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", personas: new[] { "default" });
        var (handler, db, _, _) = Setup(
            callerTenantId: tenantId,
            templates: new[] { template });

        db.AiPersonas.Add(MakeDefaultPersona(tenantId));
        await db.SaveChangesAsync();

        var first = await handler.Handle(new InstallTemplateCommand("x"), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(new InstallTemplateCommand("x"), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be(TemplateErrors.AlreadyInstalled("x", tenantId).Code);
    }

    [Fact]
    public async Task Returns_AlreadyInstalled_when_assistant_with_matching_name_exists()
    {
        var tenantId = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", personas: new[] { "default" });
        var (handler, db, _, _) = Setup(
            callerTenantId: tenantId,
            templates: new[] { template });

        db.AiPersonas.Add(MakeDefaultPersona(tenantId));
        // Pre-existing assistant with the same NAME but different SLUG.
        // template.DisplayName comes from TestTemplate's default — verify what it is.
        // For TestTemplate(slug: "x"), DisplayName is "x" (because displayName ?? slug).
        var existing = AiAssistant.Create(
            tenantId: tenantId,
            name: "x",                  // matches template.DisplayName for TestTemplate(slug: "x")
            description: null,
            systemPrompt: "Existing assistant",
            createdByUserId: Guid.NewGuid(),
            slug: "different_slug");
        db.AiAssistants.Add(existing);
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new InstallTemplateCommand("x"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Template.AlreadyInstalled");
    }

    [Fact]
    public async Task Returns_PersonaTargetMissing_when_persona_slug_does_not_exist()
    {
        var tenantId = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", personas: new[] { "missing_persona" });
        var (handler, _, _, _) = Setup(
            callerTenantId: tenantId,
            templates: new[] { template });

        var result = await handler.Handle(
            new InstallTemplateCommand("x"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TemplateErrors.PersonaTargetMissing("missing_persona").Code);
    }

    [Fact]
    public async Task Reserved_persona_slugs_are_accepted_without_db_row()
    {
        var tenantId = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", personas: new[] { "anonymous" });
        var (handler, _, _, _) = Setup(
            callerTenantId: tenantId,
            templates: new[] { template });

        var result = await handler.Handle(
            new InstallTemplateCommand("x"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Returns_ToolMissing_when_tool_slug_not_in_registry()
    {
        var tenantId = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", tools: new[] { "ghost_tool" }, personas: new[] { "default" });
        var (handler, db, _, _) = Setup(
            callerTenantId: tenantId,
            templates: new[] { template },
            toolSlugs: new[] { "list_users" });

        db.AiPersonas.Add(MakeDefaultPersona(tenantId));
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new InstallTemplateCommand("x"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TemplateErrors.ToolMissing("ghost_tool").Code);
    }

    [Fact]
    public async Task Cross_tenant_install_without_superadmin_returns_forbidden()
    {
        var callerTenant = Guid.NewGuid();
        var targetTenant = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", personas: new[] { "default" });
        var (handler, _, _, _) = Setup(
            callerTenantId: callerTenant,
            templates: new[] { template },
            callerIsSuperAdmin: false);

        var result = await handler.Handle(
            new InstallTemplateCommand("x", TargetTenantId: targetTenant),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TemplateErrors.Forbidden().Code);
    }

    [Fact]
    public async Task Cross_tenant_install_with_superadmin_writes_to_target_tenant()
    {
        var callerTenant = Guid.NewGuid();
        var targetTenant = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", personas: new[] { "default" });
        var (handler, db, _, _) = Setup(
            callerTenantId: callerTenant,
            templates: new[] { template },
            callerIsSuperAdmin: true);

        db.AiPersonas.Add(MakeDefaultPersona(targetTenant));
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new InstallTemplateCommand("x", TargetTenantId: targetTenant),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var assistant = await db.AiAssistants.IgnoreQueryFilters().SingleAsync();
        assistant.TenantId.Should().Be(targetTenant);
    }

    [Fact]
    public async Task Seed_path_with_user_id_override_creates_assistant_with_that_owner()
    {
        var tenantId = Guid.NewGuid();
        var template = MakeTemplate(slug: "x", personas: new[] { "default" });
        var (handler, db, cu, _) = Setup(
            callerTenantId: tenantId,
            templates: new[] { template });

        db.AiPersonas.Add(MakeDefaultPersona(tenantId));
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new InstallTemplateCommand("x", CreatedByUserIdOverride: Guid.Empty),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.CreatedByUserId.Should().Be(Guid.Empty);
        cu.VerifyGet(x => x.UserId, Times.Never);
    }
}
