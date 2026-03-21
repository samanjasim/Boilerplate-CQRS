using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.RevokeInvite;

public sealed record RevokeInviteCommand(Guid Id) : IRequest<Result>;
