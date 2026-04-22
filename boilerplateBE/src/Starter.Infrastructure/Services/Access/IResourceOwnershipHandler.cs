using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;

namespace Starter.Infrastructure.Services.Access;

public interface IResourceOwnershipHandler
{
    string ResourceType { get; }
    Task<Result<Guid>> GetOwnerAsync(Guid resourceId, CancellationToken ct);
    Task<Result> SetVisibilityAsync(Guid resourceId, ResourceVisibility visibility, CancellationToken ct);
    Task<Result> TransferOwnershipAsync(Guid resourceId, Guid newOwnerId, CancellationToken ct);
}
