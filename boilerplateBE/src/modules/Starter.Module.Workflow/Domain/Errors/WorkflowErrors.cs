using Starter.Shared.Results;

namespace Starter.Module.Workflow.Domain.Errors;

public static class WorkflowErrors
{
    public static Error DefinitionNotFound(string name) =>
        Error.NotFound("Workflow.DefinitionNotFound", $"Workflow definition '{name}' not found");

    public static Error DefinitionNotFoundById(Guid id) =>
        Error.NotFound("Workflow.DefinitionNotFound", $"Workflow definition '{id}' not found");

    public static Error InstanceNotFound(Guid id) =>
        Error.NotFound("Workflow.InstanceNotFound", $"Workflow instance '{id}' not found");

    public static Error TaskNotFound(Guid id) =>
        Error.NotFound("Workflow.TaskNotFound", $"Approval task '{id}' not found");

    public static Error InvalidTransition(string currentState, string action) =>
        Error.Validation("Workflow.InvalidTransition", $"Action '{action}' is not valid from state '{currentState}'");

    public static Error TaskNotAssignedToUser(Guid taskId, Guid userId) =>
        Error.Forbidden($"Task '{taskId}' is not assigned to user '{userId}'");

    public static Error InstanceNotActive(Guid id) =>
        Error.Validation("Workflow.InstanceNotActive", $"Workflow instance '{id}' is not active");

    public static Error DefinitionNotActive(string name) =>
        Error.Validation("Workflow.DefinitionNotActive", $"Workflow definition '{name}' is not active");

    public static Error CannotEditTemplate() =>
        Error.Validation("Workflow.CannotEditTemplate", "System templates cannot be edited directly. Clone it first.");
}
