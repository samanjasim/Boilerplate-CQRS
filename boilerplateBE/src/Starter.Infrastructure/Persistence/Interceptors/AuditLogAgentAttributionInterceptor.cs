using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChangesInterceptor that enriches newly-added <see cref="AuditLog"/> entries
/// with agent dual-attribution fields when an <see cref="AmbientExecutionContext"/> is
/// active (e.g., inside an agent run loop). Outside an agent run, the ambient is null and
/// this interceptor is a no-op — existing audit-write behaviour is preserved.
///
/// Plan 5d-1: chosen over modifying every inline `db.AuditLogs.Add(new AuditLog{...})`
/// call site (no central IAuditLogger exists in the codebase). Agent-aware enrichment
/// happens automatically wherever audit rows are created from inside an agent dispatch.
/// </summary>
public sealed class AuditLogAgentAttributionInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context?.ChangeTracker);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context?.ChangeTracker);
        return base.SavingChanges(eventData, result);
    }

    private static void Apply(ChangeTracker? tracker)
    {
        var ctx = AmbientExecutionContext.Current;
        if (ctx is null || tracker is null) return;

        foreach (var entry in tracker.Entries<AuditLog>())
        {
            if (entry.State != EntityState.Added) continue;
            entry.Entity.AgentPrincipalId ??= ctx.AgentPrincipalId;
            entry.Entity.OnBehalfOfUserId ??= ctx.UserId;
            entry.Entity.AgentRunId ??= ctx.AgentRunId;
        }
    }
}
