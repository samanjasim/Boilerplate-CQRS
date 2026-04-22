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
                && !WorkflowValueCoercion.IsEmpty(rawValue);

            // Required check — for checkbox, presence of a false bool is NOT sufficient
            if (field.Required && field.Type.Equals("checkbox", StringComparison.OrdinalIgnoreCase))
            {
                var isTruthy = hasValue
                    && WorkflowValueCoercion.TryGetBool(rawValue!, out var boolVal)
                    && boolVal;
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
                    if (!WorkflowValueCoercion.TryGetDouble(rawValue!, out var numVal))
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
                    var strVal = WorkflowValueCoercion.ToStringValue(rawValue!);
                    if (field.MaxLength.HasValue && strVal.Length > field.MaxLength.Value)
                        errors.Add(new(field.Name, $"'{field.Label}' must be at most {field.MaxLength.Value} characters."));
                    break;

                case "select":
                    var selectVal = WorkflowValueCoercion.ToStringValue(rawValue!);
                    if (field.Options is not null && field.Options.All(o => o.Value != selectVal))
                        errors.Add(new(field.Name, $"'{field.Label}' has an invalid selection."));
                    break;

                case "date":
                    var dateStr = WorkflowValueCoercion.ToStringValue(rawValue!);
                    if (!DateTime.TryParse(dateStr, out _))
                        errors.Add(new(field.Name, $"'{field.Label}' must be a valid date."));
                    break;
            }
        }

        return errors;
    }
}
