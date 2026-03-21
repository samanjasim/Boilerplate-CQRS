using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Notifications.Commands.UpdateNotificationPreferences;

public sealed record UpdateNotificationPreferencesCommand(
    List<UpdateNotificationPreferenceItem> Preferences) : IRequest<Result>;

public sealed record UpdateNotificationPreferenceItem(
    string NotificationType,
    bool EmailEnabled,
    bool InAppEnabled);
