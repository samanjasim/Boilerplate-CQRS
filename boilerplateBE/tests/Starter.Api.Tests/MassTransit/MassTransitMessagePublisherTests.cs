using FluentAssertions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Unit tests for <see cref="MassTransitMessagePublisher"/> — schedules messages
/// on <see cref="IIntegrationEventCollector"/> instead of using
/// <c>IPublishEndpoint</c> directly. Routes through the same outbox pipeline
/// as <see cref="IIntegrationEventCollector"/> users for consistency.
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
            "the publisher must hand the message to the collector unmodified");
    }

    [Fact]
    public async Task PublishAsync_PreservesGenericTypeInfo()
    {
        // Verify the publisher's generic invocation reaches Schedule with the
        // concrete T (not 'object'), so the interceptor publishes with the
        // correct MT message type and consumers route correctly.
        var collector = new Mock<IIntegrationEventCollector>();
        var publisher = new MassTransitMessagePublisher(collector.Object);

        await publisher.PublishAsync(new SampleMessage(Guid.NewGuid(), "abc"));

        collector.Verify(
            c => c.Schedule<SampleMessage>(It.IsAny<SampleMessage>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_DoesNotInteract_With_Anything_Else()
    {
        var collector = new Mock<IIntegrationEventCollector>(MockBehavior.Strict);
        collector.Setup(c => c.Schedule(It.IsAny<SampleMessage>()));
        var publisher = new MassTransitMessagePublisher(collector.Object);

        var act = async () => await publisher.PublishAsync(new SampleMessage(Guid.NewGuid(), "x"));

        await act.Should().NotThrowAsync(
            "the publisher should only call Schedule on the collector — nothing else");
    }
}
