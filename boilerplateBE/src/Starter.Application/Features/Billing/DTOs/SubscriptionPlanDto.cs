namespace Starter.Application.Features.Billing.DTOs;

public sealed record SubscriptionPlanDto(
    Guid Id, string Name, string Slug, string? Description, string? Translations,
    decimal MonthlyPrice, decimal AnnualPrice, string Currency, List<PlanFeatureEntry>? Features,
    bool IsFree, bool IsActive, bool IsPublic, int DisplayOrder, int TrialDays,
    int SubscriberCount, DateTime CreatedAt, DateTime? ModifiedAt);
