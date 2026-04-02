using Starter.Domain.Common;

namespace Starter.Domain.Identity.Events;

public sealed record InvitationAcceptedEvent(
    Guid UserId,
    Guid? TenantId,
    string Email,
    Guid RoleId) : DomainEventBase;
