namespace Starter.Module.Workflow.Domain.Enums;

public enum StateType
{
    Initial = 0,
    HumanTask = 1,
    SystemAction = 2,
    ConditionalGate = 3,
    Terminal = 4
}
