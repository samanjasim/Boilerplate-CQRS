using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class EmbeddingService(
    AiProviderFactory providerFactory,
    AiDbContext context,
    ICurrentUserService currentUser,
    IOptions<AiRagSettings> ragOptions,
    TokenCounter tokenCounter) : IEmbeddingService
{
    private int _vectorSize = -1;
    public int VectorSize => _vectorSize > 0
        ? _vectorSize
        : throw new InvalidOperationException("Call EmbedAsync at least once before reading VectorSize.");

    public async Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();

        var provider = providerFactory.CreateDefault();
        var providerType = providerFactory.GetDefaultProviderType();
        var batchSize = ragOptions.Value.EmbedBatchSize;
        var all = new List<float[]>(texts.Count);
        var totalTokens = 0;

        for (var offset = 0; offset < texts.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = texts.Skip(offset).Take(batchSize).ToList();
            var vectors = await EmbedBatchWithRetryAsync(provider, batch, ct);
            all.AddRange(vectors);
            totalTokens += batch.Sum(tokenCounter.Count);
        }

        if (_vectorSize < 0 && all.Count > 0) _vectorSize = all[0].Length;

        var (logTenant, logUser) = attribution is { } a
            ? (a.TenantId, (Guid?)a.UserId)
            : (currentUser.TenantId, currentUser.UserId);

        if (logUser is Guid uid)
        {
            var log = AiUsageLog.Create(
                tenantId: logTenant,
                userId: uid,
                provider: providerType,
                model: "embedding",
                inputTokens: totalTokens,
                outputTokens: 0,
                estimatedCost: 0m,
                requestType: AiRequestType.Embedding);
            context.AiUsageLogs.Add(log);
            await context.SaveChangesAsync(ct);
        }

        return all.ToArray();
    }

    private static async Task<float[][]> EmbedBatchWithRetryAsync(
        IAiProvider provider, IReadOnlyList<string> batch, CancellationToken ct)
    {
        var delays = new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(800) };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await provider.EmbedBatchAsync(batch, ct);
            }
            catch (Exception ex) when (attempt < delays.Length && IsTransient(ex))
            {
                await Task.Delay(delays[attempt], ct);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException;
}
