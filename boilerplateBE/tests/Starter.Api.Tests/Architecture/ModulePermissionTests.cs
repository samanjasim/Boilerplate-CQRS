using System.Text.RegularExpressions;
using FluentAssertions;
using Starter.Abstractions.Modularity;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Architecture;

/// <summary>
/// Tier 2.5 Theme 3 (spec 2026-04-29-modularity-tier-2-5-hardening.md):
/// closes the "permission silently dropped/overwritten" failure modes surfaced
/// by the post-Tier-2 audit (BE#1, BE#7).
/// </summary>
public class ModulePermissionTests
{
    private static readonly Regex PermissionPattern = new(@"^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);

    [Fact]
    public void Module_permission_names_are_unique_across_modules_and_core()
    {
        var modules = ModuleLoader.DiscoverModules();
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        // Seed with core permissions so module collisions with core surface here too.
        foreach (var (name, _, module) in Permissions.GetAllWithMetadata())
        {
            owners[name] = $"core/{module}";
        }

        foreach (var module in modules)
        {
            foreach (var (name, _, _) in module.GetPermissions())
            {
                if (owners.TryGetValue(name, out var owner))
                    duplicates.Add($"'{name}' declared by both {owner} and {module.GetType().FullName}");
                else
                    owners[name] = module.GetType().FullName!;
            }
        }

        duplicates.Should().BeEmpty(
            "Two modules (or a module and core) declaring the same permission string causes the seeder " +
            "in DataSeeder.SeedPermissionsAsync to upsert one and silently lose the other. " +
            "Audit finding BE#1.");
    }

    [Fact]
    public void Module_permission_names_match_naming_convention()
    {
        var modules = ModuleLoader.DiscoverModules();
        var bad = new List<string>();

        foreach (var module in modules)
        {
            foreach (var (name, _, _) in module.GetPermissions())
            {
                if (!PermissionPattern.IsMatch(name))
                    bad.Add($"'{name}' from {module.GetType().FullName} does not match {{Module}}.{{Action}} (PascalCase)");
            }
        }

        bad.Should().BeEmpty(
            "Permission strings must match the documented {Module}.{Action} convention so the seeder, " +
            "policy provider, and frontend permission map all agree on the shape. " +
            "See Starter.Shared/Constants/Permissions.cs header.");
    }

    [Fact]
    public void Default_role_permissions_reference_real_permissions()
    {
        var modules = ModuleLoader.DiscoverModules();
        var orphans = new List<string>();

        foreach (var module in modules)
        {
            var declared = module.GetPermissions().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var (role, perms) in module.GetDefaultRolePermissions())
            {
                foreach (var perm in perms)
                {
                    if (!declared.Contains(perm))
                        orphans.Add($"{module.GetType().FullName}: role '{role}' references undeclared permission '{perm}'");
                }
            }
        }

        orphans.Should().BeEmpty(
            "GetDefaultRolePermissions() may only reference permissions returned by GetPermissions() " +
            "on the same module. Typos here are silently dropped by the seeder (DataSeeder line ~146). " +
            "Audit finding BE#7.");
    }
}
