using Starter.Abstractions.Ai.Events;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

/// <summary>
/// Records a sensitive tool invocation that an AI agent has requested
/// but cannot execute until a human approves. The lifecycle is
/// <c>Pending → Approved | Denied | Expired</c>; each transition raises
/// the matching event from <c>Starter.Abstractions.Ai.Events</c> so
/// the Communication module can prompt / notify without referencing
/// the AI module directly.
/// </summary>
public sealed class AiPendingApproval : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public string AssistantName { get; private set; } = default!;
    public Guid AgentPrincipalId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? AgentTaskId { get; private set; }
    public Guid? RequestingUserId { get; private set; }
    public string ToolName { get; private set; } = default!;
    public string CommandTypeName { get; private set; } = default!;
    public string ArgumentsJson { get; private set; } = "{}";
    public string? ReasonHint { get; private set; }
    public PendingApprovalStatus Status { get; private set; } = PendingApprovalStatus.Pending;
    public Guid? DecisionUserId { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private AiPendingApproval() { }

    private AiPendingApproval(
        Guid id,
        Guid? tenantId,
        Guid assistantId,
        string assistantName,
        Guid agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? requestingUserId,
        string toolName,
        string commandTypeName,
        string argumentsJson,
        string? reasonHint,
        DateTime expiresAt) : base(id)
    {
        TenantId = tenantId;
        AssistantId = assistantId;
        AssistantName = assistantName;
        AgentPrincipalId = agentPrincipalId;
        ConversationId = conversationId;
        AgentTaskId = agentTaskId;
        RequestingUserId = requestingUserId;
        ToolName = toolName;
        CommandTypeName = commandTypeName;
        ArgumentsJson = argumentsJson;
        ReasonHint = reasonHint;
        ExpiresAt = expiresAt;
    }

    public static AiPendingApproval Create(
        Guid? tenantId,
        Guid assistantId,
        string assistantName,
        Guid agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? requestingUserId,
        string toolName,
        string commandTypeName,
        string argumentsJson,
        string? reasonHint,
        DateTime expiresAt)
    {
        if (conversationId is null && agentTaskId is null)
            throw new ArgumentException(
                "At least one of conversationId or agentTaskId must be set.",
                nameof(conversationId));
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("toolName required.", nameof(toolName));
        if (string.IsNullOrWhiteSpace(commandTypeName))
            throw new ArgumentException("commandTypeName required.", nameof(commandTypeName));
        if (string.IsNullOrWhiteSpace(argumentsJson)) argumentsJson = "{}";

        var entity = new AiPendingApproval(
            Guid.NewGuid(),
            tenantId,
            assistantId,
            assistantName.Trim(),
            agentPrincipalId,
            conversationId,
            agentTaskId,
            requestingUserId,
            toolName.Trim(),
            commandTypeName.Trim(),
            argumentsJson,
            reasonHint?.Trim(),
            expiresAt);

        if (tenantId is { } tid)
        {
            entity.RaiseDomainEvent(new AgentApprovalPendingEvent(
                TenantId: tid,
                ApprovalId: entity.Id,
                AssistantId: assistantId,
                AssistantName: entity.AssistantName,
                ToolName: entity.ToolName,
                Reason: entity.ReasonHint,
                RequestingUserId: requestingUserId,
                ConversationId: conversationId,
                AgentTaskId: agentTaskId,
                ExpiresAt: expiresAt));
        }

        return entity;
    }

    public bool TryApprove(Guid decisionUserId, string? reason)
    {
        if (Status != PendingApprovalStatus.Pending) return false;
        Status = PendingApprovalStatus.Approved;
        DecisionUserId = decisionUserId;
        DecisionReason = reason?.Trim();
        DecidedAt = DateTime.UtcNow;
        ModifiedAt = DecidedAt;
        if (TenantId is { } tid)
            RaiseDomainEvent(new AgentApprovalApprovedEvent(
                tid, Id, AssistantId, AssistantName, ToolName,
                RequestingUserId, decisionUserId, DecisionReason, ConversationId));
        return true;
    }

    public bool TryDeny(Guid decisionUserId, string reason)
    {
        if (Status != PendingApprovalStatus.Pending) return false;
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("reason required.", nameof(reason));
        Status = PendingApprovalStatus.Denied;
        DecisionUserId = decisionUserId;
        DecisionReason = reason.Trim();
        DecidedAt = DateTime.UtcNow;
        ModifiedAt = DecidedAt;
        if (TenantId is { } tid)
            RaiseDomainEvent(new AgentApprovalDeniedEvent(
                tid, Id, AssistantId, AssistantName, ToolName,
                RequestingUserId, decisionUserId, DecisionReason!, ConversationId));
        return true;
    }

    public bool TryExpire()
    {
        if (Status != PendingApprovalStatus.Pending) return false;
        Status = PendingApprovalStatus.Expired;
        DecidedAt = DateTime.UtcNow;
        ModifiedAt = DecidedAt;
        if (TenantId is { } tid)
            RaiseDomainEvent(new AgentApprovalExpiredEvent(
                tid, Id, AssistantId, AssistantName, ToolName,
                RequestingUserId, DecidedAt!.Value));
        return true;
    }
}
