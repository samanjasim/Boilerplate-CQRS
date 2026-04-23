namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Thin AI capability for cross-module AI features.
/// Modules inject this to get AI-enhanced functionality (summarization, classification, etc.)
/// without depending on the AI module directly.
/// When AI module is absent, NullAiService returns null/empty for all methods.
/// </summary>
public interface IAiService : ICapability
{
    /// <summary>Generate a text completion from a prompt.</summary>
    Task<AiCompletionResult?> CompleteAsync(
        string prompt, AiCompletionOptions? options = null, CancellationToken ct = default);

    /// <summary>Summarize content with optional instructions.</summary>
    Task<string?> SummarizeAsync(
        string content, string? instructions = null, CancellationToken ct = default);

    /// <summary>Classify text into one of the provided categories.</summary>
    Task<AiClassificationResult?> ClassifyAsync(
        string content, IReadOnlyList<string> categories, CancellationToken ct = default);

    /// <summary>Generate embedding vector for text.</summary>
    Task<float[]?> EmbedAsync(string text, CancellationToken ct = default);
}

public sealed record AiCompletionResult(string Content, int TokensUsed);

public sealed record AiClassificationResult(string Category, double Confidence);

public sealed record AiCompletionOptions(
    string? Model = null,
    double? Temperature = null,
    int? MaxTokens = null);
