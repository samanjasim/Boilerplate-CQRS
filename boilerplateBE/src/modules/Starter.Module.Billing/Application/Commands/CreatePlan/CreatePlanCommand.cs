using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.CreatePlan;

public sealed record CreatePlanCommand(
    string Name,
    string Slug,
    string? Description,
    string? Translations,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string Currency,
    List<PlanFeatureEntry>? Features,
    bool IsFree,
    bool IsPublic,
    int DisplayOrder,
    int TrialDays) : IRequest<Result<Guid>>;
