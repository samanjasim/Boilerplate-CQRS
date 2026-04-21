using System.Security.Cryptography;
using System.Text;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

/// <summary>
/// Centralized key-factory for RAG Redis caches. Every key follows
/// <c>ai:{stage}:{provider}:{model}[:{extra}]:{sha256(payload)}</c> so ops can
/// namespace, monitor, and purge per stage.
/// </summary>
internal static class RagCacheKeys
{
    public static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Classify(string provider, string model, string normalizedQuery)
        => $"ai:classify:{provider}:{model}:{Sha256Hex(normalizedQuery)}";

    public static string QueryRewrite(string provider, string model, string language, string normalizedQuery)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "-" : language;
        return $"ai:qrw:{provider}:{model}:{lang}:{Sha256Hex(normalizedQuery)}";
    }

    public static string ListwiseRerank(string provider, string model, string query, IEnumerable<HybridHit> candidates)
    {
        var ids = string.Join("|", candidates.Select(c => c.ChunkId.ToString("N")).OrderBy(s => s));
        return $"ai:rerank:lw:{provider}:{model}:{Sha256Hex($"{query}|{ids}")}";
    }

    public static string PointwiseRerank(string provider, string model, string query, Guid chunkId)
        => $"ai:rerank:pw:{provider}:{model}:{Sha256Hex(query)}:{chunkId:N}";

    public static string Contextualize(string provider, string model, string language, string normalizedPayload)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "-" : language;
        return $"ai:ctx:{provider}:{model}:{lang}:{Sha256Hex(normalizedPayload)}";
    }
}
