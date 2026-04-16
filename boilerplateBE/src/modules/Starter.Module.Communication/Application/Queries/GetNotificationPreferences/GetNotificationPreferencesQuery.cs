using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetNotificationPreferences;

public sealed record GetNotificationPreferencesQuery : IRequest<Result<List<NotificationPreferenceDto>>>;
