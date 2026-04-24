using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class PersonaAssistantVisibilityTests
{
    private static AiAssistant MakeAssistant(string slug, params string[] personaTargets)
    {
        var a = AiAssistant.Create(Guid.NewGuid(), slug, null, "prompt", Guid.NewGuid(), slug: slug);
        if (personaTargets.Length > 0) a.SetPersonaTargets(personaTargets);
        return a;
    }

    [Fact]
    public void Both_Empty_Means_Visible()
    {
        MakeAssistant("tutor")
            .IsVisibleToPersona("student", new List<string>())
            .Should().BeTrue();
    }

    [Fact]
    public void Persona_Permits_Assistant_Returns_True()
    {
        MakeAssistant("tutor")
            .IsVisibleToPersona("student", new List<string> { "tutor", "reading-coach" })
            .Should().BeTrue();
    }

    [Fact]
    public void Persona_Excludes_Assistant_Returns_False()
    {
        MakeAssistant("admin-copilot")
            .IsVisibleToPersona("student", new List<string> { "tutor", "reading-coach" })
            .Should().BeFalse();
    }

    [Fact]
    public void Assistant_Targets_Persona_Returns_True()
    {
        MakeAssistant("tutor", "student")
            .IsVisibleToPersona("student", new List<string>())
            .Should().BeTrue();
    }

    [Fact]
    public void Assistant_Excludes_Persona_Returns_False()
    {
        MakeAssistant("tutor", "teacher")
            .IsVisibleToPersona("student", new List<string>())
            .Should().BeFalse();
    }

    [Fact]
    public void Intersection_Requires_Both_Sides_To_Agree()
    {
        var a = MakeAssistant("tutor", "student");
        a.IsVisibleToPersona("student", new List<string> { "admin-copilot" })
            .Should().BeFalse();
    }
}
