using FluentAssertions;
using Starter.Abstractions.Modularity;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class ModuleNameUniquenessTests
{
    [Fact]
    public void All_loaded_modules_have_unique_names()
    {
        // IModule.Name keys the dependency-resolution dictionary in ModuleLoader.ResolveOrder.
        // Two modules sharing a name would throw at startup (now with a helpful error), but the
        // catalog enforces this at test time so the failure surfaces during CI rather than at boot.
        // See spec 2026-04-29-modularity-tier-2-5-hardening.md §2 Theme 3.
        var modules = ModuleLoader.DiscoverModules();
        var duplicates = modules
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' is declared by: {string.Join(", ", g.Select(m => m.GetType().FullName))}")
            .ToList();

        duplicates.Should().BeEmpty();
    }

    [Fact]
    public void All_loaded_modules_have_a_non_empty_name()
    {
        var modules = ModuleLoader.DiscoverModules();
        var bad = modules.Where(m => string.IsNullOrWhiteSpace(m.Name))
            .Select(m => m.GetType().FullName)
            .ToList();

        bad.Should().BeEmpty();
    }
}
