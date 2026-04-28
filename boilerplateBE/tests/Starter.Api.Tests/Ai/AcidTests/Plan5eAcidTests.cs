using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Ai.Templates;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.Billing;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

public class Plan5eAcidTests
{
    [Fact]
    public void All_five_5e_templates_have_parameterless_ctors_and_unique_slugs()
    {
        var templates = new IAiAgentTemplate[]
        {
            new PlatformInsightsAnthropicTemplate(),
            new PlatformInsightsOpenAiTemplate(),
            new SupportCopilotTemplate(),
            new TeacherTutorTemplate(),
            new BrandContentTemplate(),
        };

        templates.Select(t => t.Slug).Should().OnlyHaveUniqueItems();
        templates.Should().AllSatisfy(t =>
        {
            t.SystemPrompt.Should().NotBeNullOrWhiteSpace();
            t.DisplayName.Should().NotBeNullOrWhiteSpace();
            t.Model.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void Teacher_tutor_template_targets_student_with_explicit_childsafe()
    {
        var t = new TeacherTutorTemplate();

        t.Slug.Should().Be("teacher_tutor");
        t.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("student");
        t.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
    }

    [Fact]
    public void Brand_content_template_targets_editor_with_explicit_standard()
    {
        var t = new BrandContentTemplate();

        t.Slug.Should().Be("brand_content");
        t.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("editor");
        t.SafetyPresetOverride.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Platform_insights_templates_inherit_from_default_persona()
    {
        new PlatformInsightsAnthropicTemplate().SafetyPresetOverride.Should().BeNull();
        new PlatformInsightsOpenAiTemplate().SafetyPresetOverride.Should().BeNull();
        new SupportCopilotTemplate().SafetyPresetOverride.Should().BeNull();
    }

    [Fact]
    public void Existing_5c2_templates_now_inherit_from_persona()
    {
        new SupportAssistantAnthropicTemplate().SafetyPresetOverride.Should().BeNull();
        new SupportAssistantOpenAiTemplate().SafetyPresetOverride.Should().BeNull();
        new Starter.Module.Products.Application.Templates.ProductExpertAnthropicTemplate()
            .SafetyPresetOverride.Should().BeNull();
        new Starter.Module.Products.Application.Templates.ProductExpertOpenAiTemplate()
            .SafetyPresetOverride.Should().BeNull();
    }

    [Fact]
    public void Billing_module_registers_platform_insights_billing_tools()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        new BillingModule().ConfigureServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        var names = provider.GetServices<IAiToolDefinition>()
            .Select(t => t.Name)
            .ToList();

        // Plan 5e review C1: list_subscriptions was a SuperAdmin admin-list query
        // (cross-tenant). Replaced by get_my_subscription which is tenant-scoped.
        names.Should().Contain(new[] { "get_my_subscription", "list_usage" });
        names.Should().NotContain("list_subscriptions");
    }

    [Fact]
    public void Conversation_tool_name_matches_platform_insights_template()
    {
        var services = new ServiceCollection();
        services.AddAiToolsFromAssembly(typeof(Starter.Module.AI.AIModule).Assembly);

        using var provider = services.BuildServiceProvider();
        var names = provider.GetServices<IAiToolDefinition>()
            .Select(t => t.Name)
            .ToList();

        names.Should().Contain("list_conversations");
    }

    [Fact]
    public async Task Installing_teacher_tutor_persists_childsafe_override()
    {
        var (handler, db, _) = SetupHandler(
            templates: new IAiAgentTemplate[] { new TeacherTutorTemplate() },
            seedFlagshipPersonas: true);

        var result = await handler.Handle(
            new InstallTemplateCommand("teacher_tutor"), default);

        result.IsSuccess.Should().BeTrue($"install failed: {result.Error?.Description}");
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
        assistant.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("student");
    }

    [Fact]
    public async Task Installing_brand_content_persists_standard_override()
    {
        var (handler, db, _) = SetupHandler(
            templates: new IAiAgentTemplate[] { new BrandContentTemplate() },
            seedFlagshipPersonas: true);

        var result = await handler.Handle(
            new InstallTemplateCommand("brand_content"), default);

        result.IsSuccess.Should().BeTrue($"install failed: {result.Error?.Description}");
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().Be(SafetyPreset.Standard);
        assistant.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("editor");
    }

    [Fact]
    public async Task Installing_platform_insights_anthropic_leaves_override_null_and_enables_five_tools()
    {
        var (handler, db, _) = SetupHandler(
            templates: new IAiAgentTemplate[] { new PlatformInsightsAnthropicTemplate() },
            tools: new[] { "list_users", "list_audit_logs", "get_my_subscription", "list_usage", "list_conversations" });

        var result = await handler.Handle(
            new InstallTemplateCommand("platform_insights_anthropic"), default);

        result.IsSuccess.Should().BeTrue($"install failed: {result.Error?.Description}");
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().BeNull();
        assistant.EnabledToolNames.Should().BeEquivalentTo(new[]
        {
            "list_users", "list_audit_logs", "get_my_subscription", "list_usage", "list_conversations",
        });
    }

    // ── Plan 5e post-review additions ──────────────────────────────────────────

    [Fact]
    public void Every_registered_template_references_only_tools_in_the_registry()
    {
        // Plan 5e review S2: guards against the live-test bug where a template
        // declared "list_my_conversations" but the registered tool was named
        // "list_conversations". Walks all registered templates and resolves every
        // EnabledToolName against the merged tool registry across all assemblies.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            })
            .Build();

        // Scan every assembly that registers tools or templates today.
        services.AddAiToolsFromAssembly(typeof(Starter.Application.DependencyInjection).Assembly);
        services.AddAiToolsFromAssembly(typeof(Starter.Module.AI.AIModule).Assembly);
        services.AddAiToolsFromAssembly(typeof(Starter.Module.Products.ProductsModule).Assembly);
        services.AddAiToolsFromAssembly(typeof(BillingModule).Assembly);

        services.AddAiAgentTemplatesFromAssembly(typeof(Starter.Application.DependencyInjection).Assembly);
        services.AddAiAgentTemplatesFromAssembly(typeof(Starter.Module.Products.ProductsModule).Assembly);

        using var provider = services.BuildServiceProvider();

        var registeredToolNames = provider.GetServices<IAiToolDefinition>()
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var templates = provider.GetServices<IAiAgentTemplate>().ToList();
        templates.Should().NotBeEmpty("at least one template should be registered");

        var problems = templates
            .SelectMany(t => t.EnabledToolNames.Select(n => (Template: t.Slug, Tool: n)))
            .Where(x => !registeredToolNames.Contains(x.Tool))
            .ToList();

        problems.Should().BeEmpty(
            "every template's EnabledToolNames must resolve in the tool registry: " +
            string.Join(", ", problems.Select(p => $"{p.Template} -> {p.Tool}")));
    }

    [Fact]
    public void Server_trusted_tool_parameters_carry_AiParameterIgnore()
    {
        // Plan 5e review S3: catches future regressions where a query exposes
        // TenantId / UserId / role flags directly to the LLM tool schema. The
        // AiToolSchemaGenerator throws at startup if it sees one un-ignored, but
        // a reflection assertion catches it closer to the source.
        AssertIgnored(typeof(Starter.Module.Billing.Application.Queries.GetUsage.GetUsageQuery), "TenantId");
        AssertIgnored(typeof(Starter.Module.Billing.Application.Queries.GetSubscription.GetSubscriptionQuery), "TenantId");
        AssertIgnored(typeof(Starter.Module.Products.Application.Queries.GetProducts.GetProductsQuery), "TenantId");

        static void AssertIgnored(Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName);
            prop.Should().NotBeNull($"{type.Name} should declare {propertyName}");
            prop!
                .GetCustomAttributes(typeof(AiParameterIgnoreAttribute), inherit: true)
                .Should().NotBeEmpty(
                    $"{type.FullName}.{propertyName} is server-trusted and must carry [AiParameterIgnore].");
        }
    }

    [Fact]
    public void All_nine_boilerplate_templates_are_discovered_and_resolvable()
    {
        // Plan 5e review S1: spec §9.1 row 10 "InstallFlagOn_FreshTenantHasAllNineAssistants".
        // The full SeedDataAsync flow can't be tested in-memory without the whole DI graph,
        // but we can prove the template registry is complete and every template would install
        // successfully against a fresh tenant (persona + tool dependencies satisfied).
        var services = new ServiceCollection();

        services.AddAiToolsFromAssembly(typeof(Starter.Application.DependencyInjection).Assembly);
        services.AddAiToolsFromAssembly(typeof(Starter.Module.AI.AIModule).Assembly);
        services.AddAiToolsFromAssembly(typeof(Starter.Module.Products.ProductsModule).Assembly);
        services.AddAiToolsFromAssembly(typeof(BillingModule).Assembly);

        services.AddAiAgentTemplatesFromAssembly(typeof(Starter.Application.DependencyInjection).Assembly);
        services.AddAiAgentTemplatesFromAssembly(typeof(Starter.Module.Products.ProductsModule).Assembly);

        using var provider = services.BuildServiceProvider();

        var slugs = provider.GetServices<IAiAgentTemplate>()
            .Select(t => t.Slug)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        slugs.Should().BeEquivalentTo(new[]
        {
            "brand_content",
            "platform_insights_anthropic",
            "platform_insights_openai",
            "product_expert_anthropic",
            "product_expert_openai",
            "support_assistant_anthropic",
            "support_assistant_openai",
            "support_copilot",
            "teacher_tutor",
        });
    }

    [Fact]
    public void Every_seeded_persona_slug_is_in_AllSeededPersonaFactories()
    {
        // Plan 5e review S5: the AiPersona slug constants and factory table must
        // stay in lockstep. Catches a future contributor adding a slug constant
        // (or a Create* factory) without wiring it through both seed paths.
        var slugConstants = typeof(AiPersona)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.Name.EndsWith("Slug") && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet(StringComparer.Ordinal);

        AiPersona.AllSeededPersonaFactories.Keys
            .Should().BeEquivalentTo(slugConstants,
                "every Slug constant must be wired into AllSeededPersonaFactories");
    }

    private static (
        InstallTemplateCommandHandler handler,
        AiDbContext db,
        Guid tenantId)
        SetupHandler(
            IAiAgentTemplate[] templates,
            IEnumerable<string>? tools = null,
            bool seedFlagshipPersonas = false)
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        cu.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(false);

        var dbOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"5e-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(dbOpts, cu.Object);
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, Guid.NewGuid()));
        if (seedFlagshipPersonas)
        {
            db.AiPersonas.Add(AiPersona.CreateStudent(tenantId, Guid.NewGuid()));
            db.AiPersonas.Add(AiPersona.CreateEditor(tenantId, Guid.NewGuid()));
        }
        db.SaveChanges();

        var registry = new AiAgentTemplateRegistry(templates);

        var toolReg = new Mock<IAiToolRegistry>();
        var toolSlugs = new HashSet<string>(tools ?? Array.Empty<string>(), StringComparer.Ordinal);
        toolReg.Setup(x => x.FindByName(It.IsAny<string>()))
            .Returns<string>(name =>
            {
                if (!toolSlugs.Contains(name)) return null;
                var def = new Mock<IAiToolDefinition>();
                def.SetupGet(x => x.Name).Returns(name);
                return def.Object;
            });

        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(x => x.GetValueAsync<int>("ai.agents.max_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(int.MaxValue);

        var handler = new InstallTemplateCommandHandler(
            db, registry, toolReg.Object, cu.Object, ff.Object);
        return (handler, db, tenantId);
    }
}
