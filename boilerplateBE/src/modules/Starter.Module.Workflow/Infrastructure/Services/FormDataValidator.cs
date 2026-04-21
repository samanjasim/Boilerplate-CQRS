using System.Text.Json;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Workflow.Infrastructure.Services;

public interface IFormDataValidator
{
    List<FormValidationError> Validate(
        List<FormFieldDefinition>? formFields,
        Dictionary<string, object>? formData);
}

public sealed record FormValidationError(string FieldName, string Message);

internal sealed class FormDataValidator : IFormDataValidator
{
    public List<FormValidationError> Validate(
        List<FormFieldDefinition>? formFields,
        Dictionary<string, object>? formData)
    {
        var errors = new List<FormValidationError>();
        if (formFields is null || formFields.Count == 0) return errors;

        formData ??= new();

        foreach (var field in formFields)
        {
            var hasValue = formData.TryGetValue(field.Name, out var rawValue)
                && rawValue is not null
                && !IsEmptyValue(rawValue);

            // Required check — for checkbox, presence of a false bool is NOT sufficient
            if (field.Required && field.Type.Equals("checkbox", StringComparison.OrdinalIgnoreCase))
            {
                var isTruthy = hasValue && TryGetBool(rawValue!, out var boolVal) && boolVal;
                if (!isTruthy)
                {
                    errors.Add(new(field.Name, $"'{field.Label}' is required."));
                    continue;
                }
                // value is truthy — no further validation needed for checkbox
                continue;
            }

            if (field.Required && !hasValue)
            {
                errors.Add(new(field.Name, $"'{field.Label}' is required."));
                continue;
            }

            if (!hasValue) continue;

            // Type-specific validation
            switch (field.Type.ToLowerInvariant())
            {
                case "number":
                    if (!TryGetDouble(rawValue!, out var numVal))
                    {
                        errors.Add(new(field.Name, $"'{field.Label}' must be a number."));
                        break;
                    }
                    if (field.Min.HasValue && numVal < field.Min.Value)
                        errors.Add(new(field.Name, $"'{field.Label}' must be at least {field.Min.Value}."));
                    if (field.Max.HasValue && numVal > field.Max.Value)
                        errors.Add(new(field.Name, $"'{field.Label}' must be at most {field.Max.Value}."));
                    break;

                case "text":
                case "textarea":
                    var strVal = ToString(rawValue!);
                    if (field.MaxLength.HasValue && strVal.Length > field.MaxLength.Value)
                        errors.Add(new(field.Name, $"'{field.Label}' must be at most {field.MaxLength.Value} characters."));
                    break;

                case "select":
                    var selectVal = ToString(rawValue!);
                    if (field.Options is not null && field.Options.All(o => o.Value != selectVal))
                        errors.Add(new(field.Name, $"'{field.Label}' has an invalid selection."));
                    break;

                case "date":
                    var dateStr = ToString(rawValue!);
                    if (!DateTime.TryParse(dateStr, out _))
                        errors.Add(new(field.Name, $"'{field.Label}' must be a valid date."));
                    break;
            }
        }

        return errors;
    }

    private static bool IsEmptyValue(object value)
    {
        return value switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Null => true,
            JsonElement je when je.ValueKind == JsonValueKind.String => string.IsNullOrEmpty(je.GetString()),
            string s => string.IsNullOrEmpty(s),
            _ => false,
        };
    }

    private static string ToString(object value)
    {
        return value switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
            JsonElement je => je.ToString(),
            null => string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static bool TryGetDouble(object value, out double result)
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
                var str = ToString(value);
                if (double.TryParse(str, out var parsed))
                {
                    result = parsed;
                    return true;
                }
                result = double.NaN;
                return false;
        }
    }

    private static bool TryGetBool(object value, out bool result)
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
                var str = ToString(value);
                if (bool.TryParse(str, out var parsed))
                {
                    result = parsed;
                    return true;
                }
                result = false;
                return false;
        }
    }
}
