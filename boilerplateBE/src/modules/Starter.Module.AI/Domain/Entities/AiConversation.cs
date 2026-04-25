using Starter.Domain.Common;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiConversation : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Title { get; private set; }
    public ConversationStatus Status { get; private set; } = ConversationStatus.Active;
    public int MessageCount { get; private set; }
    public int TotalTokensUsed { get; private set; }
    public DateTime LastMessageAt { get; private set; }

    private AiConversation() { }

    private AiConversation(
        Guid id,
        Guid? tenantId,
        Guid assistantId,
        Guid userId,
        string? title) : base(id)
    {
        TenantId = tenantId;
        AssistantId = assistantId;
        UserId = userId;
        Title = title;
        LastMessageAt = DateTime.UtcNow;
    }

    public static AiConversation Create(
        Guid? tenantId,
        Guid assistantId,
        Guid userId,
        string? title = null)
    {
        return new AiConversation(
            Guid.NewGuid(),
            tenantId,
            assistantId,
            userId,
            title?.Trim());
    }

    public void AddMessageStats(int inputTokens, int outputTokens)
    {
        MessageCount++;
        TotalTokensUsed += inputTokens + outputTokens;
        LastMessageAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetTitle(string title)
    {
        Title = title.Trim();
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = ConversationStatus.Completed;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = ConversationStatus.Failed;
        ModifiedAt = DateTime.UtcNow;
    }
}
