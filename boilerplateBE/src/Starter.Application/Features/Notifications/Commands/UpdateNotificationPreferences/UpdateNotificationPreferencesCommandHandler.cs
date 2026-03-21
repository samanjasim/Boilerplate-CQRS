using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Notifications.Commands.UpdateNotificationPreferences;

internal sealed class UpdateNotificationPreferencesCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateNotificationPreferencesCommand, Result>
{
    public async Task<Result> Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure(UserErrors.Unauthorized());

        var existing = await context.NotificationPreferences
            .Where(np => np.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        var existingDict = existing.ToDictionary(np => np.NotificationType);

        foreach (var item in request.Preferences)
        {
            if (existingDict.TryGetValue(item.NotificationType, out var pref))
            {
                pref.EmailEnabled = item.EmailEnabled;
                pref.InAppEnabled = item.InAppEnabled;
            }
            else
            {
                context.NotificationPreferences.Add(new NotificationPreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    NotificationType = item.NotificationType,
                    EmailEnabled = item.EmailEnabled,
                    InAppEnabled = item.InAppEnabled,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
