using FluentAssertions;
using Moq;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Runtime.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class SinkWrappersTests
{
    [Fact]
    public async Task BufferingSink_Holds_Deltas_Until_Release()
    {
        var inner = new Mock<IAgentRunSink>();
        var sink = new BufferingSink(inner.Object);

        await sink.OnDeltaAsync("hello ", default);
        await sink.OnDeltaAsync("world", default);

        inner.Verify(s => s.OnDeltaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sink.BufferedContent.Should().Be("hello world");

        await sink.ReleaseAsync("hello world", default);
        inner.Verify(s => s.OnDeltaAsync("hello world", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BufferingSink_Forwards_Observability_Events_Live()
    {
        var inner = new Mock<IAgentRunSink>();
        var sink = new BufferingSink(inner.Object);

        await sink.OnStepStartedAsync(0, default);
        inner.Verify(s => s.OnStepStartedAsync(0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PassthroughSink_Forwards_All_Events()
    {
        var inner = new Mock<IAgentRunSink>();
        var sink = new PassthroughSink(inner.Object);

        await sink.OnDeltaAsync("x", default);
        inner.Verify(s => s.OnDeltaAsync("x", It.IsAny<CancellationToken>()), Times.Once);
    }
}
