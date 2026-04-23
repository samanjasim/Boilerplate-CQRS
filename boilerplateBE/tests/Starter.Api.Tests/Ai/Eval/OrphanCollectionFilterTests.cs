using FluentAssertions;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

public sealed class OrphanCollectionFilterTests
{
    [Fact]
    public void NonTenantPrefix_IsRejected()
    {
        OrphanCollectionFilter
            .TryParseHarnessCollectionAge("eval-something", out _)
            .Should().BeFalse();
    }

    [Fact]
    public void V4Guid_IsRejected_SoRealTenantsAreNeverReaped()
    {
        var v4 = Guid.NewGuid();
        var collection = $"tenant_{v4:N}";
        OrphanCollectionFilter
            .TryParseHarnessCollectionAge(collection, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void V7Guid_TimestampMatchesCreationTimeWithinOneSecond()
    {
        var before = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        var v7 = Guid.CreateVersion7();
        var after = DateTimeOffset.UtcNow.AddMilliseconds(1);

        var parsed = OrphanCollectionFilter
            .TryParseHarnessCollectionAge($"tenant_{v7:N}", out var createdAt);

        parsed.Should().BeTrue();
        createdAt.Should().BeOnOrAfter(before);
        createdAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void MalformedHex_IsRejected()
    {
        OrphanCollectionFilter
            .TryParseHarnessCollectionAge("tenant_not-a-guid", out _)
            .Should().BeFalse();
    }
}
