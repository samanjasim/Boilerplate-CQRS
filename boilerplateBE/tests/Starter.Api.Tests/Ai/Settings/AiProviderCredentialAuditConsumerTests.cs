using System.Text.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Infrastructure.Persistence;
using Starter.Module.AI.Application.Events;
using Starter.Module.AI.Infrastructure.Consumers;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiProviderCredentialAuditConsumerTests
{
    [Fact]
    public async Task CreatedEvent_Writes_Created_AuditLog_Without_Secret()
    {
        var (consumer, appDb) = BuildConsumer();
        var evt = NewEvent<AiProviderCredentialCreatedEvent>();

        await consumer.Consume(MockContext(evt));

        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAuditMatches(audit, evt, "AiProviderCredential.Created", AuditAction.Created);
    }

    [Fact]
    public async Task RotatedEvent_Writes_Updated_AuditLog()
    {
        var (consumer, appDb) = BuildConsumer();
        var evt = NewEvent<AiProviderCredentialRotatedEvent>();

        await consumer.Consume(MockContext(evt));

        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAuditMatches(audit, evt, "AiProviderCredential.Rotated", AuditAction.Updated);
    }

    [Fact]
    public async Task RevokedEvent_Writes_Deleted_AuditLog()
    {
        var (consumer, appDb) = BuildConsumer();
        var evt = NewEvent<AiProviderCredentialRevokedEvent>();

        await consumer.Consume(MockContext(evt));

        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAuditMatches(audit, evt, "AiProviderCredential.Revoked", AuditAction.Deleted);
    }

    [Fact]
    public async Task TestedEvent_Writes_Updated_AuditLog()
    {
        var (consumer, appDb) = BuildConsumer();
        var evt = NewEvent<AiProviderCredentialTestedEvent>();

        await consumer.Consume(MockContext(evt));

        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAuditMatches(audit, evt, "AiProviderCredential.Tested", AuditAction.Updated);
    }

    [Fact]
    public async Task Redelivery_Skips_Duplicate_Audit_Row()
    {
        var (consumer, appDb) = BuildConsumer();
        var evt = NewEvent<AiProviderCredentialCreatedEvent>();

        // At-least-once delivery: same event arrives twice. Idempotency guard must
        // collapse to a single audit row.
        await consumer.Consume(MockContext(evt));
        await consumer.Consume(MockContext(evt));

        var rows = await appDb.AuditLogs.IgnoreQueryFilters().CountAsync();
        rows.Should().Be(1);
    }

    private static (AiProviderCredentialAuditConsumer Consumer, ApplicationDbContext AppDb) BuildConsumer()
    {
        var tenantId = Guid.NewGuid();
        var appDb = CreateAppDb(tenantId);
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(appDb);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return (new AiProviderCredentialAuditConsumer(scopeFactory), appDb);
    }

    private static T NewEvent<T>() where T : class
    {
        var args = new object?[]
        {
            /* TenantId */ Guid.NewGuid(),
            /* CredentialId */ Guid.NewGuid(),
            /* Provider */ AiProviderType.OpenAI,
            /* KeyPrefix */ "sk-live-12345",
            /* PerformedBy */ (Guid?)Guid.NewGuid(),
            /* PerformedByEmail */ "admin@example.test",
            /* OccurredAt */ DateTime.UtcNow
        };
        return (T)Activator.CreateInstance(typeof(T), args)!;
    }

    private static ConsumeContext<T> MockContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static void AssertAuditMatches(AuditLog audit, dynamic evt, string actionCode, AuditAction action)
    {
        audit.EntityType.Should().Be(AuditEntityType.AiProviderCredential);
        audit.EntityId.Should().Be((Guid)evt.CredentialId);
        audit.Action.Should().Be(action);
        audit.TenantId.Should().Be((Guid?)evt.TenantId);
        audit.PerformedBy.Should().Be((Guid?)evt.PerformedBy);
        audit.PerformedAt.Should().Be((DateTime)evt.OccurredAt);
        audit.Changes.Should().NotBeNullOrWhiteSpace();
        audit.Changes!.Should().Contain(actionCode);
        audit.Changes.Should().Contain("\"Provider\"");
        audit.Changes.Should().Contain("\"KeyPrefix\"");
        // Plaintext / encrypted secret never appears in the audit detail.
        audit.Changes.Should().NotContain("protected-");
        audit.Changes.Should().NotContain("plaintext");
    }

    private static ApplicationDbContext CreateAppDb(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-credential-audit-consumer-{Guid.NewGuid()}")
            .Options;
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        return new ApplicationDbContext(options, currentUser.Object);
    }
}
