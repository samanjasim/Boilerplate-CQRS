using System.Data.Common;
using System.Net.Http;
using Polly.CircuitBreaker;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

/// <summary>
/// Single source of truth for which exceptions <see cref="RagRetrievalService"/>
/// and the retrieval circuit breakers treat as transient dependency failures.
/// Programmer bugs (ArgumentException, InvalidOperationException, NullReferenceException,
/// ObjectDisposedException, etc.) are deliberately excluded so they fail the turn loudly
/// during development instead of being silently hidden as "degraded".
/// </summary>
internal static class RagTransientExceptionClassifier
{
    public static bool IsTransient(Exception ex) =>
        ex is HttpRequestException
           or TimeoutException
           or DbException
           or Grpc.Core.RpcException
           or TaskCanceledException
           or BrokenCircuitException;
}
