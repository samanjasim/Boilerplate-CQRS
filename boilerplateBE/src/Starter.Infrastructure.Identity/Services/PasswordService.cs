using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Identity.Services;

/// <summary>
/// BCrypt password hashing and verification service.
/// </summary>
public class PasswordService : IPasswordService
{
    private const int WorkFactor = 12;

    public async Task<string> HashPasswordAsync(string password)
    {
        return await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor));
    }

    public async Task<bool> VerifyPasswordAsync(string password, string passwordHash)
    {
        return await Task.Run(() => BCrypt.Net.BCrypt.Verify(password, passwordHash));
    }
}
