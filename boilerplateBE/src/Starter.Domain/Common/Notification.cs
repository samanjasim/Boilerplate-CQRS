namespace Starter.Domain.Common;

public sealed class Notification : BaseEntity, ITenantEntity
{
    public Guid UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public string? Data { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification() { }

    private Notification(Guid id) : base(id) { }

    public static Notification Create(
        Guid userId,
        Guid? tenantId,
        string type,
        string title,
        string message,
        string? data = null)
    {
        return new Notification(Guid.NewGuid())
        {
            UserId = userId,
            TenantId = tenantId,
            Type = type,
            Title = title,
            Message = message,
            Data = data,
            IsRead = false
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
