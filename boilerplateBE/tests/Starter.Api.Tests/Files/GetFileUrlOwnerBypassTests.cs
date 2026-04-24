using FluentAssertions;
using Moq;
using Starter.Api.Tests.Access._Helpers;
using Starter.Application.Common.Access;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Files.Queries.GetFileUrl;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Enums;
using Xunit;

namespace Starter.Api.Tests.Files;

/// <summary>
/// Regression for I2: the owner bypass in GetFileUrlQueryHandler must also
/// verify the file still belongs to the user's current tenant. Otherwise a
/// user who previously uploaded a file while in tenant A and has since been
/// moved to tenant B retains read access to that file.
/// </summary>
public sealed class GetFileUrlOwnerBypassTests
{
    [Fact]
    public async Task Owner_in_same_tenant_receives_signed_url()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var file = FileMetadata.Create(
            "f.pdf", "key", "application/pdf", 1, FileCategory.Document,
            uploadedBy: userId, tenantId: tenantId,
            visibility: ResourceVisibility.Private);

        var db = TestAccessFactory.CreateDb();
        db.Add(file);
        await db.SaveChangesAsync(CancellationToken.None);

        var caller = FakeCurrentUser.For(userId, tenantId);
        var fileService = new Mock<IFileService>();
        fileService.Setup(s => s.GetUrlAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signed.url");
        var access = new Mock<IResourceAccessService>();

        var handler = new GetFileUrlQueryHandler(db, fileService.Object, access.Object, caller);
        var result = await handler.Handle(new GetFileUrlQuery(file.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("https://signed.url");
    }

    [Fact]
    public async Task Former_owner_now_in_different_tenant_is_denied_when_no_acl_grant()
    {
        var userId = Guid.NewGuid();
        var originalTenant = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        // File remains in the original tenant; user uploaded it there.
        var file = FileMetadata.Create(
            "f.pdf", "key", "application/pdf", 1, FileCategory.Document,
            uploadedBy: userId, tenantId: originalTenant,
            visibility: ResourceVisibility.Private);

        var db = TestAccessFactory.CreateDb();
        db.Add(file);
        await db.SaveChangesAsync(CancellationToken.None);

        // Same user, but now in a different tenant. No ACL grant exists.
        var caller = FakeCurrentUser.For(userId, newTenant);
        var fileService = new Mock<IFileService>();
        var access = new Mock<IResourceAccessService>();
        access.Setup(a => a.CanAccessAsync(
                It.IsAny<ICurrentUserService>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<AccessLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new GetFileUrlQueryHandler(db, fileService.Object, access.Object, caller);
        var result = await handler.Handle(new GetFileUrlQuery(file.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Starter.Shared.Results.ErrorType.Forbidden);
        fileService.Verify(s => s.GetUrlAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
