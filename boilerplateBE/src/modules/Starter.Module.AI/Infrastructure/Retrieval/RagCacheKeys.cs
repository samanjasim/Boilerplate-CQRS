using System.Security.Cryptography;
using System.Text;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

/// <summary>
/// Centralized key-factory for RAG Redis caches. Every key follows
/// <c>ai:{stage}:{tenant}:{provider}:{model}[:{extra}]:{sha256(payload)}</c>
/// so ops can namespace, monitor, and purge per stage. The tenant segment is
/// mandatory on stages whose payloads can carry tenant-identifiable text
/// (classify/contextualize/query-rewrite are derived from user messages;
/// listwise-rerank includes chunk IDs that are themselves tenant-scoped).
/// Embedding caches tenant-scope via the CachingEmbeddingService directly.
/// </summary>
internal static class RagCacheKeys
{
    public static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Classify(Guid tenantId, string provider, string model, string normalizedQuery)
        => $"ai:classify:{tenantId:N}:{provider}:{model}:{Sha256Hex(normalizedQuery)}";

    public static string QueryRewrite(Guid tenantId, string provider, string model, string language, string normalizedQuery)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "-" : language;
        return $"ai:qrw:{tenantId:N}:{provider}:{model}:{lang}:{Sha256Hex(normalizedQuery)}";
    }

    public static string ListwiseRerank(Guid tenantId, string provider, string model, string query, IEnumerable<HybridHit> candidates)
    {
        var ids = string.Join("|", candidates.Select(c => c.ChunkId.ToString("N")).OrderBy(s => s));
        return $"ai:rerank:lw:{tenantId:N}:{provider}:{model}:{Sha256Hex($"{query}|{ids}")}";
    }

    public static string PointwiseRerank(Guid tenantId, string provider, string model, string query, Guid chunkId)
        => $"ai:rerank:pw:{tenantId:N}:{provider}:{model}:{Sha256Hex(query)}:{chunkId:N}";

    public static string Contextualize(Guid tenantId, string provider, string model, string language, string normalizedPayload)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "-" : language;
        return $"ai:ctx:{tenantId:N}:{provider}:{model}:{lang}:{Sha256Hex(normalizedPayload)}";
    }
}
