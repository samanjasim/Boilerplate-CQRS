using Starter.Domain.Common;

namespace Starter.Domain.Common.Events;

public sealed record FileUploadedEvent(
    Guid FileId,
    Guid? TenantId,
    string FileName,
    long Size,
    string ContentType) : DomainEventBase;
