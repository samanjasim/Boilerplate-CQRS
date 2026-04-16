using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Starter.Module.Communication.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Communication.Infrastructure;

public sealed class CredentialEncryptionServiceTests
{
    private readonly CredentialEncryptionService _sut = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void EncryptDecrypt_RoundTrip_PreservesValues()
    {
        var original = new Dictionary<string, string>
        {
            ["apiKey"] = "sk_live_abcdef1234567890",
            ["webhookSecret"] = "whsec_xyz_789",
        };

        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted);

        encrypted.Should().NotContain("sk_live_abcdef1234567890");
        decrypted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Mask_LongValue_KeepsLastFourCharacters()
    {
        var creds = new Dictionary<string, string> { ["apiKey"] = "sk_live_abcdef1234567890" };

        var masked = _sut.Mask(creds);

        masked["apiKey"].Should().Be("****7890");
    }

    [Fact]
    public void Mask_ShortValue_RedactsEntirely()
    {
        // <= 4 chars: not enough entropy to expose any suffix safely.
        var creds = new Dictionary<string, string> { ["pin"] = "1234" };

        _sut.Mask(creds)["pin"].Should().Be("****");
    }

    [Fact]
    public void Mask_EmptyValue_RedactsEntirely()
    {
        var creds = new Dictionary<string, string> { ["apiKey"] = "" };

        _sut.Mask(creds)["apiKey"].Should().Be("****");
    }
}
