using System.Security.Cryptography;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets;

internal static class AiWidgetCredentialFactory
{
    public static GeneratedWidgetCredential Generate()
    {
        var randomPart = "";
        while (randomPart.Length < 32)
        {
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            randomPart += Convert.ToBase64String(randomBytes)
                .Replace("+", "", StringComparison.Ordinal)
                .Replace("/", "", StringComparison.Ordinal)
                .Replace("=", "", StringComparison.Ordinal);
        }

        randomPart = randomPart[..32];
        var fullKey = $"pk_ai_{randomPart}";
        var keyPrefix = $"pk_ai_{randomPart[..8]}";
        var keyHash = BCrypt.Net.BCrypt.HashPassword(fullKey);

        return new GeneratedWidgetCredential(fullKey, keyPrefix, keyHash);
    }
}

internal sealed record GeneratedWidgetCredential(string FullKey, string KeyPrefix, string KeyHash);
