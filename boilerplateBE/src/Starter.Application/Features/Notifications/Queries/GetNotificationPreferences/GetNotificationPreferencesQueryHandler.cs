using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Notifications.DTOs;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Notifications.Queries.GetNotificationPreferences;

internal sealed class GetNotificationPreferencesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetNotificationPreferencesQuery, Result<List<NotificationPreferenceDto>>>
{
    private static readonly string[] AllTypes =
    [
        NotificationType.UserCreated,
        NotificationType.UserInvited,
        NotificationType.RoleChanged,
        NotificationType.PasswordChanged,
        NotificationType.TenantCreated,
        NotificationType.InvitationAccepted,
        NotificationType.LoginFromNewDevice,
        NotificationType.CommentMentioned,
    ];

    public async Task<Result<List<NotificationPreferenceDto>>> Handle(
        GetNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<List<NotificationPreferenceDto>>(UserErrors.Unauthorized());

        var existing = await context.Set<NotificationPreference>()
            .AsNoTracking()
            .Where(np => np.UserId == userId.Value)
            .ToDictionaryAsync(np => np.NotificationType, cancellationToken);

        var result = AllTypes.Select(type =>
        {
            if (existing.TryGetValue(type, out var pref))
                return new NotificationPreferenceDto(type, pref.EmailEnabled, pref.InAppEnabled);

            return new NotificationPreferenceDto(type, true, true);
        }).ToList();

        return Result.Success(result);
    }
}
