namespace Starter.Application.Common.Interfaces;

public interface IPasswordService
{
    Task<string> HashPasswordAsync(string password);

    Task<bool> VerifyPasswordAsync(string password, string passwordHash);
}
