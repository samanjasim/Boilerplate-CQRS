namespace Starter.Module.Billing.Application.DTOs;

public sealed record SubscriptionStatusCountsDto(
    int Trialing,
    int Active,
    int PastDue,
    int Canceled,
    int Expired
);
