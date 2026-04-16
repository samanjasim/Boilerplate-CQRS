using Starter.Domain.Common;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class MessageTemplate : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string ModuleSource { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? SubjectTemplate { get; private set; }
    public string BodyTemplate { get; private set; } = default!;
    public NotificationChannel DefaultChannel { get; private set; }
    public string AvailableChannelsJson { get; private set; } = default!;
    public string? VariableSchemaJson { get; private set; }
    public string? SampleVariablesJson { get; private set; }
    public bool IsSystem { get; private set; }

    private MessageTemplate() { }

    private MessageTemplate(Guid id, string name, string moduleSource, string category,
        string? description, string? subjectTemplate, string bodyTemplate,
        NotificationChannel defaultChannel, string availableChannelsJson,
        string? variableSchemaJson, string? sampleVariablesJson, bool isSystem) : base(id)
    {
        Name = name;
        ModuleSource = moduleSource;
        Category = category;
        Description = description;
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        DefaultChannel = defaultChannel;
        AvailableChannelsJson = availableChannelsJson;
        VariableSchemaJson = variableSchemaJson;
        SampleVariablesJson = sampleVariablesJson;
        IsSystem = isSystem;
    }

    public static MessageTemplate Create(string name, string moduleSource, string category,
        string? description, string? subjectTemplate, string bodyTemplate,
        NotificationChannel defaultChannel, string availableChannelsJson,
        string? variableSchemaJson = null, string? sampleVariablesJson = null, bool isSystem = true)
    {
        return new MessageTemplate(Guid.NewGuid(), name, moduleSource, category,
            description, subjectTemplate, bodyTemplate, defaultChannel, availableChannelsJson,
            variableSchemaJson, sampleVariablesJson, isSystem);
    }

    public void Update(string? description, string? subjectTemplate, string bodyTemplate,
        NotificationChannel defaultChannel, string availableChannelsJson,
        string? variableSchemaJson, string? sampleVariablesJson)
    {
        Description = description;
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        DefaultChannel = defaultChannel;
        AvailableChannelsJson = availableChannelsJson;
        VariableSchemaJson = variableSchemaJson;
        SampleVariablesJson = sampleVariablesJson;
        ModifiedAt = DateTime.UtcNow;
    }
}
