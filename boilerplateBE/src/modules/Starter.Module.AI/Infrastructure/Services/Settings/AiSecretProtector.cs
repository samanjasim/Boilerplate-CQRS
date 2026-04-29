using Microsoft.AspNetCore.DataProtection;
using Starter.Module.AI.Application.Services.Settings;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiSecretProtector : IAiSecretProtector
{
    private const string Purpose = "Starter.Module.AI.ProviderCredentials.v1";
    private const string VersionPrefix = "ai1:";

    private readonly IDataProtector _protector;

    public AiSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintextSecret) =>
        VersionPrefix + _protector.Protect(plaintextSecret);

    public string Unprotect(string storedSecret)
    {
        if (!storedSecret.StartsWith(VersionPrefix, StringComparison.Ordinal))
            return _protector.Unprotect(storedSecret);

        return _protector.Unprotect(storedSecret[VersionPrefix.Length..]);
    }

    public string Prefix(string secret)
    {
        var trimmed = secret.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12];
    }

    public string Mask(string keyPrefix)
    {
        if (string.IsNullOrWhiteSpace(keyPrefix))
            return "****";

        return $"{keyPrefix[..Math.Min(4, keyPrefix.Length)]}****";
    }
}
