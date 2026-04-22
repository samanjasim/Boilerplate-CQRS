using Starter.Application.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;

namespace Starter.Api.Tests.Access._Helpers;

/// <summary>
/// Test double for IResourceOwnershipProbe. All checks pass by default so tests focus
/// on the specific behaviour under test. Individual tests can flip flags to simulate failures.
/// </summary>
public sealed class FakeResourceOwnershipProbe : IResourceOwnershipProbe
{
    public bool BlockCallerShare { get; set; }
    public bool BlockSubjectValid { get; set; }
    public string DisplayName { get; set; } = "Test Resource";
    public Guid OwnerId { get; set; } = Guid.NewGuid();
    public SetVisibilityInvocation? LastSetVisibility { get; private set; }
    public TransferOwnershipInvocation? LastTransferOwnership { get; private set; }

    public Task<Result> EnsureCallerCanShareAsync(string resourceType, Guid resourceId, CancellationToken ct) =>
        Task.FromResult(BlockCallerShare
            ? Result.Failure(Error.Failure("Owner.Required", "Only the owner can share"))
            : Result.Success());

    public Task<Result> EnsureSubjectValidAsync(GrantSubjectType subjectType, Guid subjectId, CancellationToken ct) =>
        Task.FromResult(BlockSubjectValid
            ? Result.Failure(Error.NotFound("Subject.NotFound", "Subject not found"))
            : Result.Success());

    public Task<Result<Guid>> GetOwnerAsync(string resourceType, Guid resourceId, CancellationToken ct) =>
        Task.FromResult(Result.Success(OwnerId));

    public Task<Result<string>> GetResourceDisplayNameAsync(string resourceType, Guid resourceId, CancellationToken ct) =>
        Task.FromResult(Result.Success(DisplayName));

    public Task<Result> SetVisibilityAsync(string resourceType, Guid resourceId, ResourceVisibility visibility, CancellationToken ct)
    {
        LastSetVisibility = new SetVisibilityInvocation(resourceType, resourceId, visibility);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> TransferOwnershipAsync(string resourceType, Guid resourceId, Guid newOwnerId, CancellationToken ct)
    {
        LastTransferOwnership = new TransferOwnershipInvocation(resourceType, resourceId, newOwnerId);
        return Task.FromResult(Result.Success());
    }

    public record SetVisibilityInvocation(string ResourceType, Guid ResourceId, ResourceVisibility Visibility);
    public record TransferOwnershipInvocation(string ResourceType, Guid ResourceId, Guid NewOwnerId);
}
