using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid Id) : IRequest<Result>;
