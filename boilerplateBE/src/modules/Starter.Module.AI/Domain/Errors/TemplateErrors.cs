using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class TemplateErrors
{
    public static Error NotFound(string slug) => new(
        "Template.NotFound",
        $"AI agent template '{slug}' is not registered.",
        ErrorType.NotFound);

    public static Error AlreadyInstalled(string slug, Guid tenantId) => new(
        "Template.AlreadyInstalled",
        $"Template '{slug}' is already installed in tenant {tenantId}.",
        ErrorType.Conflict);

    public static Error PersonaTargetMissing(string personaSlug) => new(
        "Template.PersonaTargetMissing",
        $"Template references persona '{personaSlug}' which does not exist in the target tenant.",
        ErrorType.Validation);

    public static Error ToolMissing(string toolName) => new(
        "Template.ToolMissing",
        $"Template references tool '{toolName}' which is not in the tool registry.",
        ErrorType.Validation);

    public static Error Forbidden() => new(
        "Template.Forbidden",
        "Cross-tenant install requires superadmin role.",
        ErrorType.Forbidden);
}
