using Microsoft.AspNetCore.DataProtection;

namespace Starter.Module.Webhooks.Infrastructure.Services;

internal sealed class WebhookSecretProtector : IWebhookSecretProtector
{
    private const string Purpose = "Starter.Module.Webhooks.Secret.v1";
    private const string Prefix = "dp1:";

    private readonly IDataProtector _protector;

    public WebhookSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintextSecret) =>
        Prefix + _protector.Protect(plaintextSecret);

    public string Unprotect(string storedSecret)
    {
        // Legacy / unprotected secrets (anything written before this change)
        // remain usable until the next RegenerateSecret call.
        if (!storedSecret.StartsWith(Prefix, StringComparison.Ordinal))
            return storedSecret;

        return _protector.Unprotect(storedSecret[Prefix.Length..]);
    }
}
