namespace Starter.Module.Workflow.Application.DTOs;

public sealed record InstanceStatusCountsDto(
    int Active,
    int Awaiting,
    int Completed,
    int Cancelled);
