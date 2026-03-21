namespace Starter.Application.Common.Interfaces;

public interface IOtpService
{
    Task<string> GenerateAsync(string purpose, string identifier, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string purpose, string identifier, string code, CancellationToken cancellationToken = default);
}
