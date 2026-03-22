using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using RoleConstants = Starter.Shared.Constants.Roles;

namespace Starter.Infrastructure.Services;

public sealed class NotificationService(
    IApplicationDbContext context,
    IRealtimeService realtimeService,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateAsync(
        Guid userId,
        Guid? tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken ct = default)
    {
        var notification = Notification.Create(userId, tenantId, type, title, message, data);

        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);

        await realtimeService.PublishToUserAsync(userId, "notification", new
        {
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Data,
            notification.CreatedAt
        }, ct);

        logger.LogInformation(
            "Notification created for user {UserId}: {Type} - {Title}",
            userId, type, title);
    }

    public async Task CreateForTenantAdminsAsync(
        Guid tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken ct = default)
    {
        var adminUserIds = await context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.Role != null &&
                         ur.Role.Name == RoleConstants.Admin &&
                         context.Users.Any(u => u.Id == ur.UserId && u.TenantId == tenantId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var adminUserId in adminUserIds)
        {
            await CreateAsync(adminUserId, tenantId, type, title, message, data, ct);
        }
    }
}
