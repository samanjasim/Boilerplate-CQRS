using System.Reflection;
using MassTransit;
using FluentAssertions;
using Starter.Abstractions.Modularity;
using Starter.Api.Modularity;
using Xunit;

namespace Starter.Api.Tests.Architecture;

/// <summary>
/// Tier 2.5 Theme 5 — defense-in-depth around the module registry codegen.
///
/// The CI drift gate (<c>npm run verify:modules</c>) catches generator output
/// drift, but it can't catch the case where the generator silently drops a
/// module class or where someone adds a module to the catalog without running
/// the generator. This test asserts the generated <see cref="ModuleRegistry"/>
/// returns exactly the same set of <see cref="IModule"/> implementations as
/// reflection-based <see cref="ModuleLoader.DiscoverModules"/>.
///
/// If a test here fails: run <c>npm run generate:modules</c> from the repo
/// root and commit the regenerated artifacts.
/// </summary>
public class ModuleRegistryTests
{
    [Fact]
    public void ModuleRegistry_All_returns_the_same_module_set_as_DiscoverModules()
    {
        var registry = ModuleRegistry.All()
            .Select(m => m.GetType().FullName!)
            .ToHashSet(StringComparer.Ordinal);

        var discovered = ModuleLoader.DiscoverModules()
            .Select(m => m.GetType().FullName!)
            .ToHashSet(StringComparer.Ordinal);

        // Symmetric difference — surfaces both registry-only and discovery-only entries.
        var registryOnly = registry.Except(discovered).ToList();
        var discoveryOnly = discovered.Except(registry).ToList();

        registryOnly.Should().BeEmpty(
            "the generated registry must not include modules that the runtime cannot discover. " +
            "If this fails, the catalog references a module project whose IModule entry-point class " +
            "is not loadable from the test AppDomain. Offending entries: " +
            string.Join(", ", registryOnly));

        discoveryOnly.Should().BeEmpty(
            "every IModule the runtime can discover must be in the generated registry. " +
            "If this fails, modules.catalog.json is missing an entry or " +
            "`npm run generate:modules` has not been re-run after a catalog edit. " +
            "Offending entries: " + string.Join(", ", discoveryOnly));
    }

    [Fact]
    public void ModuleRegistry_All_returns_modules_in_a_stable_order()
    {
        // The emitter sorts catalog entries by id — the test asserts the runtime
        // observes that determinism so dependency-order resolution downstream
        // is predictable across machines.
        var first = ModuleRegistry.All().Select(m => m.GetType().FullName!).ToList();
        var second = ModuleRegistry.All().Select(m => m.GetType().FullName!).ToList();
        second.Should().Equal(first,
            "ModuleRegistry.All() must be stable across calls. The emitter sorts by " +
            "catalog id and emits a fixed array literal — drift here means someone " +
            "introduced randomness into the generator or the runtime.");
    }

    [Fact]
    public void Modules_with_MassTransit_consumers_implement_IModuleBusContributor()
    {
        // Tier 2.5 Theme 5 Phase D — the host no longer auto-discovers consumers
        // from module assemblies. Each module that ships an IConsumer<> must opt
        // in via IModuleBusContributor.ConfigureBus(bus) and call bus.AddConsumers
        // on its own assembly. This test catches modules that ship a consumer but
        // forgot to declare the contract — those consumers would be dead at runtime.
        var moduleAssemblies = ModuleLoader.DiscoverModules()
            .Select(m => m.GetType().Assembly)
            .Distinct()
            .ToList();

        var problems = new List<string>();
        foreach (var asm in moduleAssemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.OfType<Type>().ToArray(); }

            var consumers = types
                .Where(t => t is { IsAbstract: false, IsInterface: false })
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)))
                .ToList();

            if (consumers.Count == 0) continue;

            var moduleType = types.FirstOrDefault(t =>
                typeof(IModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

            if (moduleType is null) continue;

            if (!typeof(IModuleBusContributor).IsAssignableFrom(moduleType))
            {
                problems.Add(
                    $"{moduleType.FullName} declares MassTransit consumer(s) " +
                    $"({string.Join(", ", consumers.Select(c => c.Name))}) but does not " +
                    $"implement IModuleBusContributor. Move the consumer registration " +
                    $"into the module: `void ConfigureBus(IBusRegistrationConfigurator bus) " +
                    $"=> bus.AddConsumers(typeof({moduleType.Name}).Assembly);`.");
            }
        }

        problems.Should().BeEmpty(
            "every module shipping an IConsumer<> must declare IModuleBusContributor; " +
            "the host stopped scanning module assemblies in Tier 2.5 Theme 5 Phase D.");
    }

    [Fact]
    public void Generated_registry_file_carries_the_AUTO_GENERATED_header()
    {
        // If someone hand-edits the generated file and accidentally drops the
        // marker, future contributors might not realise regeneration overwrites
        // their edit. Same defensive pattern as PermissionCodegenTests.
        //
        // Generated apps (the killer-test matrix) do not ship `modules.catalog.json`
        // and the BE source folder is renamed to {Name}.Api — this assertion is a
        // source-repo invariant, so we no-op when the catalog isn't reachable.
        if (!TryResolveRepoRelative(out var root,
                "boilerplateBE", "src", "Starter.Api", "Modularity", "ModuleRegistry.g.cs"))
        {
            return;
        }
        File.ReadAllText(root).Should().Contain("AUTO-GENERATED");
    }

    private static bool TryResolveRepoRelative(out string path, params string[] segments)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "modules.catalog.json");
            if (File.Exists(candidate))
            {
                path = Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
                return true;
            }
            dir = dir.Parent;
        }
        path = string.Empty;
        return false;
    }
}
