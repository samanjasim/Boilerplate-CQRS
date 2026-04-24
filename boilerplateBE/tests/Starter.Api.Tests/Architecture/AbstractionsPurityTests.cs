using System.Reflection;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Architecture;

/// <summary>
/// Locks in the dependency rules for <c>Starter.Abstractions</c>. The contracts
/// project must stay pure: zero project references, only a handful of pure
/// interface packages from Microsoft.Extensions.*. It must NOT depend on
/// <c>Starter.Domain</c>, <c>Starter.Application</c>, <c>Starter.Infrastructure</c>,
/// the web helpers project, or any framework.
///
/// If a future change adds a forbidden reference, this test fails at CI and
/// the developer is forced to either revert or explicitly update the rule.
/// </summary>
public sealed class AbstractionsPurityTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Starter.Domain",
        "Starter.Application",
        "Starter.Infrastructure",
        "Starter.Shared",
        "Starter.Abstractions.Web",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "MassTransit",
    ];

    private static readonly string[] AllowedAssemblyPrefixes =
    [
        "Starter.Abstractions",
        "System",
        "netstandard",
        "MediatR",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Primitives",
    ];

    [Fact]
    public void Starter_Abstractions_must_not_depend_on_forbidden_assemblies()
    {
        var abstractionsAssembly = typeof(ICapability).Assembly;
        var referencedNames = abstractionsAssembly.GetReferencedAssemblies();

        var violations = referencedNames
            .Where(r => ForbiddenAssemblyPrefixes.Any(p => r.Name?.StartsWith(p, StringComparison.Ordinal) == true))
            .Select(r => r.Name)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Starter.Abstractions has forbidden references: {string.Join(", ", violations)}. " +
            $"The contracts project must stay pure — see ICapability.cs for the dependency rules.");
    }

    [Fact]
    public void Starter_Abstractions_references_must_all_be_on_the_allowlist()
    {
        var abstractionsAssembly = typeof(ICapability).Assembly;
        var referencedNames = abstractionsAssembly.GetReferencedAssemblies();

        var unexpected = referencedNames
            .Where(r => !AllowedAssemblyPrefixes.Any(p => r.Name?.StartsWith(p, StringComparison.Ordinal) == true))
            .Select(r => r.Name)
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            $"Starter.Abstractions has unexpected references: {string.Join(", ", unexpected)}. " +
            $"If this is intentional, add the prefix to the allowlist in AbstractionsPurityTests.");
    }
}
