using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAssistant : AggregateRoot, ITenantEntity
{
    private List<string> _enabledToolNames = new();
    private List<Guid> _knowledgeBaseDocIds = new();

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

    public AssistantExecutionMode ExecutionMode { get; private set; }
    public int MaxAgentSteps { get; private set; } = 10;
    public bool IsActive { get; private set; }

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
        bool isActive) : base(id)
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
    }

    public static AiAssistant Create(
        Guid? tenantId,
        string name,
        string? description,
        string systemPrompt,
        AiProviderType? provider = null,
        string? model = null,
        double temperature = 0.7,
        int maxTokens = 4096,
        AssistantExecutionMode executionMode = AssistantExecutionMode.Chat,
        int maxAgentSteps = 10,
        bool isActive = true)
    {
        return new AiAssistant(
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
            isActive);
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

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate() => SetActive(false);
    public void Activate() => SetActive(true);
}
