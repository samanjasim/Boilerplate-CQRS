using System.Security.Cryptography;
using System.Text;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;

namespace EvalCacheWarmup;

/// <summary>
/// Decorator around <see cref="IReranker"/> that captures the reranked hybrid
/// scores so they can be serialised into a deterministic cache blob used during
/// offline eval testing.
/// </summary>
public sealed class CapturingReranker(IReranker inner) : IReranker
{
    /// <summary>
    /// All captured scores, keyed by <see cref="CacheKey(string, Guid)"/>.
    /// Populated after each call to <see cref="RerankAsync"/>.
    /// </summary>
    public Dictionary<string, decimal> Captured { get; } = new();

    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,
        CancellationToken ct)
    {
        var result = await inner.RerankAsync(query, candidates, candidateChunks, context, ct);

        // Capture hybrid scores from the reranked ordered list, keyed by a
        // deterministic hash of (query, chunkId) so the eval harness can look
        // up scores without needing to call the live reranker.
        foreach (var hit in result.Ordered)
            Captured[CacheKey(query, hit.ChunkId)] = hit.HybridScore;

        return result;
    }

    /// <summary>
    /// Returns a hex-encoded SHA-256 hash of "<paramref name="query"/>|<paramref name="chunkId"/>".
    /// Used as the dictionary key so that scores are query-and-chunk scoped.
    /// </summary>
    public static string CacheKey(string query, Guid chunkId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{query}|{chunkId}"), hash);
        return Convert.ToHexString(hash);
    }
}
