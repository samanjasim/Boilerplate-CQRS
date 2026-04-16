using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;

namespace Starter.Module.Communication.Infrastructure.Services;

public static class SystemTemplateRegistrar
{
    public static async Task SeedAsync(CommunicationDbContext context, ILogger logger)
    {
        var existingNames = await context.MessageTemplates
            .IgnoreQueryFilters()
            .Select(t => t.Name)
            .ToHashSetAsync();

        var templates = GetSystemTemplates();
        var newTemplates = templates.Where(t => !existingNames.Contains(t.Name)).ToList();

        if (newTemplates.Count == 0)
        {
            logger.LogDebug("All system templates already registered");
            return;
        }

        var entities = newTemplates.Select(t => MessageTemplate.Create(
            name: t.Name,
            moduleSource: t.ModuleSource,
            category: t.Category,
            description: t.Description,
            subjectTemplate: t.SubjectTemplate,
            bodyTemplate: t.BodyTemplate,
            defaultChannel: t.DefaultChannel,
            availableChannelsJson: JsonSerializer.Serialize(t.AvailableChannels),
            variableSchemaJson: t.VariableSchema is not null ? JsonSerializer.Serialize(t.VariableSchema) : null,
            sampleVariablesJson: t.SampleVariables is not null ? JsonSerializer.Serialize(t.SampleVariables) : null,
            isSystem: true)).ToList();

        context.MessageTemplates.AddRange(entities);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} system message templates", entities.Count);
    }

    private static List<SystemTemplateDef> GetSystemTemplates() =>
    [
        new("auth.welcome", "Core", "Authentication",
            "Welcome email sent when a new user registers",
            "Welcome to {{appName}}!",
            "Hi {{userName}},\n\nWelcome to {{appName}}! Your account has been created successfully.\n\n{{#verificationUrl}}Please verify your email by clicking: {{verificationUrl}}{{/verificationUrl}}\n\nBest regards,\nThe {{appName}} Team",
            NotificationChannel.Email,
            ["Email", "InApp"],
            new Dictionary<string, string>
            {
                ["userName"] = "User's display name",
                ["appName"] = "Application name",
                ["verificationUrl"] = "Email verification URL (optional)"
            },
            new Dictionary<string, object>
            {
                ["userName"] = "John Doe",
                ["appName"] = "MyApp",
                ["verificationUrl"] = "https://example.com/verify?token=abc123"
            }),

        new("auth.password-reset", "Core", "Authentication",
            "Password reset link email",
            "Reset your password",
            "Hi {{userName}},\n\nWe received a request to reset your password. Click the link below to set a new password:\n\n{{resetUrl}}\n\nThis link expires in {{expiryMinutes}} minutes.\n\nIf you didn't request this, you can safely ignore this email.",
            NotificationChannel.Email,
            ["Email"],
            new Dictionary<string, string>
            {
                ["userName"] = "User's display name",
                ["resetUrl"] = "Password reset URL",
                ["expiryMinutes"] = "Link expiry in minutes"
            },
            new Dictionary<string, object>
            {
                ["userName"] = "John Doe",
                ["resetUrl"] = "https://example.com/reset?token=xyz",
                ["expiryMinutes"] = 30
            }),

        new("auth.email-verification", "Core", "Authentication",
            "Email verification link",
            "Verify your email address",
            "Hi {{userName}},\n\nPlease verify your email address by clicking the link below:\n\n{{verificationUrl}}\n\nThis link expires in {{expiryHours}} hours.",
            NotificationChannel.Email,
            ["Email"],
            new Dictionary<string, string>
            {
                ["userName"] = "User's display name",
                ["verificationUrl"] = "Verification URL",
                ["expiryHours"] = "Link expiry in hours"
            },
            new Dictionary<string, object>
            {
                ["userName"] = "John Doe",
                ["verificationUrl"] = "https://example.com/verify?token=abc",
                ["expiryHours"] = 24
            }),

        new("auth.invitation", "Core", "Authentication",
            "Invitation to join the platform",
            "You've been invited to {{appName}}",
            "Hi {{inviteeName}},\n\n{{inviterName}} has invited you to join {{tenantName}} on {{appName}}.\n\nClick the link below to accept the invitation and set up your account:\n\n{{invitationUrl}}\n\nThis invitation expires in {{expiryDays}} days.",
            NotificationChannel.Email,
            ["Email"],
            new Dictionary<string, string>
            {
                ["inviteeName"] = "Invitee's name",
                ["inviterName"] = "Person who sent the invitation",
                ["tenantName"] = "Organization name",
                ["appName"] = "Application name",
                ["invitationUrl"] = "Invitation acceptance URL",
                ["expiryDays"] = "Invitation expiry in days"
            },
            new Dictionary<string, object>
            {
                ["inviteeName"] = "Jane Smith",
                ["inviterName"] = "John Doe",
                ["tenantName"] = "Acme Corp",
                ["appName"] = "MyApp",
                ["invitationUrl"] = "https://example.com/invite?token=inv123",
                ["expiryDays"] = 7
            }),

        new("auth.login-alert", "Core", "Security",
            "Alert when login from new device/location detected",
            "New login to your {{appName}} account",
            "Hi {{userName}},\n\nWe detected a new login to your account:\n\n- Device: {{deviceInfo}}\n- Location: {{location}}\n- Time: {{loginTime}}\n\nIf this wasn't you, please secure your account immediately.",
            NotificationChannel.Email,
            ["Email", "Push", "InApp"],
            new Dictionary<string, string>
            {
                ["userName"] = "User's display name",
                ["appName"] = "Application name",
                ["deviceInfo"] = "Browser/device information",
                ["location"] = "Approximate login location",
                ["loginTime"] = "Login timestamp"
            },
            new Dictionary<string, object>
            {
                ["userName"] = "John Doe",
                ["appName"] = "MyApp",
                ["deviceInfo"] = "Chrome on macOS",
                ["location"] = "New York, US",
                ["loginTime"] = "2026-04-13 14:30 UTC"
            }),
    ];

    private sealed record SystemTemplateDef(
        string Name,
        string ModuleSource,
        string Category,
        string Description,
        string SubjectTemplate,
        string BodyTemplate,
        NotificationChannel DefaultChannel,
        string[] AvailableChannels,
        Dictionary<string, string>? VariableSchema = null,
        Dictionary<string, object>? SampleVariables = null);
}
