namespace Starter.Application.Common.Events;

/// <summary>
/// Published when a new user account is created. Used by Notifications to
/// send welcome emails and by Billing/usage tracking to count seats.
/// </summary>
public sealed record UserRegisteredEvent(
    Guid UserId,
    Guid? TenantId,
    string Email,
    string Username,
    DateTime OccurredAt
) : IDomainEvent;
