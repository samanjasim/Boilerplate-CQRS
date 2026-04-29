namespace Starter.Application.Features.AuditLogs.DTOs;

public sealed record AuditLogDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string? Changes,
    Guid? PerformedBy,
    string? PerformedByName,
    DateTime PerformedAt,
    string? IpAddress,
    string? CorrelationId,
    Guid? OnBehalfOfUserId,
    Guid? AgentPrincipalId,
    Guid? AgentRunId);
