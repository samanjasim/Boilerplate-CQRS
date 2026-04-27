using FluentAssertions;
using Starter.Domain.Common;
using Xunit;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AuditLogShapeTests
{
    [Fact]
    public void AuditLog_Has_DualAttribution_Columns()
    {
        var log = new AuditLog
        {
            OnBehalfOfUserId = Guid.NewGuid(),
            AgentPrincipalId = Guid.NewGuid(),
            AgentRunId = Guid.NewGuid(),
        };
        log.OnBehalfOfUserId.Should().NotBeNull();
        log.AgentPrincipalId.Should().NotBeNull();
        log.AgentRunId.Should().NotBeNull();
    }
}
