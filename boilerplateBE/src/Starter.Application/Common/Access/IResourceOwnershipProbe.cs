using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Common.Access;

public interface IResourceOwnershipProbe
{
    Task<Result> EnsureCallerCanShareAsync(string resourceType, Guid resourceId, CancellationToken ct);
    Task<Result> EnsureSubjectValidAsync(GrantSubjectType subjectType, Guid subjectId, CancellationToken ct);
    Task<Result<Guid>> GetOwnerAsync(string resourceType, Guid resourceId, CancellationToken ct);
    Task<Result> SetVisibilityAsync(string resourceType, Guid resourceId, ResourceVisibility visibility, CancellationToken ct);
    Task<Result> TransferOwnershipAsync(string resourceType, Guid resourceId, Guid newOwnerId, CancellationToken ct);
}
