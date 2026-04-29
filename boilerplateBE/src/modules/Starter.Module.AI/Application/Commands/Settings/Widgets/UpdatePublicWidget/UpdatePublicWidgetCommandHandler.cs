using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.Widgets;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.UpdatePublicWidget;

internal sealed class UpdatePublicWidgetCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    IAiEntitlementResolver entitlements) : IRequestHandler<UpdatePublicWidgetCommand, Result<AiPublicWidgetDto>>
{
    public async Task<Result<AiPublicWidgetDto>> Handle(UpdatePublicWidgetCommand request, CancellationToken ct)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Result.Failure<AiPublicWidgetDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to update AI public widgets."));

        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        var entitlementResult = AiPublicWidgetRules.ValidateEntitlements(resolvedEntitlements);
        if (entitlementResult.IsFailure)
            return Result.Failure<AiPublicWidgetDto>(entitlementResult.Error);

        var quotaResult = AiPublicWidgetRules.ValidateQuotas(
            request.MonthlyTokenCap,
            request.DailyTokenCap,
            request.RequestsPerMinute,
            resolvedEntitlements);
        if (quotaResult.IsFailure)
            return Result.Failure<AiPublicWidgetDto>(quotaResult.Error);

        var widget = await db.AiPublicWidgets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == request.Id && w.TenantId == currentUser.TenantId.Value, ct);
        if (widget is null)
            return Result.Failure<AiPublicWidgetDto>(AiSettingsErrors.WidgetNotFound);

        var defaultResult = await AiPublicWidgetRules.ValidateDefaultsAsync(
            db,
            currentUser.TenantId.Value,
            request.DefaultAssistantId,
            request.DefaultPersonaSlug,
            ct);
        if (defaultResult.IsFailure)
            return Result.Failure<AiPublicWidgetDto>(defaultResult.Error);

        try
        {
            widget.Update(
                request.Name,
                request.AllowedOrigins,
                request.DefaultAssistantId,
                request.DefaultPersonaSlug,
                request.MonthlyTokenCap,
                request.DailyTokenCap,
                request.RequestsPerMinute,
                request.MetadataJson);
            widget.SetStatus(request.Status);

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
