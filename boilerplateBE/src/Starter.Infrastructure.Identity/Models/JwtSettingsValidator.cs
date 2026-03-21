using Microsoft.Extensions.Options;

namespace Starter.Infrastructure.Identity.Models;

/// <summary>
/// Validates JwtSettings configuration on startup.
/// </summary>
public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            failures.Add("JwtSettings:Secret must not be empty.");
        }
        else if (options.Secret.Length < 32)
        {
            failures.Add("JwtSettings:Secret must be at least 32 characters long.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("JwtSettings:Issuer must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("JwtSettings:Audience must not be empty.");
        }

        if (options.AccessTokenExpirationMinutes <= 0)
        {
            failures.Add("JwtSettings:AccessTokenExpirationMinutes must be greater than zero.");
        }

        if (options.RefreshTokenExpirationDays <= 0)
        {
            failures.Add("JwtSettings:RefreshTokenExpirationDays must be greater than zero.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
