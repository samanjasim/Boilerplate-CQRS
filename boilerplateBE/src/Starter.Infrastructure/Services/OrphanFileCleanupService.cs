using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Application.Common.Constants;
using Starter.Domain.Common.Enums;

namespace Starter.Infrastructure.Services;

public sealed class OrphanFileCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrphanFileCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delayMinutes = 30;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();
                delayMinutes = await settingsProvider.GetIntAsync(FileSettings.OrphanCleanupIntervalMinutesKey, FileSettings.OrphanCleanupIntervalMinutesDefault, stoppingToken);

                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

                var expiredFiles = await context.Set<FileMetadata>()
                    .IgnoreQueryFilters()
                    .Where(f => f.Status == FileStatus.Temporary && f.ExpiresAt != null && f.ExpiresAt < DateTime.UtcNow)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var file in expiredFiles)
                {
                    try
                    {
                        await storageService.DeleteAsync(file.StorageKey, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to delete storage for orphan file {FileId}", file.Id);
                    }

                    context.Set<FileMetadata>().Remove(file);
                }

                if (expiredFiles.Count > 0)
                {
                    await context.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Cleaned up {Count} orphan temporary files", expiredFiles.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during orphan file cleanup");
            }

            await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
        }
    }
}
