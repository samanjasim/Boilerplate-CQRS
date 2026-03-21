using Starter.Domain.Common;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Session> Sessions { get; }
    DbSet<LoginHistory> LoginHistory { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
