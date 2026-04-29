namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiSecretProtector
{
    string Protect(string plaintextSecret);
    string Unprotect(string storedSecret);
    string Prefix(string secret);
    string Mask(string keyPrefix);
}
