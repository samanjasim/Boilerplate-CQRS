using System.Reflection;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;

namespace Starter.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<FileMetadata> FileMetadata => Set<FileMetadata>();

    // EF Core evaluates this per-query via the expression tree.
    // Must be a property (not a method) for EF to parameterize it.
    private Guid? TenantId => _currentUserService?.TenantId;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global tenant query filters — EF parameterizes TenantId per query execution
        // Platform admin (TenantId=null): sees everything
        // Tenant user (TenantId=guid): sees ONLY their tenant's data (not platform users)

        modelBuilder.Entity<User>().HasQueryFilter(u =>
            TenantId == null || u.TenantId == TenantId);

        // Roles: tenant users see global/system roles (TenantId=null) + their own custom roles
        modelBuilder.Entity<Role>().HasQueryFilter(r =>
            TenantId == null || r.TenantId == null || r.TenantId == TenantId);

        modelBuilder.Entity<AuditLog>().HasQueryFilter(a =>
            TenantId == null || a.TenantId == TenantId);

        modelBuilder.Entity<Invitation>().HasQueryFilter(i =>
            TenantId == null || i.TenantId == TenantId);

        modelBuilder.Entity<Notification>().HasQueryFilter(n =>
            TenantId == null || n.TenantId == TenantId);

        modelBuilder.Entity<FileMetadata>().HasQueryFilter(f =>
            TenantId == null || f.TenantId == TenantId);

        // Tenant entity: tenant users see only their own tenant; platform admins see all
        modelBuilder.Entity<Tenant>().HasQueryFilter(t =>
            TenantId == null || t.Id == TenantId);
    }
}
