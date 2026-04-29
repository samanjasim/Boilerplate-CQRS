using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets;

internal static class AiPublicWidgetRules
{
    public static Result ValidateEntitlements(AiEntitlementsDto entitlements)
    {
        return entitlements.WidgetsEnabled
            ? Result.Success()
            : Result.Failure(AiSettingsErrors.WidgetDisabledByPlan);
    }

    public static async Task<Result> ValidateCreateLimitAsync(
        AiDbContext db,
        Guid tenantId,
        AiEntitlementsDto entitlements,
        CancellationToken ct)
    {
        var widgetCount = await db.AiPublicWidgets
            .IgnoreQueryFilters()
            .CountAsync(w => w.TenantId == tenantId, ct);

        return widgetCount < entitlements.WidgetMaxCount
            ? Result.Success()
            : Result.Failure(AiSettingsErrors.WidgetLimitExceeded(entitlements.WidgetMaxCount));
    }

    public static Result ValidateQuotas(
        int? monthlyTokenCap,
        int? dailyTokenCap,
        int? requestsPerMinute,
        AiEntitlementsDto entitlements)
    {
        if (monthlyTokenCap is { } monthly && monthly > entitlements.WidgetMonthlyTokens)
            return Result.Failure(AiSettingsErrors.WidgetQuotaExceedsEntitlement(nameof(monthlyTokenCap)));

        if (dailyTokenCap is { } daily && daily > entitlements.WidgetDailyTokens)
            return Result.Failure(AiSettingsErrors.WidgetQuotaExceedsEntitlement(nameof(dailyTokenCap)));

        if (requestsPerMinute is { } rpm && rpm > entitlements.WidgetRequestsPerMinute)
            return Result.Failure(AiSettingsErrors.WidgetQuotaExceedsEntitlement(nameof(requestsPerMinute)));

        return Result.Success();
    }

    public static async Task<Result> ValidateDefaultsAsync(
        AiDbContext db,
        Guid tenantId,
        Guid? defaultAssistantId,
        string defaultPersonaSlug,
        CancellationToken ct)
    {
        if (defaultAssistantId is { } assistantId)
        {
            var assistantExists = await db.AiAssistants
                .IgnoreQueryFilters()
                .AnyAsync(a => a.Id == assistantId && a.TenantId == tenantId, ct);

            if (!assistantExists)
                return Result.Failure(AiSettingsErrors.AssistantNotFound);
        }

        var normalizedPersonaSlug = string.IsNullOrWhiteSpace(defaultPersonaSlug)
            ? AiPersona.AnonymousSlug
            : defaultPersonaSlug.Trim().ToLowerInvariant();

        if (normalizedPersonaSlug == AiPersona.AnonymousSlug)
            return Result.Success();

        var personaExists = await db.AiPersonas
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == tenantId && p.Slug == normalizedPersonaSlug, ct);

        return personaExists
            ? Result.Success()
            : Result.Failure(AiSettingsErrors.PersonaNotFound);
    }
}
