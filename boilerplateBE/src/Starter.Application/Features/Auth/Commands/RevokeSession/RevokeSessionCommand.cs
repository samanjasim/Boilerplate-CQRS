using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.RevokeSession;

public sealed record RevokeSessionCommand(Guid SessionId) : IRequest<Result>;
