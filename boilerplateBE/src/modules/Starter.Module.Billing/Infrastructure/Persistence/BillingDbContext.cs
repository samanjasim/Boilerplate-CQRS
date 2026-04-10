using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.Billing.Domain.Entities;

namespace Starter.Module.Billing.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext owned by the Billing module.
///
/// Uses the same physical database as <c>ApplicationDbContext</c> but maintains
/// its own migration history table (<c>__EFMigrationsHistory_Billing</c>) so the
/// module can evolve its schema independently. Uninstalling the Billing module
/// removes this context entirely — its tables can be dropped without touching
/// core or other modules.
///
/// Cross-module data access is forbidden by design: this context only knows
/// about Billing entities. To read tenant or user data, inject
/// <c>ITenantReader</c> / <c>IUserReader</c> instead of joining across contexts.
///
/// Multi-tenancy: tenant filtering is applied to entities via <c>HasQueryFilter</c>.
/// Platform admins (<c>TenantId == null</c>) see all rows; tenant users see only
/// their own.
/// </summary>
public sealed class BillingDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    // Reference property — must be a property (not a method) for EF to parameterize.
    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public BillingDbContext(
        DbContextOptions<BillingDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<PlanPriceHistory> PlanPriceHistories => Set<PlanPriceHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // No MassTransit outbox tables here. All domain events (including those
        // raised from this context via IPublishEndpoint) flow through the single
        // outbox registered against ApplicationDbContext in
        // Starter.Infrastructure/DependencyInjection.cs. Consolidating the outbox
        // in one place keeps retry/dedup bookkeeping simple and avoids creating
        // dead __MT_* tables per module.

        // Apply Billing entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filters — only TenantSubscription/PaymentRecord are tenant-scoped.
        // SubscriptionPlan and PlanPriceHistory are platform-global (shared catalog).
        modelBuilder.Entity<TenantSubscription>().HasQueryFilter(s =>
            CurrentTenantId == null || s.TenantId == CurrentTenantId);

        modelBuilder.Entity<PaymentRecord>().HasQueryFilter(p =>
            CurrentTenantId == null || p.TenantId == CurrentTenantId);
    }
}
