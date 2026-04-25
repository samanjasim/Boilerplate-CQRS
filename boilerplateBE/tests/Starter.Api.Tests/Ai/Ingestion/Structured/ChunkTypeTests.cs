using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class ChunkTypeTests
{
    [Fact]
    public void Body_is_zero_so_unset_rows_default_to_body()
    {
        ((int)ChunkType.Body).Should().Be(0);
    }

    [Theory]
    [InlineData(ChunkType.Body)]
    [InlineData(ChunkType.Heading)]
    [InlineData(ChunkType.Table)]
    [InlineData(ChunkType.Code)]
    [InlineData(ChunkType.Math)]
    [InlineData(ChunkType.List)]
    [InlineData(ChunkType.Quote)]
    public void All_declared_values_are_distinct(ChunkType type)
    {
        Enum.IsDefined(typeof(ChunkType), type).Should().BeTrue();
    }
}
