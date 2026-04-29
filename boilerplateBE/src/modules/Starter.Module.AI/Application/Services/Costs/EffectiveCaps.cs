namespace Starter.Module.AI.Application.Services.Costs;

/// <summary>
/// The effective per-agent caps after resolving plan ceilings against tenant and per-agent overrides.
/// All values are non-negative; a value of 0 means "blocked" (no spend / no requests permitted).
/// There is no "uncapped" sentinel — every plan tier carries an explicit ceiling.
/// Platform caps apply only to spend using platform credentials.
/// </summary>
public sealed record EffectiveCaps(
    decimal MonthlyUsd,
    decimal DailyUsd,
    int Rpm,
    decimal PlatformMonthlyUsd = 0m,
    decimal PlatformDailyUsd = 0m);
