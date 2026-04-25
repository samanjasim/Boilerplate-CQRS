using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Events;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Infrastructure.Consumers;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Unit tests for <see cref="EmailDispatchConsumer"/> — the consumer that sends
/// emails scheduled via <see cref="SendEmailRequestedEvent"/>. The bus-level
/// retry policy is what makes this pattern reliable, so these tests focus on:
/// <list type="bullet">
///   <item>Correct delegation to <see cref="IEmailService"/></item>
///   <item>Exceptions propagate (so MT retry fires)</item>
///   <item>A <c>false</c> return is NOT retried (non-retriable non-error path)</item>
/// </list>
/// </summary>
public sealed class EmailDispatchConsumerTests
{
    private static ConsumeContext<SendEmailRequestedEvent> ContextFor(SendEmailRequestedEvent evt)
    {
        var mock = new Mock<ConsumeContext<SendEmailRequestedEvent>>();
        mock.SetupGet(c => c.Message).Returns(evt);
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    private static SendEmailRequestedEvent SampleEvent(string to = "acme@example.com") =>
        new(new EmailMessage(to, "Verify your email", "<p>Your code is 123456</p>"), DateTime.UtcNow);

    [Fact]
    public async Task Consume_CallsEmailService_WithExactMessageFromEvent()
    {
        var evt = SampleEvent();
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var consumer = new EmailDispatchConsumer(emailService.Object, NullLogger<EmailDispatchConsumer>.Instance);

        await consumer.Consume(ContextFor(evt));

        emailService.Verify(
            s => s.SendAsync(
                It.Is<EmailMessage>(m =>
                    m.To == evt.Message.To &&
                    m.Subject == evt.Message.Subject &&
                    m.Body == evt.Message.Body),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the consumer must forward the scheduled message unmodified");
    }

    [Fact]
    public async Task Consume_PropagatesException_SoMtRetryCanFire()
    {
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var consumer = new EmailDispatchConsumer(emailService.Object, NullLogger<EmailDispatchConsumer>.Instance);

        var act = async () => await consumer.Consume(ContextFor(SampleEvent()));

        await act.Should().ThrowAsync<InvalidOperationException>(
            "swallowing the exception would defeat the retry policy that makes the email path reliable");
    }

    [Fact]
    public async Task Consume_Throws_WhenEmailServiceReturnsFalse()
    {
        // IEmailService catches its own SMTP exceptions internally and reports
        // failure via a `false` return. The consumer treats that as transient
        // and throws so MT's retry policy fires. Without this, a brief SMTP
        // outage during tenant registration would silently drop the
        // verification email — exactly the failure mode A1 was meant to fix.
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var consumer = new EmailDispatchConsumer(emailService.Object, NullLogger<EmailDispatchConsumer>.Instance);

        var act = async () => await consumer.Consume(ContextFor(SampleEvent()));

        await act.Should().ThrowAsync<InvalidOperationException>(
            "swallowing 'false' would defeat MT's retry policy and lose the email on transient SMTP outages");
    }

    [Fact]
    public async Task Consume_PassesCancellationToken_FromConsumeContext()
    {
        using var cts = new CancellationTokenSource();
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), cts.Token))
            .ReturnsAsync(true);

        var contextMock = new Mock<ConsumeContext<SendEmailRequestedEvent>>();
        contextMock.SetupGet(c => c.Message).Returns(SampleEvent());
        contextMock.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        var consumer = new EmailDispatchConsumer(emailService.Object, NullLogger<EmailDispatchConsumer>.Instance);

        await consumer.Consume(contextMock.Object);

        emailService.Verify(
            s => s.SendAsync(It.IsAny<EmailMessage>(), cts.Token),
            Times.Once,
            "the consumer must flow its CancellationToken into the outbound SMTP call");
    }
}
