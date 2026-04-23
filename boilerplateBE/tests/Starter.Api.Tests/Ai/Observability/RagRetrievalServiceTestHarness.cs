using Starter.Module.AI.Infrastructure.Retrieval;

namespace Starter.Api.Tests.Ai.Observability;

internal static class RagRetrievalServiceTestHarness
{
    public static Task<T?> RunWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> op,
        int timeoutMs,
        string stageName,
        List<string> degraded,
        CancellationToken ct = default) where T : class
        => RagRetrievalService.RunWithTimeoutAsyncForTests(op, timeoutMs, stageName, degraded, ct);
}
