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

    [Theory]
    [InlineData("backendModule")]
    [InlineData("frontendFeature")]
    [InlineData("mobileModule")]
    [InlineData("mobileFolder")]
    public void Path_bearing_fields_are_unique_across_modules(string field)
    {
        // Two catalog entries pointing at the same project folder, feature folder,
        // or mobile module class would silently make Write-WebModulesConfig emit
        // duplicate `import` lines (broken TS) and Write-MobileModulesConfig emit
        // duplicate Dart imports. Catch the drift in the catalog itself.
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            var value = ReadOptionalString(module.Value, field);
            if (value is null) continue;

            if (owners.TryGetValue(value, out var owner))
            {
                problems.Add($"'{module.Name}' and '{owner}' both declare {field} '{value}'");
            }
            else
            {
                owners[value] = module.Name;
            }
        }

        problems.Should().BeEmpty(
            $"catalog {field} values map 1:1 to template artifacts; two modules sharing one " +
            "would generate duplicate imports.");
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

    [Fact]
    public void Module_test_folders_are_declared_in_catalog()
    {
        // Every module-scoped folder under tests/Starter.Api.Tests/ must be
        // declared by some module's `testsFolder` field. Otherwise rename.ps1
        // doesn't know to delete the orphan when that module is excluded,
        // and the generated -Modules None / -Modules <subset> app fails to
        // compile because the orphan tests reference the now-deleted module
        // namespaces. Caught by the CI killer-test matrix; this test surfaces
        // it during normal source-repo CI so we never have to debug it as a
        // post-merge regression again.
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));

        // Folders that are core (always shipped) — they don't need a testsFolder
        // in the catalog because they're never stripped.
        var coreFolders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Access",
            "Architecture",
            "Capabilities",
            "Files",
            "MassTransit",
            "bin",
            "obj",
        };

        var declaredTestFolders = ModuleEntries(doc)
            .Select(m => ReadOptionalString(m.Value, "testsFolder"))
            .Where(v => v is not null)
            .Select(v => v!)
            .ToHashSet(StringComparer.Ordinal);

        var repoRoot = GetRepoRoot();
        var testsRoot = Path.Combine(repoRoot, "boilerplateBE", "tests", "Starter.Api.Tests");
        if (!Directory.Exists(testsRoot)) return; // nothing to validate

        var problems = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(testsRoot))
        {
            var name = Path.GetFileName(dir);
            if (coreFolders.Contains(name)) continue;
            if (declaredTestFolders.Contains(name)) continue;
            problems.Add(
                $"tests/Starter.Api.Tests/{name}/ exists but no module declares testsFolder='{name}' in modules.catalog.json. " +
                "rename.ps1 will leave this folder orphaned when its module is excluded, breaking the generated app's build.");
        }

        problems.Should().BeEmpty();
    }

    [Fact]
    public void Every_module_declares_a_valid_semver_version()
    {
        // Tier 2.5 schema v2 (spec 2026-04-29 Theme 1): every module declares a semver version
        // mirroring IModule.Version. Tier 3 will use this for package compatibility checks.
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            var version = ReadOptionalString(module.Value, "version");
            if (version is null)
            {
                problems.Add($"'{module.Name}' is missing required 'version' (Tier 2.5 schema v2)");
                continue;
            }

            if (!IsSimpleSemver(version))
                problems.Add($"'{module.Name}.version' = '{version}' is not MAJOR.MINOR.PATCH semver");
        }

        problems.Should().BeEmpty(
            "modules.catalog.json schema v2 requires every module to declare a semver version. " +
            "See spec 2026-04-29-modularity-tier-2-5-hardening.md §2 Theme 1.");
    }

    [Fact]
    public void supportedPlatforms_matches_declared_path_fields()
    {
        // Tier 2.5 schema v2: supportedPlatforms is the explicit declaration. Drift between it
        // and the path-bearing fields (backendModule/frontendFeature/mobileModule) means generated
        // apps either skip a module the catalog claims to ship, or import a module that doesn't
        // exist on the target platform.
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "backend", "web", "mobile" };
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            var platforms = ReadStringArray(module.Value, "supportedPlatforms");
            if (platforms is null)
            {
                problems.Add($"'{module.Name}' is missing required 'supportedPlatforms' (Tier 2.5 schema v2)");
                continue;
            }

            if (platforms.Count == 0)
            {
                problems.Add($"'{module.Name}.supportedPlatforms' must contain at least one platform");
                continue;
            }

            foreach (var p in platforms)
            {
                if (!allowed.Contains(p))
                    problems.Add($"'{module.Name}.supportedPlatforms' contains unknown platform '{p}'");
            }

            var hasBackend = ReadOptionalString(module.Value, "backendModule") is not null;
            var hasWeb = ReadOptionalString(module.Value, "frontendFeature") is not null;
            var hasMobile = ReadOptionalString(module.Value, "mobileModule") is not null;

            if (hasBackend && !platforms.Contains("backend"))
                problems.Add($"'{module.Name}' declares backendModule but 'backend' is not in supportedPlatforms");
            if (hasWeb && !platforms.Contains("web"))
                problems.Add($"'{module.Name}' declares frontendFeature but 'web' is not in supportedPlatforms");
            if (hasMobile && !platforms.Contains("mobile"))
                problems.Add($"'{module.Name}' declares mobileModule but 'mobile' is not in supportedPlatforms");

            if (platforms.Contains("backend") && !hasBackend)
                problems.Add($"'{module.Name}' lists 'backend' in supportedPlatforms but has no backendModule");
            if (platforms.Contains("web") && !hasWeb)
                problems.Add($"'{module.Name}' lists 'web' in supportedPlatforms but has no frontendFeature");
            if (platforms.Contains("mobile") && !hasMobile)
                problems.Add($"'{module.Name}' lists 'mobile' in supportedPlatforms but has no mobileModule");
        }

        problems.Should().BeEmpty(
            "supportedPlatforms must reflect what the module actually ships, in both directions. " +
            "Drift here causes generated apps to import modules that don't exist on the target platform.");
    }

    [Fact]
    public void Catalog_dependencies_are_platform_compatible()
    {
        // A module that supports a platform cannot depend on a module that does not.
        // Otherwise generation succeeds but the platform build fails on missing imports.
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var modulePlatforms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var module in ModuleEntries(doc))
        {
            modulePlatforms[module.Name] = ReadStringArray(module.Value, "supportedPlatforms") ?? Array.Empty<string>();
        }

        var problems = new List<string>();
        foreach (var module in ModuleEntries(doc))
        {
            if (!module.Value.TryGetProperty("dependencies", out var deps) || deps.ValueKind != JsonValueKind.Array)
                continue;

            var consumerPlatforms = modulePlatforms[module.Name];
            foreach (var dep in deps.EnumerateArray())
            {
                var depId = dep.GetString();
                if (string.IsNullOrEmpty(depId)) continue;
                if (!modulePlatforms.TryGetValue(depId, out var providerPlatforms)) continue; // covered by Every_dependency_entry_resolves_to_a_known_module_id

                foreach (var platform in consumerPlatforms)
                {
                    if (!providerPlatforms.Contains(platform))
                    {
                        problems.Add(
                            $"'{module.Name}' supports '{platform}' and depends on '{depId}', " +
                            $"but '{depId}' does not support '{platform}' (supports: {string.Join(",", providerPlatforms)}).");
                    }
                }
            }
        }

        problems.Should().BeEmpty(
            "A module that supports a platform cannot depend on a module that does not — " +
            "the consumer's platform build would fail on missing imports.");
    }

    [Fact]
    public void coreCompat_when_present_is_a_non_empty_string()
    {
        // Tier 3 will add a real semver-range parser. Tier 2.5 only validates the field's shape
        // so authors don't introduce typos before the enforcer exists. Field is intentionally
        // unpopulated today; this test guards the day someone adds it.
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            if (!module.Value.TryGetProperty("coreCompat", out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Null) continue;

            if (prop.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.GetString()))
                problems.Add($"'{module.Name}.coreCompat' must be a non-empty string when present");
        }

        problems.Should().BeEmpty();
    }

    [Fact]
    public void packageId_keys_match_supportedPlatforms()
    {
        // Tier 3 generators emit packageId values per platform. The shape is validated now so
        // the field cannot rot in catalog before the generators land: only platforms the module
        // actually supports may declare a package id.
        using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var allowedKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "nuget", "backend" },
            { "npm", "web" },
            { "pub", "mobile" },
        };
        var problems = new List<string>();

        foreach (var module in ModuleEntries(doc))
        {
            if (!module.Value.TryGetProperty("packageId", out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Null) continue;
            if (prop.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"'{module.Name}.packageId' must be an object when present");
                continue;
            }

            var platforms = ReadStringArray(module.Value, "supportedPlatforms") ?? Array.Empty<string>();
            foreach (var key in prop.EnumerateObject())
            {
                if (!allowedKeys.TryGetValue(key.Name, out var requiredPlatform))
                {
                    problems.Add($"'{module.Name}.packageId' has unknown key '{key.Name}'; allowed: {string.Join(",", allowedKeys.Keys)}");
                    continue;
                }
                if (!platforms.Contains(requiredPlatform))
                {
                    problems.Add(
                        $"'{module.Name}.packageId.{key.Name}' is set but supportedPlatforms does not include '{requiredPlatform}'");
                }
                if (key.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.Value.GetString()))
                {
                    problems.Add($"'{module.Name}.packageId.{key.Name}' must be a non-empty string");
                }
            }
        }

        problems.Should().BeEmpty();
    }

    private static bool IsLowerCamelCase(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!char.IsLower(id[0])) return false;
        return id.All(c => char.IsLetterOrDigit(c));
    }

    private static bool IsSimpleSemver(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 3) return false;
        return parts.All(p => p.Length > 0 && p.All(char.IsDigit));
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.Array) return null;
        return prop.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
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
