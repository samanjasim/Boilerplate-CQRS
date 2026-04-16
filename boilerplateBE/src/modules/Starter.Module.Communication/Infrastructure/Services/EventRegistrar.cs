using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Infrastructure.Persistence;

namespace Starter.Module.Communication.Infrastructure.Services;

public static class EventRegistrar
{
    public static async Task SeedAsync(CommunicationDbContext context, ILogger logger)
    {
        var existingNames = await context.EventRegistrations
            .Select(e => e.EventName)
            .ToHashSetAsync();

        var coreEvents = GetCoreEvents();
        var newEvents = coreEvents.Where(e => !existingNames.Contains(e.EventName)).ToList();

        if (newEvents.Count == 0)
        {
            logger.LogDebug("All core events already registered");
            return;
        }

        var entities = newEvents.Select(e => EventRegistration.Create(
            eventName: e.EventName,
            moduleSource: e.ModuleSource,
            displayName: e.DisplayName,
            description: e.Description)).ToList();

        context.EventRegistrations.AddRange(entities);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} core event registrations", entities.Count);
    }

    private static List<CoreEventDef> GetCoreEvents() =>
    [
        new("user.created", "Core", "User Created", "Fired when a new user account is created"),
        new("user.updated", "Core", "User Updated", "Fired when a user profile is updated"),
        new("user.suspended", "Core", "User Suspended", "Fired when a user account is suspended"),
        new("user.activated", "Core", "User Activated", "Fired when a user account is activated"),
        new("tenant.registered", "Core", "Tenant Registered", "Fired when a new tenant organization registers"),
        new("password.reset.requested", "Core", "Password Reset Requested", "Fired when a password reset is requested"),
        new("invitation.sent", "Core", "Invitation Sent", "Fired when a user invitation is sent"),
        new("invitation.accepted", "Core", "Invitation Accepted", "Fired when a user accepts an invitation"),
        new("password.changed", "Core", "Password Changed", "Fired when a user changes their password"),
        new("file.uploaded", "Core", "File Uploaded", "Fired when a file is uploaded"),
        new("file.deleted", "Core", "File Deleted", "Fired when a file is deleted"),
    ];

    private sealed record CoreEventDef(
        string EventName,
        string ModuleSource,
        string DisplayName,
        string? Description);
}
