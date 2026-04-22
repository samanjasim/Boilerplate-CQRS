using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Access.DTOs;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// Stubbed IResourceAccessService for RAG retrieval tests. Defaults to admin-bypass so
/// tests that don't care about ACL filtering keep their pre-4b-8 behaviour. Tests that
/// want to exercise the acl-resolve stage set <see cref="ForceAdminBypass"/> to false
/// and populate <see cref="GrantedResourceIds"/>, or set <see cref="ThrowOnResolve"/>
/// to simulate a resolver failure.
/// </summary>
public sealed class FakeResourceAccessService : IResourceAccessService
{
    public bool ForceAdminBypass { get; set; } = true;
    public List<Guid> GrantedResourceIds { get; } = new();
    public bool ThrowOnResolve { get; set; }
    public int ResolveCallCount { get; private set; }

    public Task<Guid> GrantAsync(string resourceType, Guid resourceId, GrantSubjectType subjectType, Guid subjectId, AccessLevel level, CancellationToken ct)
        => Task.FromResult(Guid.NewGuid());

    public Task RevokeAsync(Guid grantId, CancellationToken ct) => Task.CompletedTask;

    public Task RevokeAllForResourceAsync(string resourceType, Guid resourceId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<ResourceGrantDto>> ListGrantsAsync(string resourceType, Guid resourceId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ResourceGrantDto>>(Array.Empty<ResourceGrantDto>());

    public Task<bool> CanAccessAsync(ICurrentUserService user, string resourceType, Guid resourceId, AccessLevel minLevel, CancellationToken ct)
        => Task.FromResult(true);

    public Task<AccessResolution> ResolveAccessibleResourcesAsync(ICurrentUserService user, string resourceType, CancellationToken ct)
    {
        ResolveCallCount++;
        if (ThrowOnResolve)
            throw new InvalidOperationException("Simulated acl-resolve failure");
        return Task.FromResult(new AccessResolution(ForceAdminBypass, GrantedResourceIds));
    }

    public Task InvalidateUserAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;

    public Task InvalidateRoleMembersAsync(Guid roleId, CancellationToken ct) => Task.CompletedTask;
}
