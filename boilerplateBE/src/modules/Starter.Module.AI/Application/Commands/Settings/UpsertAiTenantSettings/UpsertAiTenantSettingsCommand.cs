using MediatR;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;

public sealed record UpsertAiTenantSettingsCommand(
    Guid? TenantId,
    ProviderCredentialPolicy RequestedProviderCredentialPolicy,
    SafetyPreset DefaultSafetyPreset,
    decimal? MonthlyCostCapUsd,
    decimal? DailyCostCapUsd,
    decimal? PlatformMonthlyCostCapUsd,
    decimal? PlatformDailyCostCapUsd,
    int? RequestsPerMinute,
    int? PublicMonthlyTokenCap,
    int? PublicDailyTokenCap,
    int? PublicRequestsPerMinute,
    string? AssistantDisplayName,
    string? Tone,
    Guid? AvatarFileId,
    string? BrandInstructions) : IRequest<Result<AiTenantSettingsDto>>;
