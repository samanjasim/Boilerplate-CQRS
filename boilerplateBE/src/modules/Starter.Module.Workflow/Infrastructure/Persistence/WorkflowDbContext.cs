using System.Reflection;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // System templates (TenantId=null) are visible to ALL tenants
        modelBuilder.Entity<WorkflowDefinition>().HasQueryFilter(d =>
            CurrentTenantId == null || d.TenantId == null || d.TenantId == CurrentTenantId);

        modelBuilder.Entity<WorkflowInstance>().HasQueryFilter(i =>
            CurrentTenantId == null || i.TenantId == CurrentTenantId);

        modelBuilder.Entity<ApprovalTask>().HasQueryFilter(t =>
            CurrentTenantId == null || t.TenantId == CurrentTenantId);
    }
}
