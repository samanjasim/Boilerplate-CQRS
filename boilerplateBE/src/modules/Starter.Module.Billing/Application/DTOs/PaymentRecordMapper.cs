using Starter.Module.Billing.Domain.Entities;

namespace Starter.Module.Billing.Application.DTOs;

public static class PaymentRecordMapper
{
    public static PaymentRecordDto ToDto(this PaymentRecord entity)
    {
        return new PaymentRecordDto(
            Id: entity.Id,
            Amount: entity.Amount,
            Currency: entity.Currency,
            Status: entity.Status,
            Description: entity.Description,
            PeriodStart: entity.PeriodStart ?? default,
            PeriodEnd: entity.PeriodEnd ?? default,
            CreatedAt: entity.CreatedAt);
    }
}
