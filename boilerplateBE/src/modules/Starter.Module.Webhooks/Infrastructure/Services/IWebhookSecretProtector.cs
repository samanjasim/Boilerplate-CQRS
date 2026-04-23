namespace Starter.Module.Webhooks.Infrastructure.Services;

/// <summary>
/// Wraps ASP.NET Data Protection so webhook HMAC secrets are encrypted at rest
/// and only decrypted at the point we sign an outbound delivery.
/// </summary>
public interface IWebhookSecretProtector
{
    /// <summary>Encrypts the plaintext secret for database storage.</summary>
    string Protect(string plaintextSecret);

    /// <summary>Decrypts a stored secret for signing. Idempotent for legacy
    /// plaintext values that predate protection.</summary>
    string Unprotect(string storedSecret);
}
