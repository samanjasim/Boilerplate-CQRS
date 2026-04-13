using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAssistant : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string SystemPrompt { get; private set; } = default!;
    public AiProviderType? Provider { get; private set; }
    public string? Model { get; private set; }
    public double Temperature { get; private set; } = 0.7;
    public int MaxTokens { get; private set; } = 4096;
    public string EnabledToolNames { get; private set; } = "[]";
    public string KnowledgeBaseDocIds { get; private set; } = "[]";
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

    public void SetEnabledTools(string enabledToolNamesJson)
    {
        EnabledToolNames = enabledToolNamesJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetKnowledgeBase(string knowledgeBaseDocIdsJson)
    {
        KnowledgeBaseDocIds = knowledgeBaseDocIdsJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
    }
}
