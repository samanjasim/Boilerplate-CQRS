using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.ImportExport.Domain.Entities;

namespace Starter.Module.ImportExport.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext owned by the ImportExport module.
///
/// Uses the same physical database as <c>ApplicationDbContext</c> but maintains
/// its own migration history table (<c>__EFMigrationsHistory_ImportExport</c>)
/// so the module can evolve its schema independently.
///
/// The ImportExport module's <em>only owned entity</em> is <see cref="ImportJob"/>.
/// User/Role import/export providers operate on core entities and continue to
/// use <c>IApplicationDbContext</c> directly.
/// </summary>
public sealed class ImportExportDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public ImportExportDbContext(
        DbContextOptions<ImportExportDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // No MassTransit outbox tables here — see BillingDbContext for rationale.
        // All outbox bookkeeping lives on ApplicationDbContext.

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<ImportJob>().HasQueryFilter(j =>
            CurrentTenantId == null || j.TenantId == CurrentTenantId);
    }
}
