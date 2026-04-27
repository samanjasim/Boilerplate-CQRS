using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Domain.Entities;

/// <summary>
/// Tenant-overridable threshold profile per `(Preset, Provider)`.
/// Single-table-with-nullable-tenant pattern: platform defaults are rows where
/// `TenantId == null`; tenant overrides are rows where `TenantId == X`.
/// Soft-deleted via <see cref="IsActive"/>; the unique partial index spans only active rows.
/// </summary>
public sealed class AiSafetyPresetProfile : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public SafetyPreset Preset { get; private set; }
    public ModerationProvider Provider { get; private set; }
    public string CategoryThresholdsJson { get; private set; } = "{}";
    public string BlockedCategoriesJson { get; private set; } = "[]";
    public ModerationFailureMode FailureMode { get; private set; }
    public bool RedactPii { get; private set; }
    public int Version { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    private AiSafetyPresetProfile() { }

    private AiSafetyPresetProfile(
        Guid id,
        Guid? tenantId,
        SafetyPreset preset,
        ModerationProvider provider,
        string thresholdsJson,
        string blockedCategoriesJson,
        ModerationFailureMode failureMode,
        bool redactPii) : base(id)
    {
        TenantId = tenantId;
        Preset = preset;
        Provider = provider;
        CategoryThresholdsJson = thresholdsJson;
        BlockedCategoriesJson = blockedCategoriesJson;
        FailureMode = failureMode;
        RedactPii = redactPii;
    }

    public static AiSafetyPresetProfile Create(
        Guid? tenantId,
        SafetyPreset preset,
        ModerationProvider provider,
        string thresholdsJson,
        string blockedCategoriesJson,
        ModerationFailureMode failureMode,
        bool redactPii)
    {
        if (string.IsNullOrWhiteSpace(thresholdsJson)) thresholdsJson = "{}";
        if (string.IsNullOrWhiteSpace(blockedCategoriesJson)) blockedCategoriesJson = "[]";

        var entity = new AiSafetyPresetProfile(
            Guid.NewGuid(), tenantId, preset, provider,
            thresholdsJson, blockedCategoriesJson, failureMode, redactPii);
        entity.RaiseDomainEvent(new SafetyPresetProfileUpdatedEvent(tenantId, entity.Id));
        return entity;
    }

    public void Update(
        string thresholdsJson,
        string blockedCategoriesJson,
        ModerationFailureMode failureMode,
        bool redactPii)
    {
        CategoryThresholdsJson = string.IsNullOrWhiteSpace(thresholdsJson) ? "{}" : thresholdsJson;
        BlockedCategoriesJson = string.IsNullOrWhiteSpace(blockedCategoriesJson) ? "[]" : blockedCategoriesJson;
        FailureMode = failureMode;
        RedactPii = redactPii;
        Version += 1;
        ModifiedAt = DateTime.UtcNow;
        RaiseDomainEvent(new SafetyPresetProfileUpdatedEvent(TenantId, Id));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
        RaiseDomainEvent(new SafetyPresetProfileUpdatedEvent(TenantId, Id));
    }
}
