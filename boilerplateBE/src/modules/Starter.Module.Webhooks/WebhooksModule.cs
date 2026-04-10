using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.Webhooks.Constants;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Module.Webhooks.Infrastructure.Services;

namespace Starter.Module.Webhooks;

public sealed class WebhooksModule : IModule
{
    public string Name => "Starter.Module.Webhooks";
    public string DisplayName => "Webhook Management";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Module-owned DbContext with isolated migration history table
        services.AddDbContext<WebhooksDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Webhooks");
                    npgsqlOptions.MigrationsAssembly(typeof(WebhooksDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddHttpClient(); // Required for DeliverWebhookConsumer
        services.AddScoped<IWebhookPublisher, WebhookPublisher>();
        services.AddScoped<IUsageMetricCalculator, WebhookUsageMetricCalculator>();
        services.AddHostedService<WebhookDeliveryCleanupJob>();

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (WebhookPermissions.View, "View webhook endpoints and deliveries", "Webhooks");
        yield return (WebhookPermissions.Create, "Create webhook endpoints", "Webhooks");
        yield return (WebhookPermissions.Update, "Update webhook endpoints", "Webhooks");
        yield return (WebhookPermissions.Delete, "Delete webhook endpoints", "Webhooks");
        yield return (WebhookPermissions.ViewPlatform, "View all webhook endpoints across tenants", "Webhooks");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            WebhookPermissions.View, WebhookPermissions.Create,
            WebhookPermissions.Update, WebhookPermissions.Delete,
            WebhookPermissions.ViewPlatform
        ]);
        yield return ("Admin", [
            WebhookPermissions.View, WebhookPermissions.Create,
            WebhookPermissions.Update, WebhookPermissions.Delete
        ]);
        yield return ("User", [
            WebhookPermissions.View
        ]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WebhooksDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

}
