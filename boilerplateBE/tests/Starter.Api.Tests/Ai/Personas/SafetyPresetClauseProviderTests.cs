using System.Globalization;
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class SafetyPresetClauseProviderTests
{
    private static readonly ResxSafetyPresetClauseProvider Sut = new();

    [Theory]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.Internal)]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.EndCustomer)]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.Anonymous)]
    public void Standard_Returns_Empty(SafetyPreset preset, PersonaAudienceType audience)
    {
        Sut.GetClause(preset, audience, CultureInfo.GetCultureInfo("en")).Should().BeEmpty();
    }

    [Fact]
    public void ChildSafe_Internal_En_Contains_Expected_Phrase()
    {
        var clause = Sut.GetClause(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, CultureInfo.GetCultureInfo("en"));
        clause.Should().NotBeEmpty();
        clause.Should().Contain("minor under 16");
    }

    [Fact]
    public void ProfessionalModerated_EndCustomer_En_Contains_Expected_Phrase()
    {
        var clause = Sut.GetClause(SafetyPreset.ProfessionalModerated, PersonaAudienceType.EndCustomer, CultureInfo.GetCultureInfo("en"));
        clause.Should().Contain("external client");
    }

    [Fact]
    public void ChildSafe_Internal_Ar_Returns_Arabic()
    {
        var clause = Sut.GetClause(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, CultureInfo.GetCultureInfo("ar"));
        clause.Should().NotBeEmpty();
        clause.Should().Contain("قاصراً");
    }

    [Fact]
    public void Unknown_Culture_Falls_Back_To_En()
    {
        var clause = Sut.GetClause(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, CultureInfo.GetCultureInfo("fr"));
        clause.Should().Contain("minor under 16");
    }
}
