using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.UpdatePlan;

public sealed record UpdatePlanCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Translations,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string Currency,
    List<PlanFeatureEntry>? Features,
    bool IsPublic,
    int DisplayOrder,
    int TrialDays,
    string? PriceChangeReason) : IRequest<Result>;
