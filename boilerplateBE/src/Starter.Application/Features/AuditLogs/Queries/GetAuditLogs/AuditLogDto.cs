using Starter.Domain.Common.Enums;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogs;

public sealed record AuditLogDto(
    Guid Id,
    AuditEntityType EntityType,
    Guid EntityId,
    AuditAction Action,
    string? Changes,
    Guid? PerformedBy,
    string? PerformedByName,
    DateTime PerformedAt,
    string? IpAddress,
    string? CorrelationId);
