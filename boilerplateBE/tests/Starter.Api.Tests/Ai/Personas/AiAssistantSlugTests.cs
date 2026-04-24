using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AiAssistantSlugTests
{
    [Fact]
    public void Create_Accepts_Explicit_Slug()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Brand Content Agent",
            description: null,
            systemPrompt: "prompt",
            createdByUserId: Guid.NewGuid(),
            slug: "brand-content-agent");

        a.Slug.Should().Be("brand-content-agent");
        a.PersonaTargetSlugs.Should().BeEmpty();
    }

    [Fact]
    public void Create_Without_Slug_Defaults_To_Empty()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Brand Content Agent",
            description: null,
            systemPrompt: "prompt",
            createdByUserId: Guid.NewGuid());

        a.Slug.Should().Be("");
    }

    [Fact]
    public void SetSlug_Normalises_Casing_And_Trim()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "X", null, "prompt", Guid.NewGuid());
        a.SetSlug("  Brand-Content-AGENT  ");
        a.Slug.Should().Be("brand-content-agent");
    }

    [Fact]
    public void SetPersonaTargets_Dedups_And_Normalises()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "X", null, "prompt", Guid.NewGuid());
        a.SetPersonaTargets(new[] { "Student", "student ", "TEACHER", "   ", null, "teacher" });
        a.PersonaTargetSlugs.Should().BeEquivalentTo(new[] { "student", "teacher" });
    }

    [Fact]
    public void IsVisibleToPersona_Both_Empty_Means_Visible()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "X", null, "prompt", Guid.NewGuid(), slug: "tutor");
        a.IsVisibleToPersona("student", new List<string>()).Should().BeTrue();
    }

    [Fact]
    public void IsVisibleToPersona_Excluded_By_Agent_Returns_False()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "X", null, "prompt", Guid.NewGuid(), slug: "tutor");
        a.SetPersonaTargets(new[] { "teacher" });
        a.IsVisibleToPersona("student", new List<string>()).Should().BeFalse();
    }

    [Fact]
    public void IsVisibleToPersona_Excluded_By_Persona_Returns_False()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "X", null, "prompt", Guid.NewGuid(), slug: "admin-copilot");
        a.IsVisibleToPersona("student", new List<string> { "tutor" }).Should().BeFalse();
    }
}
