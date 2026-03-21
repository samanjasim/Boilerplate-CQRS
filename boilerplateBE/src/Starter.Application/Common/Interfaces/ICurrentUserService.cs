namespace Starter.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }
    IEnumerable<string> Permissions { get; }
    Guid? TenantId { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
}
