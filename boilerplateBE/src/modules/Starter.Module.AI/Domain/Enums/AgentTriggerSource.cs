namespace Starter.Module.AI.Domain.Enums;

/// <summary>
/// Origin of an agent task: User = manual, Schedule = cron trigger, Event = domain event trigger.
/// </summary>
public enum AgentTriggerSource { User, Schedule, Event }
