using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// Minimal ICurrentUserService for RAG retrieval tests. Tests that don't care about the
/// caller identity (most pre-4b-8 tests) pick up the defaults; ACL tests override
/// <see cref="UserId"/>, <see cref="TenantId"/>, and <see cref="Roles"/> as needed.
/// </summary>
public sealed class FakeCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? Email { get; set; } = "test-user@example.com";
    public bool IsAuthenticated { get; set; } = true;
    public IEnumerable<string> Roles { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
    public Guid? TenantId { get; set; }

    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
