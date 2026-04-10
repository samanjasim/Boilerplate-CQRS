using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.Tenants.Enums;

namespace Starter.Infrastructure.Identity.Authentication;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApplicationDbContext dbContext)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string TenantHeaderName = "X-Tenant-Id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var apiKeyValue))
            return AuthenticateResult.NoResult();

        var providedKey = apiKeyValue.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
            return AuthenticateResult.NoResult();

        if (providedKey.Length < 16)
            return AuthenticateResult.Fail("Invalid API key format.");

        var prefix = providedKey[..16];

        var apiKey = await dbContext.Set<ApiKey>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix);

        if (apiKey is null)
            return AuthenticateResult.Fail("Invalid API key.");

        if (!apiKey.IsValid)
            return AuthenticateResult.Fail("API key is revoked or expired.");

        if (!BCrypt.Net.BCrypt.Verify(providedKey, apiKey.KeyHash))
            return AuthenticateResult.Fail("Invalid API key.");

        // Update last used (fire-and-forget)
        apiKey.UpdateLastUsed();
        try { await dbContext.SaveChangesAsync(); }
        catch { /* non-critical */ }

        var claims = new List<Claim>
        {
            new("api_key_id", apiKey.Id.ToString()),
            new("auth_method", "api_key"),
            new("is_platform_key", apiKey.IsPlatformKey.ToString().ToLowerInvariant())
        };

        if (apiKey.TenantId.HasValue)
        {
            // Tenant key: locked to its tenant, ignore X-Tenant-Id header
            claims.Add(new Claim("tenant_id", apiKey.TenantId.Value.ToString()));
        }
        else
        {
            // Platform key: check for X-Tenant-Id header
            if (Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdHeader))
            {
                var tenantIdStr = tenantIdHeader.ToString();
                if (Guid.TryParse(tenantIdStr, out var requestedTenantId))
                {
                    var tenant = await dbContext.Tenants
                        .IgnoreQueryFilters()
                        .Where(t => t.Id == requestedTenantId)
                        .Select(t => new { t.Id, t.Status })
                        .FirstOrDefaultAsync();

                    if (tenant is null)
                        return AuthenticateResult.Fail("Invalid tenant ID.");

                    if (tenant.Status != TenantStatus.Active)
                        return AuthenticateResult.Fail("Tenant is not active.");

                    claims.Add(new Claim("tenant_id", requestedTenantId.ToString()));
                }
                else
                {
                    return AuthenticateResult.Fail("Invalid X-Tenant-Id header format.");
                }
            }
            // No header = platform-wide access (no tenant_id claim)
        }

        foreach (var scope in apiKey.Scopes)
            claims.Add(new Claim("permission", scope));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
