using MassTransit;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Events;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Consumers;

/// <summary>
/// Dispatches emails scheduled by command handlers via
/// <see cref="SendEmailRequestedEvent"/>.
///
/// <para>
/// <b>Retry + DLQ</b> — the default bus policy (3 retries at 1s/5s/15s, then
/// `_error` queue) applies automatically. Any exception from
/// <c>IEmailService.SendAsync</c> propagates, letting MT retry. If all retries
/// exhaust, the message lands in the dead-letter queue for ops investigation.
/// </para>
///
/// <para>
/// <b>Idempotency</b> — an email send is an external side effect; re-delivery
/// may cause the user to receive the email twice. We accept this: at-least-once
/// with an occasional duplicate is better than at-most-once with a lost
/// verification email. The OTP inside the body remains valid until its Redis
/// TTL expires, so a duplicate is harmless.
/// </para>
/// </summary>
public sealed class EmailDispatchConsumer(
    IEmailService emailService,
    ILogger<EmailDispatchConsumer> logger) : IConsumer<SendEmailRequestedEvent>
{
    public async Task Consume(ConsumeContext<SendEmailRequestedEvent> context)
    {
        var message = context.Message.Message;

        var sent = await emailService.SendAsync(message, context.CancellationToken);

        if (!sent)
        {
            // IEmailService catches internal SMTP exceptions and signals failure
            // via a `false` return. Treat that as a transient failure so MT's
            // retry policy fires (3× 1s/5s/15s, then `_error` queue).
            //
            // We can't distinguish "transient" (broker down) from "permanent"
            // (malformed address) at this layer because the EmailService API
            // is binary. The retry burns at most ~21 s for permanent failures —
            // an acceptable cost for never silently dropping a verification
            // email when SMTP is briefly unreachable.
            throw new InvalidOperationException(
                $"IEmailService reported failure sending '{message.Subject}' to {message.To}");
        }

        logger.LogInformation(
            "Email dispatched to {To} subject {Subject}",
            message.To, message.Subject);
    }
}
