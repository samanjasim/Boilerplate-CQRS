using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Communication;

public sealed class GetDeliveryStatusCountsQueryHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly CommunicationDbContext _db;
    private readonly FakeTimeProvider _clock = new(Now);

    public GetDeliveryStatusCountsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<CommunicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CommunicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_BucketsByStatusWithinWindow()
    {
        var tenantId = Guid.NewGuid();
        var withinWindow = Now.AddDays(-3).UtcDateTime;
        var outsideWindow = Now.AddDays(-30).UtcDateTime;

        _db.DeliveryLogs.AddRange(
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, tenantId, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, tenantId, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Failed,    tenantId, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Pending,   tenantId, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Queued,    tenantId, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Sending,   tenantId, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Bounced,   tenantId, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, tenantId, createdAt: outsideWindow));
        await _db.SaveChangesAsync();

        var sut = new GetDeliveryStatusCountsQueryHandler(_db, _clock);
        var result = await sut.Handle(new GetDeliveryStatusCountsQuery(WindowDays: 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delivered.Should().Be(2);
        result.Value.Failed.Should().Be(1);
        result.Value.Pending.Should().Be(3); // Pending + Queued + Sending
        result.Value.Bounced.Should().Be(1);
        result.Value.WindowDays.Should().Be(7);
    }

    [Theory]
    [InlineData(0, 1)]    // Below floor → clamp to 1
    [InlineData(91, 90)]  // Above ceiling → clamp to 90
    [InlineData(7, 7)]    // Pass through
    public async Task Handle_ClampsWindowDaysToValidRange(int requested, int expected)
    {
        var sut = new GetDeliveryStatusCountsQueryHandler(_db, _clock);
        var result = await sut.Handle(new GetDeliveryStatusCountsQuery(WindowDays: requested), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WindowDays.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_RowAtExactWindowBoundaryIncluded()
    {
        // Row created exactly 7 days ago should still count when WindowDays = 7.
        var tenantId = Guid.NewGuid();
        _db.DeliveryLogs.Add(
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, tenantId, createdAt: Now.AddDays(-7).UtcDateTime));
        await _db.SaveChangesAsync();

        var sut = new GetDeliveryStatusCountsQueryHandler(_db, _clock);
        var result = await sut.Handle(new GetDeliveryStatusCountsQuery(WindowDays: 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delivered.Should().Be(1);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal static class DeliveryLogTestFactory
{
    public static DeliveryLog WithStatus(DeliveryStatus status, Guid tenantId, DateTime createdAt)
    {
        var log = DeliveryLog.Create(
            tenantId: tenantId,
            recipientUserId: null,
            recipientAddress: "test@example.com",
            messageTemplateId: null,
            templateName: "test.template",
            channel: NotificationChannel.Email,
            integrationType: null,
            subject: null,
            bodyPreview: null,
            variablesJson: null);

        // Adjust CreatedAt to the requested value for time-window testing
        SetCreatedAt(log, createdAt);

        // Apply the requested status (Create() always starts as Pending)
        ApplyStatus(log, status);

        return log;
    }

    private static void SetCreatedAt(DeliveryLog log, DateTime createdAt)
    {
        // BaseEntity.CreatedAt has a private/init setter — use reflection to override it
        var prop = typeof(DeliveryLog).BaseType?.GetProperty("CreatedAt")
            ?? typeof(DeliveryLog).GetProperty("CreatedAt");
        if (prop is not null && prop.CanWrite)
        {
            prop.SetValue(log, createdAt);
            return;
        }
        // Try backing field
        var field = log.GetType().BaseType?.GetField("<CreatedAt>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? log.GetType().GetField("<CreatedAt>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(log, createdAt);
    }

    private static void ApplyStatus(DeliveryLog log, DeliveryStatus status)
    {
        switch (status)
        {
            case DeliveryStatus.Queued:   log.MarkQueued(); break;
            case DeliveryStatus.Sending:  log.MarkSending(ChannelProvider.Smtp); break;
            case DeliveryStatus.Delivered: log.MarkDelivered(null, 100); break;
            case DeliveryStatus.Failed:   log.MarkFailed(null); break;
            case DeliveryStatus.Bounced:  log.MarkBounced(null); break;
            // Pending is the default from Create()
        }
    }
}
