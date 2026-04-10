using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Module.Webhooks.Infrastructure.Persistence;

namespace Starter.Module.Webhooks.Infrastructure.Services;

public sealed class WebhookDeliveryCleanupJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WebhookDeliveryCleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<WebhooksDbContext>();

                var retentionDays = configuration.GetValue("Webhooks:RetentionDays", 7);
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

                var deleted = await context.WebhookDeliveries
                    .IgnoreQueryFilters()
                    .Where(d => d.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                    logger.LogInformation("Cleaned up {Count} webhook deliveries older than {Days} days", deleted, retentionDays);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during webhook delivery cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
