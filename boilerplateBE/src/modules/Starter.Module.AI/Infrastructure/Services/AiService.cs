using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiService(
    AiProviderFactory providerFactory,
    IConfiguration configuration,
    ILogger<AiService> logger) : IAiService
{
    private AiProviderType EmbeddingProvider =>
        Enum.TryParse<AiProviderType>(configuration["AI:EmbeddingProvider"], ignoreCase: true, out var p)
            ? p
            : AiProviderType.OpenAI;

    public async Task<AiCompletionResult?> CompleteAsync(
        string prompt, AiCompletionOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            var provider = providerFactory.CreateDefault();
            var messages = new List<AiChatMessage>
            {
                new("user", prompt)
            };

            var chatOptions = new AiChatOptions(
                Model: options?.Model ?? string.Empty,
                Temperature: options?.Temperature ?? 0.7,
                MaxTokens: options?.MaxTokens ?? 4096);

            var result = await provider.ChatAsync(messages, chatOptions, ct);
            var tokensUsed = result.InputTokens + result.OutputTokens;

            return new AiCompletionResult(result.Content ?? string.Empty, tokensUsed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI completion failed for prompt of length {Length}", prompt.Length);
            return null;
        }
    }

    public async Task<string?> SummarizeAsync(
        string content, string? instructions = null, CancellationToken ct = default)
    {
        try
        {
            var prompt = instructions is null
                ? $"Please summarize the following content concisely:\n\n{content}"
                : $"{instructions}\n\nContent to summarize:\n\n{content}";

            var result = await CompleteAsync(prompt, new AiCompletionOptions(Temperature: 0.3), ct);
            return result?.Content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI summarization failed for content of length {Length}", content.Length);
            return null;
        }
    }

    public async Task<AiClassificationResult?> ClassifyAsync(
        string content, IReadOnlyList<string> categories, CancellationToken ct = default)
    {
        try
        {
            var categoriesList = string.Join(", ", categories);
            var prompt = $"""
                Classify the following content into exactly one of these categories: {categoriesList}

                Respond with only the category name, nothing else.

                Content:
                {content}
                """;

            var result = await CompleteAsync(prompt, new AiCompletionOptions(Temperature: 0.0), ct);
            if (result is null) return null;

            var responseText = result.Content.Trim();
            var matchedCategory = categories
                .FirstOrDefault(c => c.Equals(responseText, StringComparison.OrdinalIgnoreCase))
                ?? responseText;

            return new AiClassificationResult(matchedCategory, 1.0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI classification failed for content of length {Length}", content.Length);
            return null;
        }
    }

    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var provider = providerFactory.Create(EmbeddingProvider);
            return await provider.EmbedAsync(text, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI embedding failed for text of length {Length}", text.Length);
            return null;
        }
    }
}
