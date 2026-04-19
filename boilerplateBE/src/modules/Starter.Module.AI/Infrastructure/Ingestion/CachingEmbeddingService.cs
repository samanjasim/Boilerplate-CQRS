using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class CachingEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _inner;
    private readonly ICacheService _cache;
    private readonly IAiProviderFactory _providerFactory;
    private readonly AiRagSettings _settings;

    // Scoped-per-request single writer; do not promote to singleton without adding synchronization.
    private int _vectorSize = -1;

    public CachingEmbeddingService(
        IEmbeddingService inner,
        ICacheService cache,
        IAiProviderFactory providerFactory,
        IOptions<AiRagSettings> settings)
    {
        _inner = inner;
        _cache = cache;
        _providerFactory = providerFactory;
        _settings = settings.Value;
    }

    public int VectorSize => _vectorSize > 0 ? _vectorSize : _inner.VectorSize;

    public async Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null,
        AiRequestType requestType = AiRequestType.Embedding)
    {
        if (texts.Count != 1)
            return await _inner.EmbedAsync(texts, ct, attribution, requestType);

        var key = BuildKey(_providerFactory.GetEmbeddingModelId(), texts[0]);
        var cached = await _cache.GetAsync<float[]>(key, ct);
        if (cached is not null && cached.Length > 0)
        {
            if (_vectorSize < 0) _vectorSize = cached.Length;
            return [cached];
        }

        var result = await _inner.EmbedAsync(texts, ct, attribution, requestType);
        if (result.Length == 1 && result[0].Length > 0 && _settings.EmbeddingCacheTtlSeconds > 0)
        {
            _vectorSize = result[0].Length;
            await _cache.SetAsync(key, result[0], TimeSpan.FromSeconds(_settings.EmbeddingCacheTtlSeconds), ct);
        }
        else if (result.Length == 1 && result[0].Length > 0)
        {
            _vectorSize = result[0].Length;
        }
        return result;
    }

    private static string BuildKey(string modelId, string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return "ai:embed:" + modelId + ":" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
