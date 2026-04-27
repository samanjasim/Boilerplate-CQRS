using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Persistence.Interceptors;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AuditLogAgentAttributionInterceptorTests
{
    private static ApplicationDbContext NewDb(SaveChangesInterceptor interceptor)
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
            .AddInterceptors(interceptor)
            .Options;
        return new ApplicationDbContext(opts, cu.Object);
    }

    private static AuditLog NewAuditLog() => new()
    {
        Id = Guid.NewGuid(),
        EntityType = AuditEntityType.User,
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Created,
        PerformedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Inside_AgentScope_Audit_Row_Is_Enriched()
    {
        var caller = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        await using var db = NewDb(new AuditLogAgentAttributionInterceptor());

        using (var scope = AgentExecutionScope.Begin(
                   userId: caller, agentPrincipalId: principalId,
                   tenantId: Guid.NewGuid(),
                   callerHasPermission: _ => true,
                   agentHasPermission: _ => true))
        {
            scope.AttachRunId(runId);
            db.AuditLogs.Add(NewAuditLog());
            await db.SaveChangesAsync();
        }

        var row = await db.AuditLogs.AsNoTracking().FirstAsync();
        row.AgentPrincipalId.Should().Be(principalId);
        row.OnBehalfOfUserId.Should().Be(caller);
        row.AgentRunId.Should().Be(runId);
    }

    [Fact]
    public async Task Outside_AgentScope_Audit_Row_Untouched()
    {
        await using var db = NewDb(new AuditLogAgentAttributionInterceptor());

        db.AuditLogs.Add(NewAuditLog());
        await db.SaveChangesAsync();

        var row = await db.AuditLogs.AsNoTracking().FirstAsync();
        row.AgentPrincipalId.Should().BeNull();
        row.OnBehalfOfUserId.Should().BeNull();
        row.AgentRunId.Should().BeNull();
    }
}
