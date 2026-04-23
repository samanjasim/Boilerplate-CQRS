using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Access._Helpers;

public sealed class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public bool IsAuthenticated { get; init; } = true;
    public IEnumerable<string> Roles { get; init; } = Array.Empty<string>();
    public IEnumerable<string> Permissions { get; init; } = Array.Empty<string>();
    public Guid? TenantId { get; init; }

    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasPermission(string permission) => Permissions.Contains(permission);

    public static FakeCurrentUser For(
        Guid userId,
        Guid tenantId,
        IEnumerable<string>? roles = null,
        IEnumerable<string>? permissions = null,
        bool admin = false)
    {
        return new FakeCurrentUser
        {
            UserId = userId,
            TenantId = tenantId,
            Email = $"user-{userId:N}@test.local",
            Roles = admin
                ? new[] { "Admin" }
                : (roles?.ToArray() ?? Array.Empty<string>()),
            Permissions = permissions?.ToArray() ?? Array.Empty<string>(),
        };
    }
}
