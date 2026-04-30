using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.Products.Constants;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Module.Products.Infrastructure.Services;
using Starter.Module.Products.Infrastructure.Tenancy;

namespace Starter.Module.Products;

public sealed class ProductsModule : IModule, IModuleBusContributor
{
    public string Name => "Starter.Module.Products";
    public string DisplayName => "Products";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProductsDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

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

        services.AddScoped<ProductTenantResolver>();
        services.AddCommentableEntity("Product", builder =>
        {
            builder.CustomActivityTypes = ["PriceChanged", "Published", "Archived"];
            builder.UseTenantResolver<ProductTenantResolver>();
        });

        services.AddWorkflowableEntity("Product", builder =>
        {
            builder.DefaultDefinitionName = "general-approval";
        });

        services.AddAiToolsFromAssembly(typeof(ProductsModule).Assembly);
        services.AddAiAgentTemplatesFromAssembly(typeof(ProductsModule).Assembly);

        return services;
    }

    public void ConfigureBus(IBusRegistrationConfigurator bus)
    {
        // Modules own their own bus surface — the host no longer auto-discovers
        // consumers from module assemblies (Tier 2.5 Theme 5 Phase D).
        bus.AddConsumers(typeof(ProductsModule).Assembly);
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
