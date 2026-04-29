using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiSecretProtectorTests
{
    [Fact]
    public void ProtectUnprotect_RoundTrip_Preserves_Secret()
    {
        var protector = new AiSecretProtector(new EphemeralDataProtectionProvider());
        const string secret = "sk-live-roundtrip-secret";

        var protectedSecret = protector.Protect(secret);
        var unprotectedSecret = protector.Unprotect(protectedSecret);

        protectedSecret.Should().StartWith("ai1:");
        protectedSecret.Should().NotContain(secret);
        unprotectedSecret.Should().Be(secret);
    }

    [Fact]
    public void Unprotect_Reads_Legacy_Unprefixed_Secret()
    {
        var provider = new EphemeralDataProtectionProvider();
        var legacyProtector = provider.CreateProtector("Starter.Module.AI.ProviderCredentials.v1");
        var protector = new AiSecretProtector(provider);
        const string secret = "sk-live-legacy-secret";
        var storedSecret = legacyProtector.Protect(secret);

        var unprotectedSecret = protector.Unprotect(storedSecret);

        unprotectedSecret.Should().Be(secret);
    }
}
