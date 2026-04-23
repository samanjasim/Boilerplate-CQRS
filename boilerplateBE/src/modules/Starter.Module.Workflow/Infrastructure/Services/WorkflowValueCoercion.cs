using System.Text.Json;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Coerces boxed values that arrive as <see cref="JsonElement"/> (from
/// deserialized ContextJson / form payloads) or primitive CLR types into
/// uniform strings, doubles, or bools. Shared by <see cref="FormDataValidator"/>
/// (required/type/range checks) and <see cref="ConditionEvaluator"/>
/// (runtime transition branching) so both layers understand the same input shapes.
/// </summary>
internal static class WorkflowValueCoercion
{
    public static string ToStringValue(object? value) => value switch
    {
        null => string.Empty,
        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
        JsonElement je => je.ToString(),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// Returns <see cref="double.NaN"/> on failure — convenient for arithmetic
    /// comparisons where NaN propagates and forces `>` / `<` to return false.
    /// </summary>
    public static double ToDouble(object? value) =>
        TryGetDouble(value, out var result) ? result : double.NaN;

    public static bool TryGetDouble(object? value, out double result)
    {
        switch (value)
        {
            case JsonElement je when je.ValueKind == JsonValueKind.Number:
                result = je.GetDouble();
                return true;
            case JsonElement je when je.ValueKind == JsonValueKind.String
                && double.TryParse(je.GetString(), out var d):
                result = d;
                return true;
            case double dbl:
                result = dbl;
                return true;
            case float f:
                result = f;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case decimal dec:
                result = (double)dec;
                return true;
            default:
                if (double.TryParse(ToStringValue(value), out var parsed))
                {
                    result = parsed;
                    return true;
                }
                result = double.NaN;
                return false;
        }
    }

    public static bool TryGetBool(object? value, out bool result)
    {
        switch (value)
        {
            case JsonElement je when je.ValueKind == JsonValueKind.True:
                result = true;
                return true;
            case JsonElement je when je.ValueKind == JsonValueKind.False:
                result = false;
                return true;
            case bool b:
                result = b;
                return true;
            default:
                if (bool.TryParse(ToStringValue(value), out var parsed))
                {
                    result = parsed;
                    return true;
                }
                result = false;
                return false;
        }
    }

    public static bool IsEmpty(object? value) => value switch
    {
        null => true,
        JsonElement je when je.ValueKind == JsonValueKind.Null => true,
        JsonElement je when je.ValueKind == JsonValueKind.String => string.IsNullOrEmpty(je.GetString()),
        string s => string.IsNullOrEmpty(s),
        _ => false,
    };
}
