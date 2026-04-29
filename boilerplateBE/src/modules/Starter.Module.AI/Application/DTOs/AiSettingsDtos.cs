namespace Starter.Module.AI.Application.DTOs;

public sealed record AiEntitlementsDto(
    decimal TotalMonthlyUsd,
    decimal TotalDailyUsd,
    decimal PlatformMonthlyUsd,
    decimal PlatformDailyUsd,
    int RequestsPerMinute,
    bool ByokEnabled,
    bool WidgetsEnabled,
    int WidgetMaxCount,
    int WidgetMonthlyTokens,
    int WidgetDailyTokens,
    int WidgetRequestsPerMinute,
    IReadOnlyList<string> AllowedProviders,
    IReadOnlyList<string> AllowedModels);
