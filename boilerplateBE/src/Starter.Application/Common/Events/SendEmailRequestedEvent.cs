using Starter.Application.Common.Models;

namespace Starter.Application.Common.Events;

/// <summary>
/// Cross-cutting integration event: "please send this pre-rendered email".
/// Published by command handlers via <see cref="Interfaces.IIntegrationEventCollector"/>
/// and consumed by <c>EmailDispatchConsumer</c> in Infrastructure.
///
/// <para>
/// <b>Why route email through the outbox instead of calling <c>IEmailService</c> inline?</b><br/>
/// An inline <c>emailService.SendAsync()</c> after <c>SaveChangesAsync</c> has no retry
/// protection: if the SMTP provider is briefly unreachable, the business data is
/// already committed and the email is lost. For flows like tenant registration,
/// that locks the user out of their own account. By scheduling this event,
/// the SMTP call moves into <c>EmailDispatchConsumer</c>, where MT's default
/// retry policy (3× 1s/5s/15s) + dead-letter queue handle transient failures
/// automatically.
/// </para>
///
/// <para>
/// <b>Body is pre-rendered</b> — the handler still calls <c>IEmailTemplateService</c>
/// synchronously so the message (including any OTP code) is materialized before
/// the DB commit. This keeps the OTP's Redis write and the outbox row in the same
/// logical unit: if the commit fails, the OTP expires on its own TTL and no
/// phantom email is sent.
/// </para>
/// </summary>
public sealed record SendEmailRequestedEvent(
    EmailMessage Message,
    DateTime OccurredAt) : IDomainEvent;
