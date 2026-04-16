using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateNotificationPreferences;

internal sealed class UpdateNotificationPreferencesCommandHandler(
    CommunicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateNotificationPreferencesCommand, Result<List<NotificationPreferenceDto>>>
{
    public async Task<Result<List<NotificationPreferenceDto>>> Handle(
        UpdateNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<List<NotificationPreferenceDto>>(CommunicationErrors.TenantRequired);

        var userId = currentUserService.UserId!.Value;

        var existing = await dbContext.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var item in request.Preferences)
        {
            var pref = existing.FirstOrDefault(p => p.Category == item.Category);
            if (pref is not null)
            {
                pref.Update(item.EmailEnabled, item.SmsEnabled, item.PushEnabled,
                    item.WhatsAppEnabled, item.InAppEnabled);
            }
            else
            {
                pref = CommunicationNotificationPreference.Create(userId, tenantId.Value, item.Category);
                pref.Update(item.EmailEnabled, item.SmsEnabled, item.PushEnabled,
                    item.WhatsAppEnabled, item.InAppEnabled);
                dbContext.NotificationPreferences.Add(pref);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Category)
            .ToListAsync(cancellationToken);

        return Result.Success(updated.Select(p => p.ToDto()).ToList());
    }
}
