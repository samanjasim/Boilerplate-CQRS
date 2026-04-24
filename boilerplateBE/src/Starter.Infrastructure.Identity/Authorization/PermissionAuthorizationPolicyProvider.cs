using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Starter.Infrastructure.Identity.Authentication;

namespace Starter.Infrastructure.Identity.Authorization;

/// <summary>
/// Custom policy provider for permission-based authorization.
/// Creates policies dynamically based on permission names and caches them;
/// the default provider caches already, but this implementation previously
/// rebuilt the policy graph on every request for any dotted permission name.
/// </summary>
public class PermissionAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policyCache = new(StringComparer.Ordinal);

    public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Check if it's a permission policy (policy name contains a dot, like "Users.View")
        if (policyName.Contains('.'))
        {
            return _policyCache.GetOrAdd(policyName, BuildPermissionPolicy);
        }

        // Fall back to the default policy provider for other policies
        return await _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    // Require an authenticated user and explicitly bind the policy to the JWT
    // and ApiKey schemes. Without this, an unauthenticated caller's empty
    // ClaimsPrincipal would be evaluated against the permission requirement
    // and rejected — but only by happenstance. Binding the scheme list here
    // makes the intent explicit and prevents regressions if a future endpoint
    // sets only [Authorize(Policy=...)] without inheriting a controller-level
    // [Authorize] safety net.
    private static AuthorizationPolicy BuildPermissionPolicy(string policyName) =>
        new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme,
                ApiKeyAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackPolicyProvider.GetFallbackPolicyAsync();
}
