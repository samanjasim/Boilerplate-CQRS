namespace Starter.Module.AI.Infrastructure.Observability;

internal static class RagWebhookEventNames
{
    public const string Completed = "ai.retrieval.completed";
    public const string Degraded  = "ai.retrieval.degraded";
    public const string Failed    = "ai.retrieval.failed";
}
