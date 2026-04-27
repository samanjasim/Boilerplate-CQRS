namespace Starter.Module.AI.Application.Services.Costs;

/// <summary>
/// The effective per-agent caps after resolving plan ceilings against per-agent overrides.
/// All values are non-negative; a value of 0 means "blocked" (no spend / no requests permitted).
/// There is no "uncapped" sentinel — every plan tier carries an explicit ceiling.
/// </summary>
public sealed record EffectiveCaps(decimal MonthlyUsd, decimal DailyUsd, int Rpm);
