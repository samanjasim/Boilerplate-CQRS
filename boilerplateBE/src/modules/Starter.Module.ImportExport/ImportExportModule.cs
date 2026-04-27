using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.ImportExport.Application.Definitions;
using Starter.Module.ImportExport.Constants;
using Starter.Module.ImportExport.Infrastructure.Persistence;
using Starter.Module.ImportExport.Infrastructure.Services;

namespace Starter.Module.ImportExport;

public sealed class ImportExportModule : IModule
{
    public string Name => "Starter.Module.ImportExport";
    public string DisplayName => "Import/Export Management";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Module-owned DbContext with isolated migration history table
        services.AddDbContext<ImportExportDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_ImportExport");
                    npgsqlOptions.MigrationsAssembly(typeof(ImportExportDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddSingleton<IImportExportRegistry>(sp =>
        {
            var registry = new ImportExportRegistry();
            registry.Register(UserImportExportDefinition.Create());
            registry.Register(RoleImportExportDefinition.Create());
            return registry;
        });

        services.AddScoped<UserExportDataProvider>();
        services.AddScoped<UserImportRowProcessor>();
        services.AddScoped<RoleExportDataProvider>();
        services.AddScoped<RoleImportRowProcessor>();

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (ImportExportPermissions.ImportData, "Import data from CSV files", "ImportExport");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [ImportExportPermissions.ImportData]);
        yield return ("Admin", [ImportExportPermissions.ImportData]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ImportExportDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

}
