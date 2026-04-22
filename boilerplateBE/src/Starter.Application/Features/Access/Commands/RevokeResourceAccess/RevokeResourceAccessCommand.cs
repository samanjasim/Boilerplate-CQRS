using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.RevokeResourceAccess;

public sealed record RevokeResourceAccessCommand(Guid GrantId) : IRequest<Result>;
