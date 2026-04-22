using MediatR;
using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.GrantResourceAccess;

public sealed record GrantResourceAccessCommand(
    string ResourceType,
    Guid ResourceId,
    GrantSubjectType SubjectType,
    Guid SubjectId,
    AccessLevel Level) : IRequest<Result<Guid>>;
