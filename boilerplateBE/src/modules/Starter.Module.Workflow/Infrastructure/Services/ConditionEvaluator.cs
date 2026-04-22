using System.Text.Json;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Workflow.Infrastructure.Services;

public interface IConditionEvaluator
{
    bool Evaluate(ConditionConfig condition, Dictionary<string, object>? context);
}

internal sealed class ConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionConfig condition, Dictionary<string, object>? context)
    {
        // Compound expression (AND/OR/NOT)
        if (condition.Logic is not null)
        {
            var op = condition.Logic.ToUpperInvariant();

            // NOT needs explicit handling for the "empty conditions" case — define it
            // as false (same semantics as an empty AND group) to avoid a misconfigured
            // workflow accidentally granting permit-all access.
            if (op == "NOT")
            {
                if (condition.Conditions is null or { Count: 0 }) return false;
                // Multi-child NOT treats its children as implicit AND, then inverts.
                return !condition.Conditions.All(c => Evaluate(c, context));
            }

            if (condition.Conditions is null or { Count: 0 }) return false;

            return op switch
            {
                "OR" => condition.Conditions.Any(c => Evaluate(c, context)),
                _ => condition.Conditions.All(c => Evaluate(c, context)), // default AND
            };
        }

        if (condition.Field is null) return false;
        if (context is null || !context.TryGetValue(condition.Field, out var rawValue))
            return false;

        if (condition.Operator is null) return false;

        var condValue = condition.Value ?? (object)string.Empty;

        return condition.Operator.ToLowerInvariant() switch
        {
            "equals" => CompareEquals(rawValue, condValue),
            "notequals" => !CompareEquals(rawValue, condValue),
            "greaterthan" => CompareNumeric(rawValue, condValue) > 0,
            "lessthan" => CompareNumeric(rawValue, condValue) < 0,
            "greaterthanorequal" => CompareNumeric(rawValue, condValue) >= 0,
            "lessthanorequal" => CompareNumeric(rawValue, condValue) <= 0,
            "contains" => ToStr(rawValue).Contains(ToStr(condValue), StringComparison.OrdinalIgnoreCase),
            "in" => EvaluateIn(rawValue, condValue),
            _ => false,
        };
    }

    private static bool CompareEquals(object contextValue, object conditionValue)
    {
        var left = ToStr(contextValue);
        var right = ToStr(conditionValue);
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static double CompareNumeric(object contextValue, object conditionValue)
    {
        var left = ToDouble(contextValue);
        var right = ToDouble(conditionValue);

        if (double.IsNaN(left) || double.IsNaN(right))
            return double.NaN;

        return left - right;
    }

    private static bool EvaluateIn(object contextValue, object conditionValue)
    {
        var target = ToStr(contextValue);

        // conditionValue may be a JsonElement array or a C# IEnumerable<string>
        if (conditionValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in jsonElement.EnumerateArray())
            {
                if (string.Equals(target, item.GetString(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        if (conditionValue is IEnumerable<string> strings)
            return strings.Any(s => string.Equals(target, s, StringComparison.OrdinalIgnoreCase));

        if (conditionValue is IEnumerable<object> objects)
            return objects.Any(o => string.Equals(target, ToStr(o), StringComparison.OrdinalIgnoreCase));

        return false;
    }

    private static string ToStr(object value)
    {
        return value switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
            JsonElement je => je.ToString(),
            null => string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static double ToDouble(object value)
    {
        return value switch
        {
            JsonElement je when je.ValueKind is JsonValueKind.Number => je.GetDouble(),
            JsonElement je when je.ValueKind == JsonValueKind.String
                && double.TryParse(je.GetString(), out var d) => d,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            _ when double.TryParse(ToStr(value), out var parsed) => parsed,
            _ => double.NaN,
        };
    }
}
