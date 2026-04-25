using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateDiscoveryTests
{
    [Fact]
    public void Scanner_registers_concrete_parameterless_templates()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(FixtureTemplateA).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        Assert.Contains(registered, t => t.Slug == "fixture_a");
        Assert.Contains(registered, t => t.Slug == "fixture_b");
    }

    [Fact]
    public void Scanner_skips_abstract_types()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(AbstractFixtureTemplate).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        Assert.DoesNotContain(registered, t => t.GetType().IsAbstract);
    }

    [Fact]
    public void Scanner_skips_types_without_parameterless_ctor()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(NoParameterlessCtorFixtureTemplate).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        Assert.DoesNotContain(
            registered,
            t => t.GetType().Name.Contains("NoParameterlessCtor", StringComparison.Ordinal));
    }

    [Fact]
    public void Scanner_decorates_registrations_with_module_source()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(FixtureTemplateA).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        var a = registered.First(t => t.Slug == "fixture_a");
        var src = Assert.IsAssignableFrom<IAiAgentTemplateModuleSource>(a);
        // The test assembly's name is "Starter.Api.Tests" — DeriveModuleSource
        // strips "Starter." prefix to "Api.Tests".
        Assert.Equal("Api.Tests", src.ModuleSource);
    }

    [Theory]
    [InlineData("Starter.Application", "Core")]
    [InlineData("Starter.Module.Products", "Products")]
    [InlineData("Starter.Module.AI", "AI")]
    [InlineData("Starter.Foo", "Foo")]
    [InlineData("Other.Library", "Other.Library")]
    public void DeriveModuleSource_strips_well_known_prefixes(string assemblyName, string expected)
    {
        Assert.Equal(
            expected,
            AiAgentTemplateDiscoveryExtensions.DeriveModuleSource(assemblyName));
    }
}

public class AiAgentTemplateValidateShapeTests
{
    [Fact]
    public void ValidateShape_rejects_empty_slug()
    {
        var t = new TestTemplate(slug: "");
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("Slug", ex.Message);
    }

    [Fact]
    public void ValidateShape_rejects_too_long_slug()
    {
        var t = new TestTemplate(slug: new string('a', 129));
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("128", ex.Message);
    }

    [Fact]
    public void ValidateShape_rejects_empty_system_prompt()
    {
        var t = new TestTemplate(systemPrompt: "");
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("SystemPrompt", ex.Message);
    }

    [Fact]
    public void ValidateShape_rejects_empty_model()
    {
        var t = new TestTemplate(model: "");
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("Model", ex.Message);
    }

    [Fact]
    public void ValidateShape_accepts_well_formed_template()
    {
        var t = new TestTemplate();
        AiAgentTemplateDiscoveryExtensions.ValidateShape(t);
        // No throw = pass.
    }
}
