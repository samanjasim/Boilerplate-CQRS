using System.Text.Json;
using FluentAssertions;
using Moq;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Abstractions;

public sealed class ActivityServiceExtensionsTests
{
    [Fact]
    public async Task RecordAsync_Typed_SerializesWithWebDefaults()
    {
        var service = new Mock<IActivityService>();
        string? captured = null;
        service
            .Setup(s => s.RecordAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, Guid?, string, Guid?, string?, string?, CancellationToken>(
                (_, _, _, _, _, json, _, _) => captured = json)
            .Returns(Task.CompletedTask);

        var metadata = new { OldName = "old", NewName = "new" };
        await service.Object.RecordAsync(
            "Product", Guid.NewGuid(), Guid.NewGuid(),
            "renamed", Guid.NewGuid(), metadata);

        captured.Should().NotBeNull();
        var expected = JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        captured.Should().Be(expected);
        captured.Should().Contain("\"oldName\"").And.Contain("\"newName\"");
    }
}
