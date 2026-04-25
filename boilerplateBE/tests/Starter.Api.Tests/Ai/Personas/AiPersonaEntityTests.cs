using FluentAssertions;
using Starter.Domain.Exceptions;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AiPersonaEntityTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Create_Sets_Fields_And_Defaults()
    {
        var persona = AiPersona.Create(
            tenantId: Tenant,
            slug: "teacher",
            displayName: "Teacher",
            description: "Teaching staff",
            audienceType: PersonaAudienceType.Internal,
            safetyPreset: SafetyPreset.Standard,
            createdByUserId: Creator);

        persona.TenantId.Should().Be(Tenant);
        persona.Slug.Should().Be("teacher");
        persona.DisplayName.Should().Be("Teacher");
        persona.Description.Should().Be("Teaching staff");
        persona.AudienceType.Should().Be(PersonaAudienceType.Internal);
        persona.SafetyPreset.Should().Be(SafetyPreset.Standard);
        persona.IsSystemReserved.Should().BeFalse();
        persona.IsActive.Should().BeTrue();
        persona.PermittedAgentSlugs.Should().BeEmpty();
    }

    [Fact]
    public void Create_With_Anonymous_Audience_Throws()
    {
        var act = () => AiPersona.Create(
            Tenant, "x", "x", null,
            PersonaAudienceType.Anonymous, SafetyPreset.Standard, Creator);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateAnonymous_System_Reserved_With_Anonymous_Audience()
    {
        var persona = AiPersona.CreateAnonymous(Tenant, Creator);

        persona.Slug.Should().Be("anonymous");
        persona.AudienceType.Should().Be(PersonaAudienceType.Anonymous);
        persona.IsSystemReserved.Should().BeTrue();
        persona.IsActive.Should().BeFalse();
        persona.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void CreateDefault_Not_System_Reserved()
    {
        var persona = AiPersona.CreateDefault(Tenant, Creator);

        persona.Slug.Should().Be("default");
        persona.AudienceType.Should().Be(PersonaAudienceType.Internal);
        persona.IsSystemReserved.Should().BeFalse();
        persona.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_Changes_Mutable_Fields()
    {
        var persona = AiPersona.Create(Tenant, "client", "Client", null,
            PersonaAudienceType.EndCustomer, SafetyPreset.Standard, Creator);

        persona.Update(
            displayName: "External Client",
            description: "Outside client personas",
            safetyPreset: SafetyPreset.ProfessionalModerated,
            permittedAgentSlugs: new[] { "brand-content-agent" },
            isActive: true);

        persona.DisplayName.Should().Be("External Client");
        persona.Description.Should().Be("Outside client personas");
        persona.SafetyPreset.Should().Be(SafetyPreset.ProfessionalModerated);
        persona.PermittedAgentSlugs.Should().ContainSingle(s => s == "brand-content-agent");
        persona.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetActive_False_Sets_IsActive_False()
    {
        var persona = AiPersona.Create(Tenant, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Creator);

        persona.SetActive(false);

        persona.IsActive.Should().BeFalse();
    }

    [Fact]
    public void PermittedAgentSlugs_Dedups_And_Lowercases()
    {
        var persona = AiPersona.Create(Tenant, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Creator);

        persona.Update("Teacher", null, SafetyPreset.Standard,
            new[] { "tutor", "  tutor  ", "Lesson-Planner", "", "   " },
            isActive: true);

        persona.PermittedAgentSlugs.Should().BeEquivalentTo(new[] { "tutor", "lesson-planner" });
    }
}
