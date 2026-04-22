using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Access.DTOs;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;

namespace Starter.Application.Common.Access;

public interface IResourceAccessService
{
    Task<Guid> GrantAsync(
        string resourceType,
        Guid resourceId,
        GrantSubjectType subjectType,
        Guid subjectId,
        AccessLevel level,
        CancellationToken ct);

    Task RevokeAsync(Guid grantId, CancellationToken ct);

    Task RevokeAllForResourceAsync(string resourceType, Guid resourceId, CancellationToken ct);

    Task<IReadOnlyList<ResourceGrantDto>> ListGrantsAsync(
        string resourceType,
        Guid resourceId,
        CancellationToken ct);

    Task<bool> CanAccessAsync(
        ICurrentUserService user,
        string resourceType,
        Guid resourceId,
        AccessLevel minLevel,
        CancellationToken ct);

    Task<AccessResolution> ResolveAccessibleResourcesAsync(
        ICurrentUserService user,
        string resourceType,
        CancellationToken ct);

    Task InvalidateUserAsync(Guid userId, CancellationToken ct);

    Task InvalidateRoleMembersAsync(Guid roleId, CancellationToken ct);
}
