using Starter.Module.Billing.Domain.Enums;

namespace Starter.Module.Billing.Application.DTOs;

public sealed record PaymentRecordDto(
    Guid Id, decimal Amount, string Currency, PaymentStatus Status,
    string? Description, DateTime PeriodStart, DateTime PeriodEnd, DateTime CreatedAt);
