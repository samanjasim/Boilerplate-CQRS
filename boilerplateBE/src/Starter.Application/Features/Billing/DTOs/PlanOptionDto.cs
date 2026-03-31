namespace Starter.Application.Features.Billing.DTOs;

public sealed record PlanOptionDto(
    string Key,
    string Name,
    string? Description,
    string ValueType,
    string DefaultValue,
    string Category);
