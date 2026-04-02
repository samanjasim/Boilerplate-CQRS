using Starter.Domain.Common;

namespace Starter.Domain.Common.Events;

public sealed record FileDeletedEvent(
    Guid FileId,
    Guid? TenantId,
    string FileName) : DomainEventBase;
