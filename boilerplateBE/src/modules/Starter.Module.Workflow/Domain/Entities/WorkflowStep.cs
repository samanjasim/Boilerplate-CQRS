using Starter.Domain.Common;
using Starter.Module.Workflow.Domain.Enums;

namespace Starter.Module.Workflow.Domain.Entities;

public sealed class WorkflowStep : BaseEntity
{
    public Guid InstanceId { get; private set; }
    public string FromState { get; private set; } = default!;
    public string ToState { get; private set; } = default!;
    public StepType StepType { get; private set; }
    public string Action { get; private set; } = default!;
    public Guid? ActorUserId { get; private set; }
    public string? Comment { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime Timestamp { get; private set; }

    private WorkflowStep() { }

    private WorkflowStep(
        Guid id,
        Guid instanceId,
        string fromState,
        string toState,
        StepType stepType,
        string action,
        Guid? actorUserId,
        string? comment,
        string? metadataJson) : base(id)
    {
        InstanceId = instanceId;
        FromState = fromState;
        ToState = toState;
        StepType = stepType;
        Action = action;
        ActorUserId = actorUserId;
        Comment = comment;
        MetadataJson = metadataJson;
        Timestamp = DateTime.UtcNow;
    }

    public static WorkflowStep Create(
        Guid instanceId,
        string fromState,
        string toState,
        StepType stepType,
        string action,
        Guid? actorUserId,
        string? comment,
        string? metadataJson)
    {
        return new WorkflowStep(
            Guid.NewGuid(),
            instanceId,
            fromState,
            toState,
            stepType,
            action,
            actorUserId,
            comment,
            metadataJson);
    }
}
