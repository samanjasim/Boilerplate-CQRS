using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.TransferResourceOwnership;

public sealed record TransferResourceOwnershipCommand(
    string ResourceType,
    Guid ResourceId,
    Guid NewOwnerId) : IRequest<Result>;
