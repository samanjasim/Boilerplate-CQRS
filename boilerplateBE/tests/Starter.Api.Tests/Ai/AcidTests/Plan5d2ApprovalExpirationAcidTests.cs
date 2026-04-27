using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Api.Tests.Ai.Retrieval; // AiPostgresFixture
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 acid tests: verifies that <see cref="PendingApprovalService.ExpireDueAsync"/>
/// is atomic + concurrency-safe against a real Postgres backend (so the
/// <c>FOR UPDATE SKIP LOCKED</c> path is actually exercised). The fixture
/// auto-skips via Testcontainers when no local Postgres is available.
/// </summary>
public sealed class Plan5d2ApprovalExpirationAcidTests : IClassFixture<AiPostgresFixture>
{
    private readonly AiPostgresFixture _fx;

    public Plan5d2ApprovalExpirationAcidTests(AiPostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Atomic_Update_Marks_Expired_And_Skips_Already_Decided()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var principal = Guid.NewGuid();

        Guid t1Id, t2Id, t3Id;

        // Seed three rows: one expired+pending, one expired+approved, one not-yet-expired.
        await using (var db = _fx.CreateDbContext())
        {
            var t1 = AiPendingApproval.Create(tenant, assistant, "Tutor", principal,
                Guid.NewGuid(), null, Guid.NewGuid(),
                "T1", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(-1));
            var t2 = AiPendingApproval.Create(tenant, assistant, "Tutor", principal,
                Guid.NewGuid(), null, Guid.NewGuid(),
                "T2", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(-1));
            var t3 = AiPendingApproval.Create(tenant, assistant, "Tutor", principal,
                Guid.NewGuid(), null, Guid.NewGuid(),
                "T3", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(60));

            db.AiPendingApprovals.AddRange(t1, t2, t3);

            // Approve T2 — its status flips to Approved so ExpireDueAsync must skip it.
            t2.TryApprove(Guid.NewGuid(), null);

            await db.SaveChangesAsync();
            t1Id = t1.Id;
            t2Id = t2.Id;
            t3Id = t3.Id;
        }

        // Run expire under a fresh scope.
        await using (var db = _fx.CreateDbContext())
        {
            var svc = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
            var n = await svc.ExpireDueAsync(100, default);
            n.Should().Be(1, "only T1 (expired + pending) should be flipped");
        }

        // Verify state.
        await using (var db = _fx.CreateDbContext())
        {
            var byTool = await db.AiPendingApprovals
                .AsNoTracking()
                .Where(p => p.AssistantId == assistant)
                .ToDictionaryAsync(p => p.ToolName);
            byTool["T1"].Status.Should().Be(PendingApprovalStatus.Expired);
            byTool["T2"].Status.Should().Be(PendingApprovalStatus.Approved);
            byTool["T3"].Status.Should().Be(PendingApprovalStatus.Pending);
        }
    }

    [Fact]
    public async Task Concurrent_Expiration_Calls_Are_Safe()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var principal = Guid.NewGuid();

        await using (var db = _fx.CreateDbContext())
        {
            for (var i = 0; i < 20; i++)
                db.AiPendingApprovals.Add(AiPendingApproval.Create(
                    tenant, assistant, "Tutor", principal,
                    Guid.NewGuid(), null, Guid.NewGuid(),
                    $"T{i}", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();
        }

        // Run two expirations concurrently — total expired must equal 20, no row twice.
        async Task<int> RunOne()
        {
            await using var db = _fx.CreateDbContext();
            var svc = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
            return await svc.ExpireDueAsync(100, default);
        }

        var results = await Task.WhenAll(RunOne(), RunOne());
        (results[0] + results[1]).Should().Be(20, "FOR UPDATE SKIP LOCKED must partition the rows across the two callers");

        await using (var db = _fx.CreateDbContext())
        {
            var pending = await db.AiPendingApprovals
                .AsNoTracking()
                .CountAsync(p => p.AssistantId == assistant && p.Status == PendingApprovalStatus.Pending);
            pending.Should().Be(0);
        }
    }
}
