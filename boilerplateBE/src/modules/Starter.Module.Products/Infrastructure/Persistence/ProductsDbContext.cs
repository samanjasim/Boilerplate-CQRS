using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.Products.Domain.Entities;

namespace Starter.Module.Products.Infrastructure.Persistence;

/// <summary>
/// Module-owned DbContext for the Products module. Uses a separate
/// migration history table (<c>__EFMigrationsHistory_Products</c>) so the
/// module can be added or removed without touching core migrations.
/// </summary>
public sealed class ProductsDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public ProductsDbContext(
        DbContextOptions<ProductsDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Product>().HasQueryFilter(p =>
            CurrentTenantId == null || p.TenantId == CurrentTenantId);
    }
}
