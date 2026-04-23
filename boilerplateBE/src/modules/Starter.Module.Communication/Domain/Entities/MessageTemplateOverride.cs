using Starter.Domain.Common;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class MessageTemplateOverride : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid MessageTemplateId { get; private set; }
    public string? SubjectTemplate { get; private set; }
    public string BodyTemplate { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private MessageTemplateOverride() { }

    private MessageTemplateOverride(Guid id, Guid tenantId, Guid messageTemplateId,
        string? subjectTemplate, string bodyTemplate) : base(id)
    {
        TenantId = tenantId;
        MessageTemplateId = messageTemplateId;
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        IsActive = true;
    }

    public static MessageTemplateOverride Create(Guid tenantId, Guid messageTemplateId,
        string? subjectTemplate, string bodyTemplate)
    {
        return new MessageTemplateOverride(Guid.NewGuid(), tenantId, messageTemplateId,
            subjectTemplate, bodyTemplate);
    }

    public void Update(string? subjectTemplate, string bodyTemplate)
    {
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate() { IsActive = true; ModifiedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; ModifiedAt = DateTime.UtcNow; }
}
