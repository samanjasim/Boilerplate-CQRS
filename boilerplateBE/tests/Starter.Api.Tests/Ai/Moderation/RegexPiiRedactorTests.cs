using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class RegexPiiRedactorTests
{
    private static ResolvedSafetyProfile Profile(bool redact) => new(
        SafetyPreset.ProfessionalModerated, ModerationProvider.OpenAi,
        new Dictionary<string, double>(), Array.Empty<string>(),
        ModerationFailureMode.FailClosed, redact);

    private static IPiiRedactor Make() => new RegexPiiRedactor(NullLogger<RegexPiiRedactor>.Instance);

    [Fact]
    public async Task NoOp_When_Profile_Disables_Redaction()
    {
        var r = await Make().RedactAsync("contact me at a@b.com", Profile(redact: false), default);
        r.Outcome.Should().Be(ModerationOutcome.Allowed);
        r.Text.Should().Contain("a@b.com");
    }

    [Fact]
    public async Task Redacts_Email_Phone_And_Reports_Hits()
    {
        var input = "email a@b.com call +14155552671";
        var r = await Make().RedactAsync(input, Profile(redact: true), default);
        r.Outcome.Should().Be(ModerationOutcome.Redacted);
        r.Text.Should().NotContain("a@b.com");
        r.Text.Should().NotContain("+14155552671");
        r.Hits.Should().ContainKey("pii-email");
        r.Hits.Should().ContainKey("pii-phone");
    }

    [Fact]
    public async Task Card_Number_Redacted_Only_When_Luhn_Valid()
    {
        // Visa test number "4111 1111 1111 1111" — Luhn valid.
        var goodInput = "card 4111 1111 1111 1111";
        var bad = "card 4111 1111 1111 1112"; // not Luhn valid

        var rGood = await Make().RedactAsync(goodInput, Profile(redact: true), default);
        var rBad = await Make().RedactAsync(bad, Profile(redact: true), default);

        rGood.Hits.Should().ContainKey("pii-card");
        rBad.Hits.Should().NotContainKey("pii-card");
    }

    [Fact]
    public async Task SSN_Format_Redacted()
    {
        var r = await Make().RedactAsync("ssn 123-45-6789", Profile(redact: true), default);
        r.Hits.Should().ContainKey("pii-ssn");
        r.Text.Should().NotContain("123-45-6789");
    }

    [Fact]
    public async Task Iban_Redacted()
    {
        var r = await Make().RedactAsync("iban GB29NWBK60161331926819", Profile(redact: true), default);
        r.Hits.Should().ContainKey("pii-iban");
    }
}
