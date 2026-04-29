using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiTenantSettings : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public ProviderCredentialPolicy RequestedProviderCredentialPolicy { get; private set; }
    public SafetyPreset DefaultSafetyPreset { get; private set; }
    public decimal? MonthlyCostCapUsd { get; private set; }
    public decimal? DailyCostCapUsd { get; private set; }
    public decimal? PlatformMonthlyCostCapUsd { get; private set; }
    public decimal? PlatformDailyCostCapUsd { get; private set; }
    public int? RequestsPerMinute { get; private set; }
    public int? PublicMonthlyTokenCap { get; private set; }
    public int? PublicDailyTokenCap { get; private set; }
    public int? PublicRequestsPerMinute { get; private set; }
    public string? AssistantDisplayName { get; private set; }
    public string? Tone { get; private set; }
    public Guid? AvatarFileId { get; private set; }
    public string? BrandInstructions { get; private set; }

    private AiTenantSettings() { }

    private AiTenantSettings(Guid tenantId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        RequestedProviderCredentialPolicy = ProviderCredentialPolicy.PlatformOnly;
        DefaultSafetyPreset = SafetyPreset.Standard;
    }

    public static AiTenantSettings CreateDefault(Guid tenantId)
    {
        ValidateTenantId(tenantId);

        return new AiTenantSettings(tenantId);
    }

    private static void ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
    }

    public void UpdatePolicy(ProviderCredentialPolicy policy, SafetyPreset safetyPreset)
    {
        RequestedProviderCredentialPolicy = policy;
        DefaultSafetyPreset = safetyPreset;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateCostSelfLimits(
        decimal? monthlyCostCapUsd,
        decimal? dailyCostCapUsd,
        decimal? platformMonthlyCostCapUsd,
        decimal? platformDailyCostCapUsd,
        int? requestsPerMinute)
    {
        if (monthlyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyCostCapUsd));
        if (dailyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(dailyCostCapUsd));
        if (platformMonthlyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(platformMonthlyCostCapUsd));
        if (platformDailyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(platformDailyCostCapUsd));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));

        MonthlyCostCapUsd = monthlyCostCapUsd;
        DailyCostCapUsd = dailyCostCapUsd;
        PlatformMonthlyCostCapUsd = platformMonthlyCostCapUsd;
        PlatformDailyCostCapUsd = platformDailyCostCapUsd;
        RequestsPerMinute = requestsPerMinute;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdatePublicWidgetDefaults(int? monthlyTokenCap, int? dailyTokenCap, int? requestsPerMinute)
    {
        if (monthlyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyTokenCap));
        if (dailyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(dailyTokenCap));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));

        PublicMonthlyTokenCap = monthlyTokenCap;
        PublicDailyTokenCap = dailyTokenCap;
        PublicRequestsPerMinute = requestsPerMinute;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateBrandProfile(
        string? assistantDisplayName,
        string? tone,
        Guid? avatarFileId,
        string? brandInstructions)
    {
        AssistantDisplayName = string.IsNullOrWhiteSpace(assistantDisplayName) ? null : assistantDisplayName.Trim();
        Tone = string.IsNullOrWhiteSpace(tone) ? null : tone.Trim();
        AvatarFileId = avatarFileId == Guid.Empty ? null : avatarFileId;
        BrandInstructions = string.IsNullOrWhiteSpace(brandInstructions) ? null : brandInstructions.Trim();
        ModifiedAt = DateTime.UtcNow;
    }
}
