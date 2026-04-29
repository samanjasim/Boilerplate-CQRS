using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class ApproveDenyPendingActionTests
{
    [Fact]
    public async Task Deny_Without_Reason_Fails_Validation()
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var opts = new DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var svc = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
        var handler = new DenyPendingActionCommandHandler(svc, cu.Object, db);

        var result = await handler.Handle(new DenyPendingActionCommand(Guid.NewGuid(), ""), default);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.DenyReasonRequired");
    }
}
