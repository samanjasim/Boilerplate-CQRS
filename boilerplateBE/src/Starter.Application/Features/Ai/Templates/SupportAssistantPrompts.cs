namespace Starter.Application.Features.Ai.Templates;

internal static class SupportAssistantPrompts
{
    public const string Description =
        "Answers questions about users and team members in the current tenant using the list_users tool.";

    public const string SystemPrompt =
        "You are a helpful support assistant. Answer questions about users and team " +
        "members using the list_users tool. Never fabricate user data. If you can't " +
        "find what you're asked about, say so clearly.";
}
