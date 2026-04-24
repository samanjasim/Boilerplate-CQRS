using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class UserPersonaEntityTests
{
    [Fact]
    public void Create_Sets_Fields()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();

        var up = UserPersona.Create(user, personaId, tenant, isDefault: true, assignedBy: assignedBy);

        up.UserId.Should().Be(user);
        up.PersonaId.Should().Be(personaId);
        up.TenantId.Should().Be(tenant);
        up.IsDefault.Should().BeTrue();
        up.AssignedBy.Should().Be(assignedBy);
        up.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MakeDefault_Sets_IsDefault_True()
    {
        var up = UserPersona.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isDefault: false, null);
        up.MakeDefault();
        up.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void ClearDefault_Sets_IsDefault_False()
    {
        var up = UserPersona.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isDefault: true, null);
        up.ClearDefault();
        up.IsDefault.Should().BeFalse();
    }
}
