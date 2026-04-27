using System.Text.Json;
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;

public sealed class UpsertSafetyPresetProfileCommandValidator : AbstractValidator<UpsertSafetyPresetProfileCommand>
{
    public UpsertSafetyPresetProfileCommandValidator()
    {
        RuleFor(x => x.CategoryThresholdsJson)
            .NotEmpty()
            .Must(BeValidThresholdsJson)
            .WithMessage("CategoryThresholdsJson must be a JSON object with all values in [0.0, 1.0].");

        RuleFor(x => x.BlockedCategoriesJson)
            .NotEmpty()
            .Must(BeValidStringArrayJson)
            .WithMessage("BlockedCategoriesJson must be a JSON array of strings.");
    }

    private static bool BeValidThresholdsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
            return dict is not null && dict.Values.All(v => v >= 0.0 && v <= 1.0);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool BeValidStringArrayJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
