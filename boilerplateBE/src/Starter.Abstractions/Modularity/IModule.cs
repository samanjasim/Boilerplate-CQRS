using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Abstractions.Modularity;

public interface IModule
{
    string Name { get; }
    string DisplayName { get; }
    string Version { get; }
    IReadOnlyList<string> Dependencies { get; }
    IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration);
    IEnumerable<(string Name, string Description, string Module)> GetPermissions();
    IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions();

    /// <summary>
    /// Apply the module's EF Core migrations. Modules with their own DbContext
    /// override this to call <c>scope.GetRequiredService&lt;TDbContext&gt;().Database.MigrateAsync(ct)</c>.
    /// Modules without persistence (or that rely on <c>ApplicationDbContext</c>) leave the default no-op.
    /// </summary>
    Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Seed initial data for this module. Called once at application startup after
    /// <see cref="MigrateAsync"/> completes. Modules without seed data leave the default no-op.
    /// </summary>
    Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
