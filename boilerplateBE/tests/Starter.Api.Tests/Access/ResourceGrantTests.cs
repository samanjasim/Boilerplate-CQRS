using FluentAssertions;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Xunit;

namespace Starter.Api.Tests.Access;

public sealed class ResourceGrantTests
{
    [Fact]
    public void Create_sets_all_fields_and_generates_id()
    {
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var grantedBy = Guid.NewGuid();

        var g = ResourceGrant.Create(
            tenantId,
            "File",
            resourceId,
            GrantSubjectType.User,
            subjectId,
            AccessLevel.Editor,
            grantedBy);

        g.Id.Should().NotBe(Guid.Empty);
        g.TenantId.Should().Be(tenantId);
        g.ResourceType.Should().Be("File");
        g.ResourceId.Should().Be(resourceId);
        g.SubjectType.Should().Be(GrantSubjectType.User);
        g.SubjectId.Should().Be(subjectId);
        g.Level.Should().Be(AccessLevel.Editor);
        g.GrantedByUserId.Should().Be(grantedBy);
        g.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateLevel_changes_level()
    {
        var g = ResourceGrant.Create(
            Guid.NewGuid(),
            "File",
            Guid.NewGuid(),
            GrantSubjectType.User,
            Guid.NewGuid(),
            AccessLevel.Viewer,
            Guid.NewGuid());

        g.UpdateLevel(AccessLevel.Manager);

        g.Level.Should().Be(AccessLevel.Manager);
    }
}
