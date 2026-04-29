using Microsoft.Extensions.Configuration;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Resolves the OpenAI API key used by the moderation client. Tries the dedicated
/// moderation key first, then falls back to the existing chat-provider key. A null
/// return means no key is configured — caller registers <see cref="NoOpContentModerator"/>.
/// </summary>
internal interface IModerationKeyResolver
{
    string? Resolve();
}

internal sealed class ConfigurationModerationKeyResolver(IConfiguration configuration) : IModerationKeyResolver
{
    public string? Resolve()
    {
        var dedicated = configuration["AI:Moderation:OpenAi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(dedicated)) return dedicated;
        var fallback = configuration["AI:Providers:OpenAI:ApiKey"];
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }
}
