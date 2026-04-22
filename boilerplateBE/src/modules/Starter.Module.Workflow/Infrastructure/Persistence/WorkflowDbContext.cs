using System.Reflection;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Domain.Entities;

namespace Starter.Module.Workflow.Infrastructure.Persistence;

/// <summary>
/// Module-owned DbContext for the Workflow module. Uses a separate
/// migration history table (<c>__EFMigrationsHistory_Workflow</c>) so the
/// module can be added or removed without touching core migrations.
/// </summary>
public sealed class WorkflowDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public WorkflowDbContext(
        DbContextOptions<WorkflowDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();
    public DbSet<DelegationRule> DelegationRules => Set<DelegationRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // MassTransit transactional outbox tables (OutboxMessage, OutboxState, InboxState).
        // Bound to WorkflowDbContext so workflow integration events publish atomically
        // with workflow state changes. See Phase 2b spec.
        //
        // The tables are physically owned by ApplicationDbContext's migration. We register
        // the entity types here only so WorkflowDbContext.SaveChanges can enqueue outbox
        // messages in the same transaction as workflow state changes, and exclude them
        // from this context's migrations so the shared tables aren't created twice.
        modelBuilder.AddTransactionalOutboxEntities();
        modelBuilder.Entity<InboxState>().ToTable("InboxState", t => t.ExcludeFromMigrations());
        modelBuilder.Entity<OutboxState>().ToTable("OutboxState", t => t.ExcludeFromMigrations());
        modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessage", t => t.ExcludeFromMigrations());

        // System templates (TenantId=null) are visible to ALL tenants
        modelBuilder.Entity<WorkflowDefinition>().HasQueryFilter(d =>
            CurrentTenantId == null || d.TenantId == null || d.TenantId == CurrentTenantId);

        modelBuilder.Entity<WorkflowInstance>().HasQueryFilter(i =>
            CurrentTenantId == null || i.TenantId == CurrentTenantId);

        modelBuilder.Entity<ApprovalTask>().HasQueryFilter(t =>
            CurrentTenantId == null || t.TenantId == CurrentTenantId);

        modelBuilder.Entity<DelegationRule>().HasQueryFilter(r =>
            CurrentTenantId == null || r.TenantId == CurrentTenantId);
    }
}
