namespace Starter.Module.Workflow.Application.DTOs;

public sealed record DelegationRuleDto(
    Guid Id,
    Guid ToUserId,
    string? ToDisplayName,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive);
