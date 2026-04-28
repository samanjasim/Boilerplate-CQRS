using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Approvals;

namespace Starter.Module.AI.Infrastructure.Background;

/// <summary>
/// Multi-replica-safe expiration job. Uses Postgres FOR UPDATE SKIP LOCKED inside
/// IPendingApprovalService.ExpireDueAsync so concurrent ticks across replicas claim
/// disjoint rows. Bounded batch keeps each transaction sub-second; AgentApprovalExpiredEvent
/// is raised per row inside the SaveChanges, so notifications + webhook fan-out happen
/// in the same transactional unit.
/// </summary>
internal sealed class AiPendingApprovalExpirationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<AiPendingApprovalExpirationJob> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var svc = scope.ServiceProvider.GetRequiredService<IPendingApprovalService>();
                var n = await svc.ExpireDueAsync(BatchSize, stoppingToken);
                if (n > 0)
                    logger.LogInformation("AiPendingApprovalExpirationJob expired {Count} approvals.", n);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AiPendingApprovalExpirationJob iteration failed; retrying after interval.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (TaskCanceledException) { return; }
        }
    }
}
