namespace Starter.Application.Common.Interfaces;

public interface ITotpService
{
    string GenerateSecret();
    string GetQrCodeUri(string email, string secret, string issuer);
    bool ValidateCode(string secret, string code);
    List<string> GenerateBackupCodes(int count = 8);
    string HashBackupCode(string code);
}
