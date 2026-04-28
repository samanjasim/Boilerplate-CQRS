using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class CatalogConsistencyTests
{
    private static readonly string CatalogPath = FindCatalogPath();

    [Fact]
    public void Every_dependency_entry_resolves_to_a_known_module_id()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var root = doc.RootElement;

        // Top-level keys (excluding _comment metadata) are the canonical module ids.
        var moduleIds = root.EnumerateObject()
            .Where(p => !p.Name.StartsWith("_"))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var problems = new List<string>();
        foreach (var module in root.EnumerateObject().Where(p => !p.Name.StartsWith("_")))
        {
            if (!module.Value.TryGetProperty("dependencies", out var deps)) continue;
            if (deps.ValueKind != JsonValueKind.Array) continue;

            foreach (var dep in deps.EnumerateArray())
            {
                var depId = dep.GetString();
                if (string.IsNullOrEmpty(depId)) continue;
                if (!moduleIds.Contains(depId))
                {
                    problems.Add($"'{module.Name}.dependencies' references unknown module id '{depId}'");
                }
            }
        }

        problems.Should().BeEmpty(
            "modules.catalog.json dependencies must reference catalog top-level ids. " +
            "rename.ps1 strict-mode reads this field to surface missing optional modules " +
            "at generation time. Drift inside the catalog (a dep typo, or a removed module " +
            "still referenced) silently produces the wrong selection. " +
            "See spec §14 D6 for the catalog-vs-runtime naming policy.");
    }

    [Fact]
    public void Module_ids_are_lower_camel_case()
    {
        // Spec §5: "id is stable and lower camel case." Enforce so the catalog
        // stays consistent as new modules are added (avoids "AI" vs "ai" drift).
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var problems = doc.RootElement.EnumerateObject()
            .Where(p => !p.Name.StartsWith("_"))
            .Where(p => !IsLowerCamelCase(p.Name))
            .Select(p => p.Name)
            .ToList();

        problems.Should().BeEmpty(
            "Module ids must be lowerCamelCase (spec §5). Offending ids: " +
            string.Join(", ", problems));
    }

    private static bool IsLowerCamelCase(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!char.IsLower(id[0])) return false;
        return id.All(c => char.IsLetterOrDigit(c));
    }

    private static string FindCatalogPath()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "modules.catalog.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "modules.catalog.json not found walking up from " + AppDomain.CurrentDomain.BaseDirectory);
    }
}
