using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;
using Xunit.Abstractions;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 W1 — OpenAI Moderation wire-compat (gated).
///
/// Mirrors the RAG eval harness gating pattern (<c>AI_EVAL_ENABLED=1</c>): runs
/// only when <c>MODERATION_LIVE_TESTS=1</c> is set AND a moderation key is
/// configured (via <c>dotnet user-secrets</c> on Starter.Api or env var). In any
/// other case the test emits a skip reason via <see cref="ITestOutputHelper"/>
/// and returns silently — so CI executes it as a no-op until ops opts the gate
/// on (e.g. a nightly job to catch OpenAI API drift).
///
/// Asserts that submitting deliberately category-targeting strings causes the
/// OpenAI Moderations API to populate <see cref="ModerationVerdict.Categories"/>
/// (i.e. produce a non-empty score map). This catches schema/wire changes
/// from upstream without re-running on every PR.
/// </summary>
public sealed class Plan5d2OpenAiModerationWireTests
{
    private readonly ITestOutputHelper _output;

    public Plan5d2OpenAiModerationWireTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Wire_Compat_Live_Call_Returns_Expected_Categories()
    {
        if (Environment.GetEnvironmentVariable("MODERATION_LIVE_TESTS") != "1")
        {
            _output.WriteLine("Skipped: set MODERATION_LIVE_TESTS=1 to run.");
            return;
        }

        var config = new ConfigurationBuilder()
            .AddUserSecrets<Starter.Api.Program>()
            .AddEnvironmentVariables()
            .Build();
        var resolver = new ConfigurationModerationKeyResolver(config);
        if (string.IsNullOrWhiteSpace(resolver.Resolve()))
        {
            _output.WriteLine("Skipped: no moderation key configured in user-secrets / env.");
            return;
        }

        var http = new HttpClientFactory();
        var moderator = new OpenAiContentModerator(resolver, http, NullLogger<OpenAiContentModerator>.Instance);
        var profile = new ResolvedSafetyProfile(
            Starter.Abstractions.Ai.SafetyPreset.Standard,
            ModerationProvider.OpenAi,
            new Dictionary<string, double>
            {
                ["sexual"] = 0.85,
                ["violence"] = 0.85,
                ["hate"] = 0.85,
            },
            Array.Empty<string>(),
            ModerationFailureMode.FailOpen,
            RedactPii: false);

        // Deliberately category-targeting strings drawn from OpenAI's published
        // moderation examples — each phrase aims at a distinct harm category so
        // a wire/schema regression on any one of them surfaces a non-empty
        // Categories dictionary (the contract this test pins down).
        var v1 = await moderator.ScanAsync(
            "I want to kill them.", ModerationStage.Output, profile, "en", default);
        v1.Categories.Should().NotBeEmpty(
            because: "OpenAI Moderations should return per-category scores for a violence-targeting string");

        var v2 = await moderator.ScanAsync(
            "I hate that group of people and they should be eliminated.", ModerationStage.Output, profile, "en", default);
        v2.Categories.Should().NotBeEmpty(
            because: "OpenAI Moderations should return per-category scores for a hate-targeting string");

        var v3 = await moderator.ScanAsync(
            "Explicit sexual content involving adults.", ModerationStage.Output, profile, "en", default);
        v3.Categories.Should().NotBeEmpty(
            because: "OpenAI Moderations should return per-category scores for a sexual-targeting string");
    }

    /// <summary>
    /// Minimal one-shot <see cref="IHttpClientFactory"/> for the live test.
    /// Connection pooling/leasing concerns don't apply for an isolated, gated
    /// test that fires three calls and exits.
    /// </summary>
    private sealed class HttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
