using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class AiErrors
{
    public static Error AssistantNotFound => Error.NotFound("Ai.AssistantNotFound", "AI assistant not found.");
    public static Error ConversationNotFound => Error.NotFound("Ai.ConversationNotFound", "Conversation not found.");
    public static Error DocumentNotFound => Error.NotFound("Ai.DocumentNotFound", "Document not found.");
    public static Error AgentTaskNotFound => Error.NotFound("Ai.AgentTaskNotFound", "Agent task not found.");
    public static Error TriggerNotFound => Error.NotFound("Ai.TriggerNotFound", "Agent trigger not found.");
    public static Error ToolNotFound => Error.NotFound("Ai.ToolNotFound", "AI tool not found.");
    public static Error QuotaExceeded(long limit) => new("Ai.QuotaExceeded", $"Monthly AI token quota exceeded. Limit: {limit:N0} tokens.", ErrorType.Forbidden);
    public static Error ProviderNotConfigured => Error.Validation("Ai.ProviderNotConfigured", "AI provider is not configured. Set API key in AI settings.");
    public static Error ProviderError(string message) => Error.Failure("Ai.ProviderError", $"AI provider returned an error: {message}");
    public static Error AgentStepLimitReached(int limit) => Error.Validation("Ai.AgentStepLimitReached", $"Agent reached the maximum step limit of {limit}.");
    public static Error AgentTaskAlreadyCompleted => Error.Validation("Ai.AgentTaskAlreadyCompleted", "Agent task has already completed.");
    public static Error DocumentProcessingFailed(string reason) => Error.Failure("Ai.DocumentProcessingFailed", $"Document processing failed: {reason}");
    public static Error AssistantNameAlreadyExists => Error.Conflict("Ai.AssistantNameAlreadyExists", "An assistant with this name already exists.");
    public static Error AssistantInUse => Error.Conflict(
        "Ai.AssistantInUse",
        "This assistant has conversations and cannot be deleted. Deactivate it instead.");
    public static Error ToolPermissionDenied(string toolName) => new("Ai.ToolPermissionDenied", $"You don't have permission to use the tool '{toolName}'.", ErrorType.Forbidden);
    public static Error AiNotEnabled => new("Ai.NotEnabled", "AI features are not enabled for this tenant.", ErrorType.Forbidden);
    public static Error NotAuthenticated => new("Ai.NotAuthenticated", "You must be signed in to chat.", ErrorType.Unauthorized);
}
