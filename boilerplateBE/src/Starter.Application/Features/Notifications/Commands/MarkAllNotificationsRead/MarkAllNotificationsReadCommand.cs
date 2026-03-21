using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand : IRequest<Result>;
