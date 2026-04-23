using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Infrastructure.Persistence;

namespace Starter.Module.Communication.Infrastructure.Services;

internal sealed class DeliveryLogCleanupJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DeliveryLogCleanupJob> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay initial run to let the app finish startup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeOldLogsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during delivery log cleanup");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task PurgeOldLogsAsync(CancellationToken cancellationToken)
    {
        var retentionDays = configuration.GetValue("Communication:DeliveryLogRetentionDays", 90);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

        // Delete attempts for old delivery logs (cross-tenant)
        var attemptsPurged = await context.DeliveryAttempts
            .IgnoreQueryFilters()
            .Where(a => context.DeliveryLogs
                .IgnoreQueryFilters()
                .Where(d => d.CreatedAt < cutoff)
                .Select(d => d.Id)
                .Contains(a.DeliveryLogId))
            .ExecuteDeleteAsync(cancellationToken);

        // Delete old delivery logs (cross-tenant)
        var logsPurged = await context.DeliveryLogs
            .IgnoreQueryFilters()
            .Where(d => d.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (logsPurged > 0 || attemptsPurged > 0)
        {
            logger.LogInformation(
                "Delivery log cleanup: purged {LogCount} logs and {AttemptCount} attempts older than {RetentionDays} days",
                logsPurged, attemptsPurged, retentionDays);
        }
    }
}
