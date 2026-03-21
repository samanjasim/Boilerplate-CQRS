using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.ValueObjects;
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
            await SeedSuperAdminUserAsync(context, configuration, logger);
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
}
