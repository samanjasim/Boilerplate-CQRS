using Starter.Domain.Common;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Exceptions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAssistant : AggregateRoot, ITenantEntity, IShareable
{
    private List<string> _enabledToolNames = new();
    private List<Guid> _knowledgeBaseDocIds = new();
    private List<string> _personaTargetSlugs = new();

    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string SystemPrompt { get; private set; } = default!;
    public AiProviderType? Provider { get; private set; }
    public string? Model { get; private set; }
    public double Temperature { get; private set; } = 0.7;
    public int MaxTokens { get; private set; } = 4096;

    /// <summary>Names of tools this assistant is allowed to call. Persisted as jsonb.</summary>
    public IReadOnlyList<string> EnabledToolNames
    {
        get => _enabledToolNames;
        private set => _enabledToolNames = value?.ToList() ?? new();
    }

    /// <summary>IDs of documents this assistant's RAG retrieval is scoped to. Persisted as jsonb.</summary>
    public IReadOnlyList<Guid> KnowledgeBaseDocIds
    {
        get => _knowledgeBaseDocIds;
        private set => _knowledgeBaseDocIds = value?.ToList() ?? new();
    }

    public string Slug { get; private set; } = "";

    /// <summary>Slugs of personas this assistant is visible to. Empty = visible to all personas. Persisted as jsonb.</summary>
    public IReadOnlyList<string> PersonaTargetSlugs
    {
        get => _personaTargetSlugs;
        private set => _personaTargetSlugs = value?.ToList() ?? new();
    }

    public AssistantExecutionMode ExecutionMode { get; private set; }
    public int MaxAgentSteps { get; private set; } = 10;
    public bool IsActive { get; private set; }
    public AiRagScope RagScope { get; private set; } = AiRagScope.None;

    public ResourceVisibility Visibility { get; private set; } = ResourceVisibility.Private;
    public AssistantAccessMode AccessMode { get; private set; } = AssistantAccessMode.CallerPrincipal;
    public Guid CreatedByUserId { get; private set; }

    public string? TemplateSourceSlug { get; private set; }
    public string? TemplateSourceVersion { get; private set; }

    public decimal? MonthlyCostCapUsd { get; private set; }
    public decimal? DailyCostCapUsd { get; private set; }
    public int? RequestsPerMinute { get; private set; }

    private AiAssistant() { }

    private AiAssistant(
        Guid id,
        Guid? tenantId,
        string name,
        string? description,
        string systemPrompt,
        AiProviderType? provider,
        string? model,
        double temperature,
        int maxTokens,
        AssistantExecutionMode executionMode,
        int maxAgentSteps,
        bool isActive,
        Guid createdByUserId) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        SystemPrompt = systemPrompt;
        Provider = provider;
        Model = model;
        Temperature = temperature;
        MaxTokens = maxTokens;
        ExecutionMode = executionMode;
        MaxAgentSteps = maxAgentSteps;
        IsActive = isActive;
        CreatedByUserId = createdByUserId;
    }

    public static AiAssistant Create(
        Guid? tenantId,
        string name,
        string? description,
        string systemPrompt,
        Guid createdByUserId,
        AiProviderType? provider = null,
        string? model = null,
        double temperature = 0.7,
        int maxTokens = 4096,
        AssistantExecutionMode executionMode = AssistantExecutionMode.Chat,
        int maxAgentSteps = 10,
        bool isActive = true,
        string? slug = null)
    {
        var a = new AiAssistant(
            Guid.NewGuid(),
            tenantId,
            name.Trim(),
            description?.Trim(),
            systemPrompt.Trim(),
            provider,
            model?.Trim(),
            temperature,
            maxTokens,
            executionMode,
            maxAgentSteps,
            isActive,
            createdByUserId);

        if (!string.IsNullOrWhiteSpace(slug))
            a.SetSlug(slug);

        return a;
    }

    public void SetVisibility(ResourceVisibility visibility)
    {
        if ((int)visibility > (int)ResourceVisibility.TenantWide)
            throw new DomainException(
                AccessErrors.VisibilityNotAllowedForResourceType.Description,
                AccessErrors.VisibilityNotAllowedForResourceType.Code);

        Visibility = visibility;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetAccessMode(AssistantAccessMode accessMode)
    {
        AccessMode = accessMode;
        ModifiedAt = DateTime.UtcNow;
    }

    public void TransferOwnership(Guid newOwnerUserId)
    {
        CreatedByUserId = newOwnerUserId;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string? description,
        string systemPrompt,
        AiProviderType? provider,
        string? model,
        double temperature,
        int maxTokens,
        AssistantExecutionMode executionMode,
        int maxAgentSteps)
    {
        Name = name.Trim();
        Description = description?.Trim();
        SystemPrompt = systemPrompt.Trim();
        Provider = provider;
        Model = model?.Trim();
        Temperature = temperature;
        MaxTokens = maxTokens;
        ExecutionMode = executionMode;
        MaxAgentSteps = maxAgentSteps;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetEnabledTools(IEnumerable<string> toolNames)
    {
        _enabledToolNames = toolNames?.Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct()
            .ToList() ?? new();
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetKnowledgeBase(IEnumerable<Guid> documentIds)
    {
        _knowledgeBaseDocIds = documentIds?.Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? new();
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetRagScope(AiRagScope scope)
    {
        if (scope == AiRagScope.SelectedDocuments && _knowledgeBaseDocIds.Count == 0)
            throw new DomainException(
                AiErrors.RagScopeRequiresDocuments.Description,
                AiErrors.RagScopeRequiresDocuments.Code);

        RagScope = scope;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate() => SetActive(false);
    public void Activate() => SetActive(true);

    public void SetSlug(string slug)
    {
        Slug = (slug ?? "").Trim().ToLowerInvariant();
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetPersonaTargets(IEnumerable<string?>? slugs)
    {
        _personaTargetSlugs = (slugs ?? Array.Empty<string?>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        ModifiedAt = DateTime.UtcNow;
    }

    public void StampTemplateSource(string templateSlug, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(templateSlug))
            throw new ArgumentException("Template slug must be non-empty.", nameof(templateSlug));
        TemplateSourceSlug = templateSlug;
        TemplateSourceVersion = version;
    }

    public void SetBudget(decimal? monthlyUsd, decimal? dailyUsd, int? requestsPerMinute)
    {
        if (monthlyUsd is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyUsd));
        if (dailyUsd is < 0) throw new ArgumentOutOfRangeException(nameof(dailyUsd));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));
        MonthlyCostCapUsd = monthlyUsd;
        DailyCostCapUsd = dailyUsd;
        RequestsPerMinute = requestsPerMinute;
        ModifiedAt = DateTime.UtcNow;
    }

    public bool IsVisibleToPersona(string personaSlug, IReadOnlyList<string> personaPermittedAgentSlugs)
    {
        personaSlug = personaSlug.ToLowerInvariant();
        var personaSide = personaPermittedAgentSlugs.Count == 0
            || personaPermittedAgentSlugs.Contains(Slug, StringComparer.Ordinal);
        var agentSide = _personaTargetSlugs.Count == 0
            || _personaTargetSlugs.Contains(personaSlug, StringComparer.Ordinal);
        return personaSide && agentSide;
    }
}
