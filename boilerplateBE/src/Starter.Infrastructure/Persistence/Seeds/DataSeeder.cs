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
using Starter.Abstractions.Modularity;
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

            // Resolve discovered modules from DI (registered in Program.cs)
            var modules = scope.ServiceProvider
                .GetService<IReadOnlyList<IModule>>() ?? [];

            // Apply each module's own migrations (module-owned DbContexts with
            // their own __EFMigrationsHistory_{Module} tables). Must run before
            // module seed data so seeds write against a migrated schema.
            foreach (var module in modules)
                await module.MigrateAsync(serviceProvider);

            await SeedPermissionsAsync(context, logger, modules);
            await SeedRolesAsync(context, logger);
            await SeedRolePermissionsAsync(context, logger, modules);
            await SeedDefaultTenantAsync(context, logger);
            await SeedSuperAdminUserAsync(context, configuration, logger);
            await SeedDemoTenantsAsync(context, logger);
            await SeedDefaultSettingsAsync(context, logger);
            await SeedFeatureFlagsAsync(context, logger);

            // Module-owned seed data (e.g. Billing plans). Runs last so every
            // module can rely on core permissions, roles, and the default tenant
            // being present. Gated by the same SeedDataOnStartup flag as core.
            foreach (var module in modules)
            {
                logger.LogInformation("Seeding module {Module}", module.Name);
                await module.SeedDataAsync(serviceProvider);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private static async Task SeedPermissionsAsync(
        ApplicationDbContext context, ILogger logger, IReadOnlyList<IModule> modules)
    {
        var allPermissions = Permissions.GetAllWithMetadata()
            .Concat(modules.SelectMany(m => m.GetPermissions()))
            .ToList();

        var existingPermissions = await context.Permissions
            .Select(p => p.Name)
            .ToListAsync();

        var newPermissions = allPermissions
            .Where(p => !existingPermissions.Contains(p.Name))
            .ToList();

        if (newPermissions.Count == 0) return;

        foreach (var (name, description, module) in newPermissions)
        {
            var permission = Permission.Create(name, description, module);
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

    private static async Task SeedRolePermissionsAsync(
        ApplicationDbContext context, ILogger logger, IReadOnlyList<IModule> modules)
    {
        var rolePermissionMappings = RoleNames.GetRolePermissions()
            .Concat(modules.SelectMany(m => m.GetDefaultRolePermissions()))
            .ToList();
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

    // Demo tenants used for multi-tenant dev/test scenarios (e.g. verifying
    // tenant isolation, switching roles). Idempotent per slug — each tenant's
    // block is skipped if the slug already exists. Credentials are documented
    // in CLAUDE.md under "Default Credentials".
    private static readonly (string Name, string Slug, (string Username, string FirstName, string LastName, string Role)[] Users)[] DemoTenants =
    [
        ("Acme Corporation", "acme", [
            ("acme.admin",   "Alice",  "Anderson", RoleNames.Admin),
            ("acme.alice",   "Alice",  "Baker",    RoleNames.User),
            ("acme.bob",     "Bob",    "Carter",   RoleNames.User),
        ]),
        ("Globex Industries", "globex", [
            ("globex.admin", "Gloria", "Greene",   RoleNames.Admin),
            ("globex.hank",  "Hank",   "Harrison", RoleNames.User),
            ("globex.ivy",   "Ivy",    "Ingram",   RoleNames.User),
        ]),
        ("Initech Systems", "initech", [
            ("initech.admin","Peter",  "Parker",   RoleNames.Admin),
            ("initech.milton","Milton","Waddams",  RoleNames.User),
            ("initech.samir","Samir",  "Nagheenanajar", RoleNames.User),
        ]),
    ];

    private const string DemoUserPassword = "Admin@123456";

    private static async Task SeedDemoTenantsAsync(ApplicationDbContext context, ILogger logger)
    {
        var rolesByName = await context.Roles.ToDictionaryAsync(r => r.Name);
        var createdTenants = 0;
        var createdUsers = 0;

        foreach (var (name, slug, users) in DemoTenants)
        {
            var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant is null)
            {
                tenant = Tenant.Create(name, slug);
                tenant.Activate();
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
                createdTenants++;
            }

            foreach (var (username, firstName, lastName, roleName) in users)
            {
                var exists = await context.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Username == username);
                if (exists) continue;

                var user = User.Create(
                    username,
                    Starter.Domain.Identity.ValueObjects.Email.Create($"{username}@{slug}.com"),
                    FullName.Create(firstName, lastName),
                    BCrypt.Net.BCrypt.HashPassword(DemoUserPassword),
                    tenant.Id);

                user.ConfirmEmail();
                user.Activate();

                if (rolesByName.TryGetValue(roleName, out var role))
                    user.AddRole(role);

                context.Users.Add(user);
                createdUsers++;
            }
        }

        if (createdTenants == 0 && createdUsers == 0) return;

        await context.SaveChangesAsync();
        logger.LogInformation(
            "Seeded {Tenants} demo tenant(s) and {Users} demo user(s) (password: {Password})",
            createdTenants, createdUsers, DemoUserPassword);
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
            ("registration.tenant_owner_role_id", "", "Default role for users who register a new tenant via Get Started (leave empty for system Admin role)", "Registration", false, "role-select"),
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
        if (await context.Set<FeatureFlag>().AnyAsync())
            return;

        var flags = new[]
        {
            FeatureFlag.Create("users.max_count", "Max Users", "Maximum number of users per tenant", "100", FlagValueType.Integer, FlagCategory.Users, true),
            FeatureFlag.Create("users.invitations_enabled", "Invitations Enabled", "Allow sending user invitations", "true", FlagValueType.Boolean, FlagCategory.Users, true),
            FeatureFlag.Create("files.max_upload_size_mb", "Max Upload Size (MB)", "Maximum single file upload size in megabytes", "50", FlagValueType.Integer, FlagCategory.Files, true),
            FeatureFlag.Create("files.max_storage_mb", "Max Storage (MB)", "Maximum total storage per tenant in megabytes", "5120", FlagValueType.Integer, FlagCategory.Files, true),
            FeatureFlag.Create("reports.enabled", "Reports Enabled", "Enable report generation", "true", FlagValueType.Boolean, FlagCategory.Reports, true),
            FeatureFlag.Create("reports.max_concurrent", "Max Concurrent Reports", "Maximum concurrent report generation jobs", "3", FlagValueType.Integer, FlagCategory.Reports, true),
            FeatureFlag.Create("reports.pdf_export", "PDF Export", "Enable PDF export for reports", "true", FlagValueType.Boolean, FlagCategory.Reports, true),
            FeatureFlag.Create("api_keys.enabled", "API Keys Enabled", "Enable API key management", "true", FlagValueType.Boolean, FlagCategory.ApiKeys, true),
            FeatureFlag.Create("api_keys.max_count", "Max API Keys", "Maximum number of API keys per tenant", "10", FlagValueType.Integer, FlagCategory.ApiKeys, true),
            FeatureFlag.Create("ui.maintenance_mode", "Maintenance Mode", "Show maintenance page to non-admin users", "false", FlagValueType.Boolean, FlagCategory.System, true),
            FeatureFlag.Create("billing.enabled", "Billing Enabled", "Enable billing and subscription features", "false", FlagValueType.Boolean, FlagCategory.Billing, true),
            FeatureFlag.Create("roles.tenant_custom_enabled", "Tenant Custom Roles", "Allow tenants to create custom roles", "false", FlagValueType.Boolean, FlagCategory.System, true),
            FeatureFlag.Create("webhooks.enabled", "Webhooks Enabled", "Enable webhook integrations", "false", FlagValueType.Boolean, FlagCategory.System, true),
            FeatureFlag.Create("webhooks.max_count", "Max Webhooks", "Maximum webhook endpoints per tenant", "0", FlagValueType.Integer, FlagCategory.System, true),
            FeatureFlag.Create("imports.enabled", "Imports Enabled", "Enable data imports", "false", FlagValueType.Boolean, FlagCategory.System, false),
            FeatureFlag.Create("imports.max_rows", "Max Import Rows", "Maximum rows per import", "0", FlagValueType.Integer, FlagCategory.System, false),
            FeatureFlag.Create("exports.enabled", "Exports Enabled", "Enable data exports", "true", FlagValueType.Boolean, FlagCategory.System, true),
            FeatureFlag.Create("comments.activity_enabled", "Comments & Activity Enabled", "Enable comments and activity timeline on supported entities", "true", FlagValueType.Boolean, FlagCategory.System, true),
            FeatureFlag.Create("workflow.enabled", "Workflow Enabled", "Enable workflow and approvals module", "true", FlagValueType.Boolean, FlagCategory.System, true),
            // AI agent identity + cost enforcement (Plan 5d-1)
            FeatureFlag.Create("ai.cost.tenant_monthly_usd", "AI Tenant Monthly USD Cap", "Monthly USD ceiling for AI agent runs per tenant", "0", FlagValueType.Integer, FlagCategory.Ai, true),
            FeatureFlag.Create("ai.cost.tenant_daily_usd", "AI Tenant Daily USD Cap", "Daily USD ceiling for AI agent runs per tenant", "0", FlagValueType.Integer, FlagCategory.Ai, true),
            FeatureFlag.Create("ai.agents.max_count", "Max AI Agents", "Maximum number of AI agents per tenant", "0", FlagValueType.Integer, FlagCategory.Ai, true),
            FeatureFlag.Create("ai.agents.operational_enabled", "Operational AI Agents Enabled", "Allow event-/cron-triggered AI agents (no human caller)", "false", FlagValueType.Boolean, FlagCategory.Ai, true),
            FeatureFlag.Create("ai.agents.requests_per_minute_default", "Default Agent RPM", "Default per-agent requests-per-minute ceiling", "0", FlagValueType.Integer, FlagCategory.Ai, true),
        };

        context.Set<FeatureFlag>().AddRange(flags);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} feature flags", flags.Length);
    }
}
