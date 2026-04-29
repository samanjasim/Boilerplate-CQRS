using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class EmbeddingService(
    IAiProviderFactory providerFactory,
    AiDbContext context,
    ICurrentUserService currentUser,
    IOptions<AiRagSettings> ragOptions,
    TokenCounter tokenCounter,
    IAiModelDefaultResolver modelDefaults,
    IAiProviderCredentialResolver providerCredentials) : IEmbeddingService
{
    private int _vectorSize = -1;
    public int VectorSize => _vectorSize > 0
        ? _vectorSize
        : throw new InvalidOperationException("Call EmbedAsync at least once before reading VectorSize.");

    public async Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null,
        AiRequestType requestType = AiRequestType.Embedding)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();

        var tenantId = attribution?.TenantId ?? currentUser.TenantId;
        var modelResult = await modelDefaults.ResolveAsync(
            tenantId,
            AiAgentClass.Embedding,
            explicitProvider: null,
            explicitModel: null,
            explicitTemperature: null,
            explicitMaxTokens: null,
            ct);
        if (modelResult.IsFailure)
            throw new InvalidOperationException(modelResult.Error.Description);

        var modelConfig = modelResult.Value;
        var credentialResult = await providerCredentials.ResolveAsync(tenantId, modelConfig.Provider, ct);
        if (credentialResult.IsFailure)
            throw new InvalidOperationException(credentialResult.Error.Description);

        var credential = credentialResult.Value;
        var provider = providerFactory.Create(modelConfig.Provider);
        var embeddingOptions = new AiEmbeddingOptions(
            Model: modelConfig.Model,
            ApiKey: credential.Secret,
            ProviderCredentialSource: credential.Source,
            ProviderCredentialId: credential.ProviderCredentialId);
        var batchSize = ragOptions.Value.EmbedBatchSize;
        var all = new List<float[]>(texts.Count);
        var totalTokens = 0;

        for (var offset = 0; offset < texts.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = texts.Skip(offset).Take(batchSize).ToList();
            var vectors = await EmbedBatchWithRetryAsync(provider, batch, embeddingOptions, ct);
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
                provider: modelConfig.Provider,
                model: modelConfig.Model,
                inputTokens: totalTokens,
                outputTokens: 0,
                estimatedCost: 0m,
                requestType: requestType,
                providerCredentialSource: credential.Source,
                providerCredentialId: credential.ProviderCredentialId);
            context.AiUsageLogs.Add(log);
            await context.SaveChangesAsync(ct);
        }

        return all.ToArray();
    }

    private static async Task<float[][]> EmbedBatchWithRetryAsync(
        IAiProvider provider,
        IReadOnlyList<string> batch,
        AiEmbeddingOptions options,
        CancellationToken ct)
    {
        var delays = new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(800) };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await provider.EmbedBatchAsync(batch, ct, options);
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
