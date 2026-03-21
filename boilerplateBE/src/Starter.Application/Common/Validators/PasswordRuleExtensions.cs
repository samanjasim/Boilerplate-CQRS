using FluentValidation;

namespace Starter.Application.Common.Validators;

public static class PasswordRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(
        this IRuleBuilder<T, string> rule, string fieldName = "Password")
    {
        return rule
            .NotEmpty().WithMessage($"{fieldName} is required.")
            .MinimumLength(8).WithMessage($"{fieldName} must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage($"{fieldName} must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage($"{fieldName} must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage($"{fieldName} must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage($"{fieldName} must contain at least one special character.");
    }
}
