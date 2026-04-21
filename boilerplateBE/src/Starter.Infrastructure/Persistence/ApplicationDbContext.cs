using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Tenants.Entities;
using MassTransit;
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
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

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

        // MassTransit transactional outbox tables: InboxState, OutboxMessage,
        // OutboxState. Required for AddEntityFrameworkOutbox<ApplicationDbContext>().
        //
        // The app now has TWO outboxes: this core outbox on ApplicationDbContext
        // (used by Billing, Webhooks, Import/Export, and all other modules), and
        // a dedicated outbox on WorkflowDbContext (see WorkflowMassTransitExtensions).
        // Workflow owns its own SaveChanges scope, so its events must be enqueued
        // in the same transaction as the workflow state changes — a shared outbox
        // can't span two DbContexts. Non-workflow modules should continue to
        // publish through this core outbox rather than registering their own.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // Core entity configurations only. Each module owns its own DbContext
        // and applies its own configurations there — ApplicationDbContext no
        // longer scans module assemblies.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filters
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        // ── Explicit filters for entities with non-standard logic ──

        // Non-nullable TenantId entities (can't use ITenantEntity convention)
        modelBuilder.Entity<TenantFeatureFlag>().HasQueryFilter(t =>
            TenantId == null || t.TenantId == TenantId);

        // Non-standard pattern: tenant users see global (TenantId=null) + their own
        modelBuilder.Entity<Role>().HasQueryFilter(r =>
            TenantId == null || r.TenantId == null || r.TenantId == TenantId);

        modelBuilder.Entity<Invitation>().HasQueryFilter(i =>
            TenantId == null || i.TenantId == null || i.TenantId == TenantId);

        modelBuilder.Entity<SystemSetting>().HasQueryFilter(s =>
            TenantId == null || s.TenantId == null || s.TenantId == TenantId);

        // Special: uses Id not TenantId
        modelBuilder.Entity<Tenant>().HasQueryFilter(t =>
            TenantId == null || t.Id == TenantId);

        // ── Convention-based filters for module entities ──
        // Module entities implementing ITenantEntity get the standard filter automatically.
        // Skips entities that already have a filter defined above.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Skip entities that already have an explicit filter
            if (entityType.GetDeclaredQueryFilters().Any())
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var currentTenantId = Expression.Property(
                Expression.Constant(this),
                typeof(ApplicationDbContext).GetProperty(nameof(TenantId),
                    BindingFlags.NonPublic | BindingFlags.Instance)!);

            var filter = Expression.Lambda(
                Expression.OrElse(
                    Expression.Equal(currentTenantId,
                        Expression.Constant(null, typeof(Guid?))),
                    Expression.Equal(tenantIdProp, currentTenantId)),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await Database.BeginTransactionAsync(isolationLevel, ct);
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }
}
