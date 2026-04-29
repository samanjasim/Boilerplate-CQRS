using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public class AiPersonaFlagshipFactoriesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Student_persona_is_internal_childsafe_active_not_reserved()
    {
        var p = AiPersona.CreateStudent(TenantId, Actor);

        p.Slug.Should().Be(AiPersona.StudentSlug).And.Be("student");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.ChildSafe);
        p.IsActive.Should().BeTrue();
        p.IsSystemReserved.Should().BeFalse();
    }

    [Fact]
    public void Teacher_persona_is_internal_standard()
    {
        var p = AiPersona.CreateTeacher(TenantId, Actor);

        p.Slug.Should().Be(AiPersona.TeacherSlug).And.Be("teacher");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Parent_persona_is_endcustomer_standard()
    {
        var p = AiPersona.CreateParent(TenantId, Actor);

        p.Slug.Should().Be(AiPersona.ParentSlug).And.Be("parent");
        p.AudienceType.Should().Be(PersonaAudienceType.EndCustomer);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Editor_persona_is_internal_standard()
    {
        var p = AiPersona.CreateEditor(TenantId, Actor);

        p.Slug.Should().Be(AiPersona.EditorSlug).And.Be("editor");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Approver_persona_is_internal_standard()
    {
        var p = AiPersona.CreateApprover(TenantId, Actor);

        p.Slug.Should().Be(AiPersona.ApproverSlug).And.Be("approver");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Client_persona_is_endcustomer_professionally_moderated()
    {
        var p = AiPersona.CreateClient(TenantId, Actor);

        p.Slug.Should().Be(AiPersona.ClientSlug).And.Be("client");
        p.AudienceType.Should().Be(PersonaAudienceType.EndCustomer);
        p.SafetyPreset.Should().Be(SafetyPreset.ProfessionalModerated);
    }
}
