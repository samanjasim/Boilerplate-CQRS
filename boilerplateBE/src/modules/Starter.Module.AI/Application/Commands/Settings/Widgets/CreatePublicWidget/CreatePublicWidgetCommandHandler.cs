using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.Widgets;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.CreatePublicWidget;

internal sealed class CreatePublicWidgetCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    IAiEntitlementResolver entitlements) : IRequestHandler<CreatePublicWidgetCommand, Result<AiPublicWidgetDto>>
{
    public async Task<Result<AiPublicWidgetDto>> Handle(CreatePublicWidgetCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<AiPublicWidgetDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to create AI public widgets."));

        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        var entitlementResult = AiPublicWidgetRules.ValidateEntitlements(resolvedEntitlements);
        if (entitlementResult.IsFailure)
            return Result.Failure<AiPublicWidgetDto>(entitlementResult.Error);

        var limitResult = await AiPublicWidgetRules.ValidateCreateLimitAsync(db, tenantId.Value, resolvedEntitlements, ct);
        if (limitResult.IsFailure)
            return Result.Failure<AiPublicWidgetDto>(limitResult.Error);

        var quotaResult = AiPublicWidgetRules.ValidateQuotas(
            request.MonthlyTokenCap,
            request.DailyTokenCap,
            request.RequestsPerMinute,
            resolvedEntitlements);
        if (quotaResult.IsFailure)
            return Result.Failure<AiPublicWidgetDto>(quotaResult.Error);

        var defaultResult = await AiPublicWidgetRules.ValidateDefaultsAsync(
            db,
            tenantId.Value,
            request.DefaultAssistantId,
            request.DefaultPersonaSlug,
            ct);
        if (defaultResult.IsFailure)
            return Result.Failure<AiPublicWidgetDto>(defaultResult.Error);

        try
        {
            var widget = AiPublicWidget.Create(
                tenantId.Value,
                request.Name,
                request.AllowedOrigins,
                request.DefaultAssistantId,
                request.DefaultPersonaSlug,
                request.MonthlyTokenCap,
                request.DailyTokenCap,
                request.RequestsPerMinute,
                currentUser.UserId,
                request.MetadataJson);

            db.AiPublicWidgets.Add(widget);
            await db.SaveChangesAsync(ct);

            return Result.Success(AiPublicWidgetMappings.ToDto(widget));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<AiPublicWidgetDto>(
                Error.Validation("AiSettings.InvalidWidget", ex.Message));
        }
    }
}
