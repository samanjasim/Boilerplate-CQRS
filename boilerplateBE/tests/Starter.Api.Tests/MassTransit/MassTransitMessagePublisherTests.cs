using FluentAssertions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Unit tests for <see cref="MassTransitMessagePublisher"/> — the
/// <see cref="IMessagePublisher"/> implementation routes through
/// <see cref="IIntegrationEventCollector"/> instead of <c>IPublishEndpoint</c>
/// to avoid the dual-outbox silent-drop bug.
///
/// <para>
/// Every existing caller of <c>IMessagePublisher.PublishAsync</c> now flows
/// through the outbox transparently — no caller had to change. These tests
/// pin that contract.
/// </para>
/// </summary>
public sealed class MassTransitMessagePublisherTests
{
    private sealed record SampleMessage(Guid Id, string Name);

    [Fact]
    public async Task PublishAsync_Schedules_The_Message_On_The_Collector()
    {
        var collector = new Mock<IIntegrationEventCollector>();
        var publisher = new MassTransitMessagePublisher(collector.Object);

        var msg = new SampleMessage(Guid.NewGuid(), "test");

        await publisher.PublishAsync(msg);

        collector.Verify(
            c => c.Schedule(It.Is<SampleMessage>(m => m == msg)),
            Times.Once,
            "the publisher must hand the message to the collector unmodified — " +
            "the interceptor flushes it during ApplicationDbContext.SaveChangesAsync");
    }

    [Fact]
    public async Task PublishAsync_DoesNotInteractWith_Anything_Else()
    {
        // Strict mock with no setup beyond Schedule — proves the publisher
        // doesn't touch any other API on the collector.
        var collector = new Mock<IIntegrationEventCollector>(MockBehavior.Strict);
        collector.Setup(c => c.Schedule(It.IsAny<SampleMessage>()));

        var publisher = new MassTransitMessagePublisher(collector.Object);

        var act = async () => await publisher.PublishAsync(new SampleMessage(Guid.NewGuid(), "x"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_PreservesGenericTypeInfo_OnSchedule()
    {
        // The collector's Schedule<T> signature carries TypeOf<T> for downstream
        // routing. Verify the publisher's generic invocation reaches Schedule
        // with the *concrete* T (not 'object').
        var collector = new Mock<IIntegrationEventCollector>();
        var publisher = new MassTransitMessagePublisher(collector.Object);

        await publisher.PublishAsync(new SampleMessage(Guid.NewGuid(), "abc"));

        // We can't observe the generic argument directly via Moq, but we can
        // verify the invocation matched the exact runtime type by setting up
        // a strongly-typed match.
        collector.Verify(
            c => c.Schedule<SampleMessage>(It.IsAny<SampleMessage>()),
            Times.Once);
    }
}
