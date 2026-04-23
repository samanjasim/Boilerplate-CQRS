using MediatR;
using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.SetResourceVisibility;

public sealed record SetResourceVisibilityCommand(
    string ResourceType,
    Guid ResourceId,
    ResourceVisibility Visibility) : IRequest<Result>;
