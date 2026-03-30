using Starter.Domain.Common;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.ValueObjects;
using Starter.Domain.Tenants.Entities;
using Starter.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoleNames = Starter.Shared.Constants.Roles;

namespace Starter.Infrastructure.Persistence.Seeds;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            var resetDatabase = configuration.GetValue<bool>("DatabaseSettings:ResetDatabase");
            if (resetDatabase)
            {
                logger.LogWarning("ResetDatabase is enabled — dropping and recreating database");
                await context.Database.EnsureDeletedAsync();
            }

            await context.Database.MigrateAsync();

            await SeedPermissionsAsync(context, logger);
            await SeedRolesAsync(context, logger);
            await SeedRolePermissionsAsync(context, logger);
            await SeedDefaultTenantAsync(context, logger);
            await SeedSuperAdminUserAsync(context, configuration, logger);
            await SeedDefaultSettingsAsync(context, logger);
            await SeedFeatureFlagsAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private static async Task SeedPermissionsAsync(ApplicationDbContext context, ILogger logger)
    {
        var allPermissions = Permissions.GetAll().ToList();
        var existingPermissions = await context.Permissions
            .Select(p => p.Name)
            .ToListAsync();

        var newPermissions = allPermissions
            .Where(p => !existingPermissions.Contains(p))
            .ToList();

        if (newPermissions.Count == 0) return;

        foreach (var permissionName in newPermissions)
        {
            var module = permissionName.Contains('.')
                ? permissionName[..permissionName.IndexOf('.')]
                : null;

            var permission = Permission.Create(
                permissionName,
                $"Permission to {permissionName.Replace(".", " ").ToLowerInvariant()}",
                module);

            context.Permissions.Add(permission);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} permissions", newPermissions.Count);
    }

    private static async Task SeedRolesAsync(ApplicationDbContext context, ILogger logger)
    {
        var allRoles = RoleNames.GetAll().ToList();
        var existingRoles = await context.Roles
            .Select(r => r.Name)
            .ToListAsync();

        var newRoles = allRoles
            .Where(r => !existingRoles.Contains(r))
            .ToList();

        if (newRoles.Count == 0) return;

        foreach (var roleName in newRoles)
        {
            var role = Role.Create(
                roleName,
                $"{roleName} system role",
                isSystemRole: true);

            context.Roles.Add(role);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} roles", newRoles.Count);
    }

    private static async Task SeedRolePermissionsAsync(ApplicationDbContext context, ILogger logger)
    {
        var rolePermissionMappings = RoleNames.GetRolePermissions().ToList();
        var roles = await context.Roles
            .Include(r => r.RolePermissions)
            .ToListAsync();
        var permissions = await context.Permissions.ToListAsync();

        var seededCount = 0;

        foreach (var (roleName, permissionNames) in rolePermissionMappings)
        {
            var role = roles.FirstOrDefault(r => r.Name == roleName);
            if (role is null) continue;

            foreach (var permissionName in permissionNames)
            {
                var permission = permissions.FirstOrDefault(p => p.Name == permissionName);
                if (permission is null) continue;

                var exists = role.RolePermissions.Any(rp => rp.PermissionId == permission.Id);
                if (exists) continue;

                role.AddPermission(permission);
                seededCount++;
            }
        }

        if (seededCount > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} role-permission mappings", seededCount);
        }
    }

    private static async Task SeedDefaultTenantAsync(ApplicationDbContext context, ILogger logger)
    {
        var exists = await context.Tenants
            .AnyAsync(t => t.Slug == "default");

        if (exists) return;

        var tenant = Tenant.Create("Default Organization", "default");
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded default tenant: {TenantName}", tenant.Name);
    }

    private static async Task SeedSuperAdminUserAsync(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger logger)
    {
        var superAdminEmail = configuration["SeedSettings:SuperAdmin:Email"] ?? "admin@starter.com";
        var superAdminPassword = configuration["SeedSettings:SuperAdmin:Password"] ?? "Admin@123456";
        var superAdminFirstName = configuration["SeedSettings:SuperAdmin:FirstName"] ?? "Super";
        var superAdminLastName = configuration["SeedSettings:SuperAdmin:LastName"] ?? "Admin";
        var superAdminUsername = configuration["SeedSettings:SuperAdmin:Username"] ?? "superadmin";

        var existingUser = await context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Username == superAdminUsername);

        if (existingUser is not null) return;

        var email = Starter.Domain.Identity.ValueObjects.Email.Create(superAdminEmail);
        var fullName = FullName.Create(superAdminFirstName, superAdminLastName);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(superAdminPassword);

        var user = User.Create(superAdminUsername, email, fullName, passwordHash);

        user.ConfirmEmail();
        user.Activate();

        var superAdminRole = await context.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.SuperAdmin);

        if (superAdminRole is not null)
        {
            user.AddRole(superAdminRole);
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded SuperAdmin user: {Username}", superAdminUsername);
    }

    private static async Task SeedDefaultSettingsAsync(ApplicationDbContext context, ILogger logger)
    {
        var defaultSettings = new (string Key, string Value, string Description, string Category, bool IsSecret, string DataType)[]
        {
            // Application
            ("App.Name", "Starter", "Application display name", "Application", false, "text"),
            ("App.Timezone", "UTC", "Default timezone", "Application", false, "text"),
            ("App.DateFormat", "yyyy-MM-dd", "Date display format", "Application", false, "text"),
            ("App.Currency", "USD", "Default currency code", "Application", false, "text"),
            ("App.Language", "en", "Default language", "Application", false, "text"),
            ("App.MaintenanceMode", "false", "Enable maintenance mode", "Application", false, "boolean"),
            ("App.FrontendUrl", "http://localhost:3000", "Frontend application URL", "Application", false, "url"),

            // Email
            ("Email.FromName", "Starter", "Email sender display name", "Email", false, "text"),
            ("Email.FromAddress", "noreply@starter.com", "Email sender address", "Email", false, "email"),
            ("Email.SmtpHost", "localhost", "SMTP server hostname", "Email", false, "text"),
            ("Email.SmtpPort", "587", "SMTP server port", "Email", false, "number"),
            ("Email.SmtpUsername", "", "SMTP authentication username", "Email", true, "password"),
            ("Email.SmtpPassword", "", "SMTP authentication password", "Email", true, "password"),
            ("Email.SmtpEnableSsl", "true", "Use SSL/TLS for SMTP", "Email", false, "boolean"),

            // SMS
            ("Sms.TwilioEnabled", "false", "Enable Twilio SMS service", "SMS", false, "boolean"),
            ("Sms.TwilioAccountSid", "", "Twilio account SID", "SMS", true, "password"),
            ("Sms.TwilioAuthToken", "", "Twilio auth token", "SMS", true, "password"),
            ("Sms.TwilioFromNumber", "", "SMS sender phone number", "SMS", false, "text"),

            // Notifications
            ("Notifications.AblyEnabled", "false", "Enable Ably real-time notifications", "Notifications", false, "boolean"),
            ("Notifications.AblyApiKey", "", "Ably API key", "Notifications", true, "password"),
            ("Notifications.PollingIntervalSeconds", "30", "Notification polling interval in seconds", "Notifications", false, "number"),

            // Security
            ("Security.MaxLoginAttempts", "5", "Max failed login attempts before lockout", "Security", false, "number"),
            ("Security.LockoutDuration", "15", "Account lockout duration in minutes", "Security", false, "number"),
            ("Security.PasswordMinLength", "8", "Minimum password length", "Security", false, "number"),
            ("Security.SessionTimeout", "1440", "Session timeout in minutes", "Security", false, "number"),
            ("Security.OtpExpirationMinutes", "10", "OTP code expiration in minutes", "Security", false, "number"),
            ("Security.OtpMaxAttempts", "3", "Max OTP generation attempts per window", "Security", false, "number"),
            ("Security.AccessTokenExpirationMinutes", "60", "JWT access token lifetime in minutes", "Security", false, "number"),
            ("Security.RefreshTokenExpirationDays", "7", "Refresh token lifetime in days", "Security", false, "number"),

            // Reports
            ("Reports.CacheDurationMinutes", "60", "Report cache duration in minutes", "Reports", false, "number"),
            ("Reports.MaxFileSizeMb", "50", "Maximum report file size in MB", "Reports", false, "number"),

            // Files
            ("Files.TempFileTtlMinutes", "120", "Time-to-live for temporary file uploads in minutes", "Files", false, "number"),
            ("Files.OrphanCleanupIntervalMinutes", "30", "Interval between orphan file cleanup runs in minutes", "Files", false, "number"),
            ("Files.MaxUploadSizeMb", "50", "Maximum file upload size in MB", "Files", false, "number"),
            ("Reports.FileExpirationHours", "24", "Hours until time-sensitive report files expire", "Reports", false, "number"),

            // Registration
            ("registration.default_role_id", "", "Default role ID for new user registrations (leave empty for system User role)", "Registration", false, "role-select"),
        };

        var existingSettings = await context.SystemSettings
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == null)
            .ToListAsync();

        var existingByKey = existingSettings.ToDictionary(s => s.Key);
        var seededCount = 0;
        var updatedCount = 0;

        foreach (var (key, value, description, category, isSecret, dataType) in defaultSettings)
        {
            if (existingByKey.TryGetValue(key, out var existing))
            {
                // Update DataType if it changed
                if (existing.DataType != dataType)
                {
                    existing.UpdateDataType(dataType);
                    updatedCount++;
                }

                continue;
            }

            var setting = SystemSetting.Create(
                key,
                value,
                tenantId: null,
                description: description,
                category: category,
                isSecret: isSecret,
                dataType: dataType);

            context.SystemSettings.Add(setting);
            seededCount++;
        }

        if (seededCount > 0 || updatedCount > 0)
        {
            await context.SaveChangesAsync();
        }

        if (seededCount > 0)
            logger.LogInformation("Seeded {Count} default system settings", seededCount);

        if (updatedCount > 0)
            logger.LogInformation("Updated DataType on {Count} existing system settings", updatedCount);
    }

    private static async Task SeedFeatureFlagsAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.FeatureFlags.AnyAsync())
        {
            logger.LogInformation("Feature flags already seeded");
            return;
        }

        var flags = new[]
        {
            FeatureFlag.Create("users.max_count", "Max Users", "Maximum number of users per tenant", "100", FlagValueType.Integer, FlagCategory.Users, false),
            FeatureFlag.Create("users.invitations_enabled", "Invitations Enabled", "Allow sending user invitations", "true", FlagValueType.Boolean, FlagCategory.Users, false),
            FeatureFlag.Create("files.max_upload_size_mb", "Max Upload Size (MB)", "Maximum single file upload size in megabytes", "50", FlagValueType.Integer, FlagCategory.Files, false),
            FeatureFlag.Create("files.max_storage_mb", "Max Storage (MB)", "Maximum total storage per tenant in megabytes", "5120", FlagValueType.Integer, FlagCategory.Files, false),
            FeatureFlag.Create("reports.enabled", "Reports Enabled", "Enable report generation", "true", FlagValueType.Boolean, FlagCategory.Reports, false),
            FeatureFlag.Create("reports.max_concurrent", "Max Concurrent Reports", "Maximum concurrent report generation jobs", "3", FlagValueType.Integer, FlagCategory.Reports, false),
            FeatureFlag.Create("reports.pdf_export", "PDF Export", "Enable PDF export for reports", "true", FlagValueType.Boolean, FlagCategory.Reports, false),
            FeatureFlag.Create("api_keys.enabled", "API Keys Enabled", "Enable API key management", "true", FlagValueType.Boolean, FlagCategory.ApiKeys, false),
            FeatureFlag.Create("api_keys.max_count", "Max API Keys", "Maximum number of API keys per tenant", "10", FlagValueType.Integer, FlagCategory.ApiKeys, false),
            FeatureFlag.Create("ui.maintenance_mode", "Maintenance Mode", "Show maintenance page to non-admin users", "false", FlagValueType.Boolean, FlagCategory.System, true),
            FeatureFlag.Create("billing.enabled", "Billing Enabled", "Enable billing and subscription features", "false", FlagValueType.Boolean, FlagCategory.Billing, true),
            FeatureFlag.Create("roles.tenant_custom_enabled", "Tenant Custom Roles", "Allow tenants to create custom roles", "false", FlagValueType.Boolean, FlagCategory.System, false),
        };

        context.FeatureFlags.AddRange(flags);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} default feature flags", flags.Length);
    }
}
