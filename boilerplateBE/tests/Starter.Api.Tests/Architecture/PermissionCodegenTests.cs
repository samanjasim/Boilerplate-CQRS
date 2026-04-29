using FluentAssertions;
using Starter.Abstractions.Modularity;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Architecture;

/// <summary>
/// Tier 2.5 Theme 4 — defense-in-depth around the cross-platform permission codegen.
/// The CI drift gate (npm run verify:permissions) catches generator output drift,
/// but it can't catch the case where the generator silently drops a permission
/// (e.g. a future regex change that misses a constant). These tests enumerate the
/// BE permission set via reflection and assert every value appears as a string
/// literal in both generated artifacts.
///
/// If a test here fails after a permission rename: run `npm run generate:permissions`
/// from the repo root and commit the regenerated files.
/// </summary>
public class PermissionCodegenTests
{
    private static readonly string GeneratedTsPath = ResolveRepoRelative(
        "boilerplateFE", "src", "constants", "permissions.generated.ts");
    private static readonly string GeneratedDartPath = ResolveRepoRelative(
        "boilerplateMobile", "lib", "core", "permissions", "permissions.generated.dart");

    [Fact]
    public void Every_backend_permission_appears_as_a_string_literal_in_the_generated_TS_file()
    {
        var generated = File.ReadAllText(GeneratedTsPath);
        var missing = AllBackendPermissions()
            .Where(p => !generated.Contains($"'{p}'"))
            .ToList();

        missing.Should().BeEmpty(
            "every BE permission constant must appear in permissions.generated.ts. " +
            "Run `npm run generate:permissions` from the repo root if this fails.");
    }

    [Fact]
    public void Every_backend_permission_appears_as_a_string_literal_in_the_generated_Dart_file()
    {
        var generated = File.ReadAllText(GeneratedDartPath);
        var missing = AllBackendPermissions()
            .Where(p => !generated.Contains($"'{p}'"))
            .ToList();

        missing.Should().BeEmpty(
            "every BE permission constant must appear in permissions.generated.dart. " +
            "Run `npm run generate:permissions` from the repo root if this fails.");
    }

    [Fact]
    public void Generated_files_carry_the_AUTO_GENERATED_header()
    {
        // If someone hand-edits a generated file and accidentally drops the marker,
        // future contributors might not realize regeneration overwrites their edit.
        File.ReadAllText(GeneratedTsPath).Should().Contain("AUTO-GENERATED");
        File.ReadAllText(GeneratedDartPath).Should().Contain("AUTO-GENERATED");
    }

    private static IEnumerable<string> AllBackendPermissions()
    {
        // Core permissions from Starter.Shared.Constants.Permissions.GetAllWithMetadata.
        foreach (var (name, _, _) in Permissions.GetAllWithMetadata())
            yield return name;

        // Module-provided permissions via IModule.GetPermissions.
        foreach (var module in ModuleLoader.DiscoverModules())
        {
            foreach (var (name, _, _) in module.GetPermissions())
                yield return name;
        }
    }

    private static string ResolveRepoRelative(params string[] segments)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "modules.catalog.json");
            if (File.Exists(candidate))
                return Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "Repo root (containing modules.catalog.json) not found walking up from " +
            AppDomain.CurrentDomain.BaseDirectory);
    }
}
