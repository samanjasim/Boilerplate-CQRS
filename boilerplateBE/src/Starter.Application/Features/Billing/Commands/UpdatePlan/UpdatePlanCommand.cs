using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.UpdatePlan;

public sealed record UpdatePlanCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Translations,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string Currency,
    string? Features,
    bool IsPublic,
    int DisplayOrder,
    int TrialDays,
    string? PriceChangeReason) : IRequest<Result>;
