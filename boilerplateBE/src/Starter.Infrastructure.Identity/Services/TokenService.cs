using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Identity.Models;
using Starter.Shared.Results;

namespace Starter.Infrastructure.Identity.Services;

/// <summary>
/// JWT token service implementation. Handles only token operations.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public Task<Result<TokenResponse>> GenerateTokensAsync(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        Guid? tenantId)
    {
        var accessToken = GenerateAccessToken(userId, email, roles, permissions, tenantId);
        var refreshToken = GenerateRefreshToken();

        var accessTokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        var response = new TokenResponse(
            accessToken,
            refreshToken,
            accessTokenExpiration,
            refreshTokenExpiration);

        return Task.FromResult(Result.Success(response));
    }

    private string GenerateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        Guid? tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
