using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IAiService"/> registered when the
/// AI module is not installed. All methods return null/empty so callers
/// need no module-awareness checks.
/// </summary>
public sealed class NullAiService(ILogger<NullAiService> logger) : IAiService
{
    public Task<AiCompletionResult?> CompleteAsync(
        string prompt, AiCompletionOptions? options = null, CancellationToken ct = default)
    {
        logger.LogDebug("AI completion skipped — AI module not installed");
        return Task.FromResult<AiCompletionResult?>(null);
    }

    public Task<string?> SummarizeAsync(
        string content, string? instructions = null, CancellationToken ct = default)
    {
        logger.LogDebug("AI summarization skipped — AI module not installed");
        return Task.FromResult<string?>(null);
    }

    public Task<AiClassificationResult?> ClassifyAsync(
        string content, IReadOnlyList<string> categories, CancellationToken ct = default)
    {
        logger.LogDebug("AI classification skipped — AI module not installed");
        return Task.FromResult<AiClassificationResult?>(null);
    }

    public Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        logger.LogDebug("AI embedding skipped — AI module not installed");
        return Task.FromResult<float[]?>(null);
    }
}
