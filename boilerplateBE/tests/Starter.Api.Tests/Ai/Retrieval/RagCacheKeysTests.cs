using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class RagCacheKeysTests
{
    [Fact]
    public void Contextualize_produces_namespaced_sha256_key()
    {
        var key = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "en", "user:what is qdrant?\nassistant:qdrant is...\n---how do we configure it?");
        key.Should().StartWith("ai:ctx:OpenAI:gpt-4o-mini:en:");
        key.Length.Should().Be("ai:ctx:OpenAI:gpt-4o-mini:en:".Length + 64); // sha256 hex
    }

    [Fact]
    public void Contextualize_blank_language_defaults_to_dash()
    {
        var key = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "", "payload");
        key.Should().Contain(":-:");
    }

    [Fact]
    public void Contextualize_is_deterministic_for_same_payload()
    {
        var a = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "en", "same-payload");
        var b = RagCacheKeys.Contextualize("OpenAI", "gpt-4o-mini", "en", "same-payload");
        a.Should().Be(b);
    }
}
