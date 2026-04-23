using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateNotificationPreferences;

public sealed record NotificationPreferenceItem(
    string Category,
    bool EmailEnabled,
    bool SmsEnabled,
    bool PushEnabled,
    bool WhatsAppEnabled,
    bool InAppEnabled);

public sealed record UpdateNotificationPreferencesCommand(
    List<NotificationPreferenceItem> Preferences) : IRequest<Result<List<NotificationPreferenceDto>>>;
