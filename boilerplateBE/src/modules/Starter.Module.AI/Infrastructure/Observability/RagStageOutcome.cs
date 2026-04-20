namespace Starter.Module.AI.Infrastructure.Observability;

/// Tag values for the rag.outcome dimension. Enumerated up-front to avoid
/// cardinality explosions from dynamic strings.
internal static class RagStageOutcome
{
    public const string Success = "success";
    public const string Timeout = "timeout";
    public const string Error = "error";
}
