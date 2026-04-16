using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetNotificationPreferences;

internal sealed class GetNotificationPreferencesQueryHandler(
    CommunicationDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetNotificationPreferencesQuery, Result<List<NotificationPreferenceDto>>>
{
    public async Task<Result<List<NotificationPreferenceDto>>> Handle(
        GetNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<List<NotificationPreferenceDto>>(CommunicationErrors.TenantRequired);

        var userId = currentUserService.UserId!.Value;

        // Get all template categories
        var categories = await context.MessageTemplates
            .AsNoTracking()
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        // Get existing user preferences
        var existing = await context.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        var result = new List<NotificationPreferenceDto>();
        foreach (var category in categories)
        {
            var pref = existing.FirstOrDefault(p => p.Category == category);
            if (pref is not null)
            {
                result.Add(pref.ToDto());
            }
            else
            {
                // Return default preferences for categories without a saved record
                result.Add(new NotificationPreferenceDto(
                    UserId: userId,
                    Category: category,
                    EmailEnabled: true,
                    SmsEnabled: false,
                    PushEnabled: true,
                    WhatsAppEnabled: false,
                    InAppEnabled: true));
            }
        }

        return Result.Success(result);
    }
}
