using Starter.Domain.Common;
using Starter.Domain.Exceptions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiPersona : AggregateRoot, ITenantEntity
{
    public const string AnonymousSlug = "anonymous";
    public const string DefaultSlug = "default";

    private List<string> _permittedAgentSlugs = new();

    public Guid? TenantId { get; private set; }
    public string Slug { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public PersonaAudienceType AudienceType { get; private set; }
    public SafetyPreset SafetyPreset { get; private set; }
    public bool IsSystemReserved { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public IReadOnlyList<string> PermittedAgentSlugs
    {
        get => _permittedAgentSlugs;
        private set => _permittedAgentSlugs = value?.ToList() ?? new();
    }

    private AiPersona() { }

    private AiPersona(
        Guid id,
        Guid? tenantId,
        string slug,
        string displayName,
        string? description,
        PersonaAudienceType audienceType,
        SafetyPreset safetyPreset,
        bool isSystemReserved,
        bool isActive,
        Guid createdByUserId) : base(id)
    {
        TenantId = tenantId;
        Slug = slug;
        DisplayName = displayName;
        Description = description;
        AudienceType = audienceType;
        SafetyPreset = safetyPreset;
        IsSystemReserved = isSystemReserved;
        IsActive = isActive;
        CreatedByUserId = createdByUserId;
    }

    public static AiPersona Create(
        Guid? tenantId,
        string slug,
        string displayName,
        string? description,
        PersonaAudienceType audienceType,
        SafetyPreset safetyPreset,
        Guid createdByUserId)
    {
        if (audienceType == PersonaAudienceType.Anonymous)
            throw new DomainException(
                PersonaErrors.AudienceAnonymousReserved.Description,
                PersonaErrors.AudienceAnonymousReserved.Code);

        return new AiPersona(
            Guid.NewGuid(),
            tenantId,
            slug.Trim().ToLowerInvariant(),
            displayName.Trim(),
            description?.Trim(),
            audienceType,
            safetyPreset,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);
    }

    public static AiPersona CreateAnonymous(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            AnonymousSlug,
            "Anonymous",
            "Unauthenticated public visitor.",
            PersonaAudienceType.Anonymous,
            SafetyPreset.Standard,
            isSystemReserved: true,
            isActive: false,
            createdByUserId);

    public static AiPersona CreateDefault(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            DefaultSlug,
            "Default",
            "Default persona for authenticated users.",
            PersonaAudienceType.Internal,
            SafetyPreset.Standard,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);

    public void Update(
        string displayName,
        string? description,
        SafetyPreset safetyPreset,
        IEnumerable<string>? permittedAgentSlugs,
        bool isActive)
    {
        DisplayName = displayName.Trim();
        Description = description?.Trim();
        SafetyPreset = safetyPreset;
        _permittedAgentSlugs = Normalize(permittedAgentSlugs);
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    private static List<string> Normalize(IEnumerable<string>? slugs) =>
        slugs?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? new();
}
