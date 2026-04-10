using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.Products.Constants;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Module.Products.Infrastructure.Services;

namespace Starter.Module.Products;

public sealed class ProductsModule : IModule
{
    public string Name => "Starter.Module.Products";
    public string DisplayName => "Products";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProductsDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Products");
                    npgsqlOptions.MigrationsAssembly(typeof(ProductsDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddScoped<IUsageMetricCalculator, ProductsUsageMetricCalculator>();

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (ProductPermissions.View, "View products", "Products");
        yield return (ProductPermissions.Create, "Create products", "Products");
        yield return (ProductPermissions.Update, "Update products", "Products");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            ProductPermissions.View, ProductPermissions.Create,
            ProductPermissions.Update]);
        yield return ("Admin", [
            ProductPermissions.View, ProductPermissions.Create,
            ProductPermissions.Update]);
        yield return ("User", [ProductPermissions.View]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
