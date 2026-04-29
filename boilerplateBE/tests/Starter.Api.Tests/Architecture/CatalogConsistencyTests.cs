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

    [Fact]
    public void Config_keys_are_present_and_unique()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var configKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            if (!module.Value.TryGetProperty("configKey", out var configKeyProp) ||
                configKeyProp.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(configKeyProp.GetString()))
            {
                problems.Add($"'{module.Name}' is missing a non-empty configKey");
                continue;
            }

            var configKey = configKeyProp.GetString()!;
            if (configKeys.TryGetValue(configKey, out var owner))
            {
                problems.Add($"'{module.Name}' and '{owner}' share configKey '{configKey}'");
            }
            else
            {
                configKeys[configKey] = module.Name;
            }
        }

        problems.Should().BeEmpty(
            "rename.ps1 emits activeModules keys from configKey, so keys must be stable and unique.");
    }

    [Fact]
    public void Declared_backend_module_projects_exist()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var repoRoot = GetRepoRoot();
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            var backendModule = ReadOptionalString(module.Value, "backendModule");
            if (backendModule is null) continue;

            var expectedPath = Path.Combine(repoRoot, "boilerplateBE", "src", "modules", backendModule);
            if (!Directory.Exists(expectedPath))
            {
                problems.Add($"'{module.Name}.backendModule' points to missing project folder '{expectedPath}'");
            }
        }

        problems.Should().BeEmpty(
            "catalog backendModule values are used by generated-app composition and must resolve in the template.");
    }

    [Fact]
    public void Declared_frontend_features_have_module_entrypoints()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var repoRoot = GetRepoRoot();
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            var feature = ReadOptionalString(module.Value, "frontendFeature");
            if (feature is null) continue;

            var featurePath = Path.Combine(repoRoot, "boilerplateFE", "src", "features", feature);
            var indexTs = Path.Combine(featurePath, "index.ts");
            var indexTsx = Path.Combine(featurePath, "index.tsx");

            if (!Directory.Exists(featurePath))
            {
                problems.Add($"'{module.Name}.frontendFeature' points to missing folder '{featurePath}'");
                continue;
            }

            if (!File.Exists(indexTs) && !File.Exists(indexTsx))
            {
                problems.Add($"'{module.Name}.frontendFeature' must expose index.ts or index.tsx in '{featurePath}'");
            }
        }

        problems.Should().BeEmpty(
            "generated modules.config.ts imports selected web modules from their feature entrypoints.");
    }

    [Fact]
    public void Declared_mobile_modules_have_matching_folder_and_entrypoint()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var repoRoot = GetRepoRoot();
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            var mobileModule = ReadOptionalString(module.Value, "mobileModule");
            var mobileFolder = ReadOptionalString(module.Value, "mobileFolder");

            if (mobileModule is null && mobileFolder is null) continue;
            if (mobileModule is null || mobileFolder is null)
            {
                problems.Add($"'{module.Name}' must define both mobileModule and mobileFolder, or neither");
                continue;
            }

            var moduleFile = ToSnakeCase(mobileModule) + ".dart";
            var expectedPath = Path.Combine(repoRoot, "boilerplateMobile", "lib", "modules", mobileFolder, moduleFile);

            if (!File.Exists(expectedPath))
            {
                problems.Add($"'{module.Name}' mobile entrypoint missing at '{expectedPath}'");
            }
        }

        problems.Should().BeEmpty(
            "generated modules.config.dart imports selected mobile modules from catalog metadata.");
    }

    private static bool IsLowerCamelCase(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!char.IsLower(id[0])) return false;
        return id.All(c => char.IsLetterOrDigit(c));
    }

    private static IEnumerable<JsonProperty> ModuleEntries(JsonDocument doc) =>
        doc.RootElement.EnumerateObject().Where(p => !p.Name.StartsWith("_"));

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return null;
        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (property.ValueKind != JsonValueKind.String) return null;

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ToSnakeCase(string value)
    {
        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray());
    }

    private static string GetRepoRoot()
    {
        var catalog = new FileInfo(CatalogPath);
        return catalog.Directory?.FullName
            ?? throw new DirectoryNotFoundException("Unable to resolve repository root from " + CatalogPath);
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
