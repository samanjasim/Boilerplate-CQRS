using Starter.Shared.Results;

namespace Starter.Application.Common.Interfaces;

public interface ITokenService
{
    Task<Result<TokenResponse>> GenerateTokensAsync(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        Guid? tenantId);
}

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
