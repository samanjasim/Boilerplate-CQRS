using Starter.Domain.Common;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiUsageLog : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? AgentTaskId { get; private set; }
    public AiProviderType Provider { get; private set; }
    public string Model { get; private set; } = default!;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public AiRequestType RequestType { get; private set; }
    public Guid? AiAssistantId { get; private set; }
    public Guid? AgentPrincipalId { get; private set; }
    public ProviderCredentialSource ProviderCredentialSource { get; private set; } = ProviderCredentialSource.Platform;
    public Guid? ProviderCredentialId { get; private set; }

    private AiUsageLog() { }

    private AiUsageLog(
        Guid id,
        Guid? tenantId,
        Guid userId,
        Guid? conversationId,
        Guid? agentTaskId,
        AiProviderType provider,
        string model,
        int inputTokens,
        int outputTokens,
        decimal estimatedCost,
        AiRequestType requestType,
        Guid? aiAssistantId,
        Guid? agentPrincipalId,
        ProviderCredentialSource providerCredentialSource,
        Guid? providerCredentialId) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        ConversationId = conversationId;
        AgentTaskId = agentTaskId;
        Provider = provider;
        Model = model;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        EstimatedCost = estimatedCost;
        RequestType = requestType;
        AiAssistantId = aiAssistantId;
        AgentPrincipalId = agentPrincipalId;
        ProviderCredentialSource = providerCredentialSource;
        ProviderCredentialId = providerCredentialId;
    }

    public static AiUsageLog Create(
        Guid? tenantId,
        Guid userId,
        AiProviderType provider,
        string model,
        int inputTokens,
        int outputTokens,
        decimal estimatedCost,
        AiRequestType requestType,
        Guid? conversationId = null,
        Guid? agentTaskId = null,
        Guid? aiAssistantId = null,
        Guid? agentPrincipalId = null,
        ProviderCredentialSource providerCredentialSource = ProviderCredentialSource.Platform,
        Guid? providerCredentialId = null)
    {
        return new AiUsageLog(
            Guid.NewGuid(),
            tenantId,
            userId,
            conversationId,
            agentTaskId,
            provider,
            model.Trim(),
            inputTokens,
            outputTokens,
            estimatedCost,
            requestType,
            aiAssistantId,
            agentPrincipalId,
            providerCredentialSource,
            providerCredentialId);
    }
}
