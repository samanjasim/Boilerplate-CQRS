using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.RemoveRequiredNotification;

public sealed record RemoveRequiredNotificationCommand(Guid Id) : IRequest<Result>;
