using Starter.Domain.Billing.Entities;

namespace Starter.Application.Features.Billing.DTOs;

public static class SubscriptionPlanMapper
{
    public static SubscriptionPlanDto ToDto(this SubscriptionPlan entity, int subscriberCount = 0)
    {
        return new SubscriptionPlanDto(
            Id: entity.Id,
            Name: entity.Name,
            Slug: entity.Slug,
            Description: entity.Description,
            Translations: entity.Translations,
            MonthlyPrice: entity.MonthlyPrice,
            AnnualPrice: entity.AnnualPrice,
            Currency: entity.Currency,
            Features: PlanFeatureEntry.ParseFeatures(entity.Features),
            IsFree: entity.IsFree,
            IsActive: entity.IsActive,
            IsPublic: entity.IsPublic,
            DisplayOrder: entity.DisplayOrder,
            TrialDays: entity.TrialDays,
            SubscriberCount: subscriberCount,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
