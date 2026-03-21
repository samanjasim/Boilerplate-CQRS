using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Notifications.Queries.GetNotificationPreferences;

public sealed record GetNotificationPreferencesQuery : IRequest<Result<List<NotificationPreferenceDto>>>;
