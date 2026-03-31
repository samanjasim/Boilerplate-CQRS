using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starter.Application.Features.Billing.DTOs;

public sealed record PlanFeatureEntry(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("translations")] Dictionary<string, PlanFeatureTranslation>? Translations = null)
{
    internal static List<PlanFeatureEntry>? ParseFeatures(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<PlanFeatureEntry>>(json);
        }
        catch
        {
            // Backward compat: old format was Dictionary<string, string> or Dictionary<string, JsonElement>
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                return dict?.Select(kv => new PlanFeatureEntry(kv.Key, kv.Value.ToString(), null)).ToList();
            }
            catch { return null; }
        }
    }
}

public sealed record PlanFeatureTranslation(
    [property: JsonPropertyName("label")] string Label);
