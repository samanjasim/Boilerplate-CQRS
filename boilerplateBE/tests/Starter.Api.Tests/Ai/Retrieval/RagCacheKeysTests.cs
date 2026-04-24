using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class RagCacheKeysTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Contextualize_produces_namespaced_sha256_key()
    {
        var key = RagCacheKeys.Contextualize(Tenant, "OpenAI", "gpt-4o-mini", "en", "user:what is qdrant?\nassistant:qdrant is...\n---how do we configure it?");
        var prefix = $"ai:ctx:{Tenant:N}:OpenAI:gpt-4o-mini:en:";
        key.Should().StartWith(prefix);
        key.Length.Should().Be(prefix.Length + 64); // sha256 hex
    }

    [Fact]
    public void Contextualize_blank_language_defaults_to_dash()
    {
        var key = RagCacheKeys.Contextualize(Tenant, "OpenAI", "gpt-4o-mini", "", "payload");
        key.Should().MatchRegex($"^ai:ctx:{Tenant:N}:OpenAI:gpt-4o-mini:-:[a-f0-9]{{64}}$");
    }

    [Fact]
    public void Contextualize_is_deterministic_for_same_payload()
    {
        var a = RagCacheKeys.Contextualize(Tenant, "OpenAI", "gpt-4o-mini", "en", "same-payload");
        var b = RagCacheKeys.Contextualize(Tenant, "OpenAI", "gpt-4o-mini", "en", "same-payload");
        a.Should().Be(b);
    }

    [Fact]
    public void Contextualize_is_tenant_scoped()
    {
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var a = RagCacheKeys.Contextualize(Tenant, "OpenAI", "gpt-4o-mini", "en", "same-payload");
        var b = RagCacheKeys.Contextualize(other,  "OpenAI", "gpt-4o-mini", "en", "same-payload");
        a.Should().NotBe(b);
    }
}
