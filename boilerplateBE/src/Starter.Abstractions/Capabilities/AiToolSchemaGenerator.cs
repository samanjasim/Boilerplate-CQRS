using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Pure helper: converts an <c>[AiTool]</c>-attributed type into a JSON Schema
/// <see cref="JsonElement"/>. Applies <see cref="AiParameterIgnoreAttribute"/> stripping,
/// trust-boundary validation, and <see cref="DescriptionAttribute"/>-based enrichment — or
/// uses <see cref="AiToolAttribute.ParameterSchemaJson"/> verbatim when set.
/// </summary>
public static class AiToolSchemaGenerator
{
    // Property names (camelCase, after JsonSerializerOptions name policy) that the LLM
    // must never be allowed to supply — they bind to server-trusted state resolved by
    // handlers from ICurrentUserService.
    private static readonly HashSet<string> TrustBoundaryPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenantId",
        "userId",
        "createdByUserId",
        "modifiedByUserId",
        "impersonatedBy",
        "isSystemAdmin",
    };

    private static readonly JsonSerializerOptions SchemaOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    // Treat the top-level type (and any unannotated reference type) as non-nullable so the
    // emitted schema uses `"type": "object"` rather than `"type": ["object", "null"]`.
    // Function-calling LLMs reject array-form type declarations at the root.
    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
    };

    public static JsonElement Generate(Type attributedType, AiToolAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attributedType);
        ArgumentNullException.ThrowIfNull(attribute);

        return attribute.ParameterSchemaJson is { } overrideJson
            ? ParseOverride(overrideJson, attributedType)
            : AutoDerive(attributedType);
    }

    private static JsonElement ParseOverride(string overrideJson, Type attributedType)
    {
        try
        {
            using var doc = JsonDocument.Parse(overrideJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"[AiTool] on '{attributedType.FullName}': ParameterSchemaJson override is not valid JSON. {ex.Message}",
                ex);
        }
    }

    private static JsonElement AutoDerive(Type attributedType)
    {
        JsonNode? node;
        try
        {
            node = JsonSchemaExporter.GetJsonSchemaAsNode(SchemaOptions, attributedType, ExporterOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[AiTool] on '{attributedType.FullName}': schema generation failed. {ex.Message}",
                ex);
        }

        if (node is not JsonObject obj)
            throw new InvalidOperationException(
                $"[AiTool] on '{attributedType.FullName}': expected a JSON object schema but got '{node?.GetValueKind()}'.");

        // JsonSchemaExporter emits draft-2020-12 shape: `type`, `properties`, `required`.
        StripIgnoredProperties(obj, attributedType);
        EnrichPropertyDescriptions(obj, attributedType);
        EnforceTrustBoundary(obj, attributedType);
        EnsureAdditionalPropertiesFalse(obj);

        return JsonSerializer.SerializeToElement(obj);
    }

    private static void StripIgnoredProperties(JsonObject root, Type type)
    {
        if (root["properties"] is not JsonObject propsObj) return;

        var ignoredCamel = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<AiParameterIgnoreAttribute>() is not null)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .ToHashSet(StringComparer.Ordinal);

        if (ignoredCamel.Count == 0) return;

        foreach (var name in ignoredCamel)
            propsObj.Remove(name);

        if (root["required"] is JsonArray requiredArr)
        {
            for (var i = requiredArr.Count - 1; i >= 0; i--)
            {
                if (requiredArr[i]?.GetValue<string>() is { } n && ignoredCamel.Contains(n))
                    requiredArr.RemoveAt(i);
            }
        }
    }

    private static void EnrichPropertyDescriptions(JsonObject root, Type type)
    {
        if (root["properties"] is not JsonObject propsObj) return;

        foreach (var clrProp in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var camel = JsonNamingPolicy.CamelCase.ConvertName(clrProp.Name);
            if (propsObj[camel] is not JsonObject propNode) continue;

            var desc = clrProp.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrWhiteSpace(desc))
                propNode["description"] = desc;
        }
    }

    private static void EnforceTrustBoundary(JsonObject root, Type type)
    {
        if (root["properties"] is not JsonObject propsObj) return;

        foreach (var propName in propsObj.Select(kv => kv.Key).ToList())
        {
            if (TrustBoundaryPropertyNames.Contains(propName))
                throw new InvalidOperationException(
                    $"[AiTool] on '{type.FullName}': property '{propName}' is a server-trusted field. " +
                    $"Mark it with [AiParameterIgnore] or remove it from the record.");
        }
    }

    private static void EnsureAdditionalPropertiesFalse(JsonObject root)
    {
        // Explicit — JsonSchemaExporter may omit this by default; the contract requires it.
        root["additionalProperties"] = false;
    }
}
