using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Starter.Module.Communication.Infrastructure.Services;

public interface ICredentialEncryptionService
{
    string Encrypt(Dictionary<string, string> credentials);
    Dictionary<string, string> Decrypt(string encryptedJson);
    Dictionary<string, string> Mask(Dictionary<string, string> credentials);
}

internal sealed class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly IDataProtector _protector;

    public CredentialEncryptionService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Communication.Credentials.v1");
    }

    public string Encrypt(Dictionary<string, string> credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        return _protector.Protect(json);
    }

    public Dictionary<string, string> Decrypt(string encryptedJson)
    {
        var json = _protector.Unprotect(encryptedJson);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }

    public Dictionary<string, string> Mask(Dictionary<string, string> credentials)
    {
        var masked = new Dictionary<string, string>();
        foreach (var (key, value) in credentials)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 4)
            {
                masked[key] = "****";
            }
            else
            {
                masked[key] = string.Concat("****", value.AsSpan(value.Length - 4));
            }
        }
        return masked;
    }
}
