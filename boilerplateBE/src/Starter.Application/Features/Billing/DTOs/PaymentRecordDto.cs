using Starter.Domain.Billing.Enums;

namespace Starter.Application.Features.Billing.DTOs;

public sealed record PaymentRecordDto(
    Guid Id, decimal Amount, string Currency, PaymentStatus Status,
    string? Description, DateTime PeriodStart, DateTime PeriodEnd, DateTime CreatedAt);
