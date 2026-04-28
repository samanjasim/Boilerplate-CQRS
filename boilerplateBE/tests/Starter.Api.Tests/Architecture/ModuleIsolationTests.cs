using FluentAssertions;
using NetArchTest.Rules;
using Starter.Api.Configurations;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class ModuleIsolationTests
{
    private static readonly string[] OptionalModuleNamespaces =
    [
        "Starter.Module.AI",
        "Starter.Module.Billing",
        "Starter.Module.CommentsActivity",
        "Starter.Module.Communication",
        "Starter.Module.ImportExport",
        "Starter.Module.Products",
        "Starter.Module.Webhooks",
        "Starter.Module.Workflow",
    ];

    [Fact]
    public void Starter_Api_must_not_use_types_from_optional_modules()
    {
        // Use any public type from Starter.Api to grab its assembly.
        var apiAssembly = typeof(OpenTelemetryConfiguration).Assembly;

        var result = Types.InAssembly(apiAssembly)
            .Should()
            .NotHaveDependencyOnAny(OptionalModuleNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Starter.Api is the module host — it must compose modules through neutral contracts " +
            "(IModule, IModuleBusContributor, etc.) and never reference optional module internals. " +
            "If this fails, move the offending logic into the relevant module and have it implement " +
            "the appropriate host contract. See docs/superpowers/specs/2026-04-28-hybrid-full-stack-module-system-design.md §6. " +
            "Offending types: " + (result.FailingTypes is null
                ? "<none>"
                : string.Join(", ", result.FailingTypes.Select(t => t.FullName))));
    }
}
