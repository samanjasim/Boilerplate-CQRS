using Starter.Domain.Common;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class EventRegistration : BaseEntity
{
    public string EventName { get; private set; } = default!;
    public string ModuleSource { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? AvailableRecipientsJson { get; private set; }
    public string? SamplePayloadJson { get; private set; }

    private EventRegistration() { }

    public static EventRegistration Create(string eventName, string moduleSource,
        string displayName, string? description = null,
        string? availableRecipientsJson = null, string? samplePayloadJson = null)
    {
        return new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventName = eventName,
            ModuleSource = moduleSource,
            DisplayName = displayName,
            Description = description,
            AvailableRecipientsJson = availableRecipientsJson,
            SamplePayloadJson = samplePayloadJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string displayName, string? description,
        string? availableRecipientsJson, string? samplePayloadJson)
    {
        DisplayName = displayName;
        Description = description;
        AvailableRecipientsJson = availableRecipientsJson;
        SamplePayloadJson = samplePayloadJson;
        ModifiedAt = DateTime.UtcNow;
    }
}
