using System.Data;
using Starter.Domain.Common;
using Starter.Domain.Common.Access;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Core entities — always present
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
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<ResourceGrant> ResourceGrants { get; }

    // Generic accessor — modules and core services use this for all other entities
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);
}
