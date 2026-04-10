using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.Webhooks.Domain.Entities;

namespace Starter.Module.Webhooks.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext owned by the Webhooks module.
///
/// Uses the same physical database as <c>ApplicationDbContext</c> but maintains
/// its own migration history table (<c>__EFMigrationsHistory_Webhooks</c>) so the
/// module can evolve its schema independently. Uninstalling the Webhooks module
/// removes this context entirely — its tables can be dropped without touching
/// core or other modules.
///
/// Cross-module data access is forbidden by design: this context only knows
/// about Webhooks entities. To read tenant or user data, inject
/// <c>ITenantReader</c> / <c>IUserReader</c> instead of joining across contexts.
/// </summary>
public sealed class WebhooksDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public WebhooksDbContext(
        DbContextOptions<WebhooksDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // No MassTransit outbox tables here — see BillingDbContext for rationale.
        // All outbox bookkeeping lives on ApplicationDbContext.

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filters — both webhook entities are tenant-scoped
        modelBuilder.Entity<WebhookEndpoint>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);

        modelBuilder.Entity<WebhookDelivery>().HasQueryFilter(d =>
            CurrentTenantId == null || d.TenantId == CurrentTenantId);
    }
}
