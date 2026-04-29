namespace Starter.Module.Workflow.Application.DTOs;

public sealed record InboxStatusCountsDto(
    int Overdue,
    int DueToday,
    int Upcoming);
