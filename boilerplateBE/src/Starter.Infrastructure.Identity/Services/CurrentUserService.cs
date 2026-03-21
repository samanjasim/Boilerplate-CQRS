using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Identity.Services;

/// <summary>
/// Current user service implementation using HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userId, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [];

    public IEnumerable<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value) ?? [];

    public Guid? TenantId
    {
        get
        {
            // First try JWT claim
            var claim = User?.FindFirstValue("tenant_id");
            if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var claimId))
                return claimId;

            // Fall back to X-Tenant-Id header
            var header = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header) && Guid.TryParse(header, out var headerId))
                return headerId;

            return null;
        }
    }

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public bool IsInRole(string role) =>
        User?.IsInRole(role) ?? false;
}
