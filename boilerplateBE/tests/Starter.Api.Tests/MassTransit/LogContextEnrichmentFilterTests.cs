using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Starter.Application.Common.Events;
using Starter.Application.Common.Models;
using Starter.Infrastructure.Messaging;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Unit tests for <see cref="LogContextEnrichmentFilter{TMessage}"/> — verifies
/// that the filter pushes <c>ConversationId</c>, <c>MessageId</c>, and
/// <c>MessageType</c> into the <see cref="ILogger"/> scope for the duration of
/// the Consume pipeline, and that the scope is disposed after the next filter
/// completes (even on exception).
/// </summary>
public sealed class LogContextEnrichmentFilterTests
{
    private static SendEmailRequestedEvent SampleEvent() =>
        new(new EmailMessage("to@example.com", "s", "b"), DateTime.UtcNow);

    private static (
        LogContextEnrichmentFilter<SendEmailRequestedEvent> filter,
        Mock<ILogger<LogContextEnrichmentFilter<SendEmailRequestedEvent>>> logger
    ) Build()
    {
        var logger = new Mock<ILogger<LogContextEnrichmentFilter<SendEmailRequestedEvent>>>();
        logger.Setup(l => l.BeginScope(It.IsAny<IDictionary<string, object?>>()))
              .Returns(Mock.Of<IDisposable>());
        return (new LogContextEnrichmentFilter<SendEmailRequestedEvent>(logger.Object), logger);
    }

    private static ConsumeContext<SendEmailRequestedEvent> ContextWith(
        Guid? conversationId,
        Guid? messageId)
    {
        var mock = new Mock<ConsumeContext<SendEmailRequestedEvent>>();
        mock.SetupGet(c => c.Message).Returns(SampleEvent());
        mock.SetupGet(c => c.ConversationId).Returns(conversationId);
        mock.SetupGet(c => c.MessageId).Returns(messageId);
        return mock.Object;
    }

    [Fact]
    public async Task Send_PushesConversationIdAndMessageIdAndMessageTypeIntoScope()
    {
        var (filter, logger) = Build();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var next = new Mock<IPipe<ConsumeContext<SendEmailRequestedEvent>>>();
        next.Setup(n => n.Send(It.IsAny<ConsumeContext<SendEmailRequestedEvent>>()))
            .Returns(Task.CompletedTask);

        await filter.Send(ContextWith(conversationId, messageId), next.Object);

        logger.Verify(
            l => l.BeginScope(It.Is<IDictionary<string, object?>>(d =>
                d.ContainsKey("ConversationId") &&
                (Guid?)d["ConversationId"] == conversationId &&
                d.ContainsKey("MessageId") &&
                (Guid?)d["MessageId"] == messageId &&
                d.ContainsKey("MessageType") &&
                (string)d["MessageType"]! == nameof(SendEmailRequestedEvent))),
            Times.Once);
    }

    [Fact]
    public async Task Send_CallsNextPipe()
    {
        var (filter, _) = Build();

        var next = new Mock<IPipe<ConsumeContext<SendEmailRequestedEvent>>>();
        next.Setup(n => n.Send(It.IsAny<ConsumeContext<SendEmailRequestedEvent>>()))
            .Returns(Task.CompletedTask);

        await filter.Send(ContextWith(Guid.NewGuid(), Guid.NewGuid()), next.Object);

        next.Verify(n => n.Send(It.IsAny<ConsumeContext<SendEmailRequestedEvent>>()), Times.Once);
    }

    [Fact]
    public async Task Send_DisposesScope_EvenWhenNextThrows()
    {
        var scope = new Mock<IDisposable>();
        var logger = new Mock<ILogger<LogContextEnrichmentFilter<SendEmailRequestedEvent>>>();
        logger.Setup(l => l.BeginScope(It.IsAny<IDictionary<string, object?>>()))
              .Returns(scope.Object);

        var filter = new LogContextEnrichmentFilter<SendEmailRequestedEvent>(logger.Object);

        var next = new Mock<IPipe<ConsumeContext<SendEmailRequestedEvent>>>();
        next.Setup(n => n.Send(It.IsAny<ConsumeContext<SendEmailRequestedEvent>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = async () => await filter.Send(ContextWith(Guid.NewGuid(), Guid.NewGuid()), next.Object);

        await act.Should().ThrowAsync<InvalidOperationException>();

        scope.Verify(
            s => s.Dispose(),
            Times.Once,
            "the using/await pattern must dispose the scope even when the downstream filter throws");
    }

    [Fact]
    public async Task Send_WithNullConversationAndMessageId_StillPushesScope()
    {
        // Non-HTTP-originated messages (hosted services, direct IBus calls) may have
        // no ConversationId. The filter must not choke on nulls — it pushes them
        // as null and the log line will render them as empty.
        var (filter, logger) = Build();

        var next = new Mock<IPipe<ConsumeContext<SendEmailRequestedEvent>>>();
        next.Setup(n => n.Send(It.IsAny<ConsumeContext<SendEmailRequestedEvent>>()))
            .Returns(Task.CompletedTask);

        var act = async () => await filter.Send(ContextWith(conversationId: null, messageId: null), next.Object);

        await act.Should().NotThrowAsync();

        logger.Verify(
            l => l.BeginScope(It.IsAny<IDictionary<string, object?>>()),
            Times.Once);
    }

    // Probe() is a pure MT diagnostic dispatch — it just hands the string
    // "log-context-enrichment" to MT's ProbeContextExtensions. Testing it would
    // mostly verify the extension method, which is MT's concern, not ours.
}
