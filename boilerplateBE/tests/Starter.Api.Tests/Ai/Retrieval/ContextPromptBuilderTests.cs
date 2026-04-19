using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ContextPromptBuilderTests
{
    [Fact]
    public void Empty_Context_Omits_Context_Block()
    {
        var sp = ContextPromptBuilder.Build(
            assistantSystemPrompt: "You are helpful.",
            context: RetrievedContext.Empty);

        sp.Should().NotContain("<context>");
        sp.Should().Contain("You are helpful.");
    }

    [Fact]
    public void Non_Empty_Context_Numbers_Children_One_To_N()
    {
        var ctx = new RetrievedContext(
            Children: new List<RetrievedChunk>
            {
                MakeChild("apple"),
                MakeChild("banana")
            },
            Parents: [],
            TotalTokens: 10,
            TruncatedByBudget: false,
            DegradedStages: []);

        var sp = ContextPromptBuilder.Build("Be helpful.", ctx);

        sp.Should().Contain("[1]");
        sp.Should().Contain("[2]");
        sp.Should().Contain("apple");
        sp.Should().Contain("banana");
        sp.Should().Contain("<context>");
        sp.Should().Contain("<assistant_instructions>");
        sp.Should().Contain("Be helpful.");
    }

    [Fact]
    public void Parents_Appear_Near_Children_Without_Own_Marker()
    {
        var parentId = Guid.NewGuid();
        var ctx = new RetrievedContext(
            Children: new List<RetrievedChunk> { MakeChild("child", parentChunkId: parentId) },
            Parents: new List<RetrievedChunk> { MakeParent(parentId, "parent context") },
            TotalTokens: 10,
            TruncatedByBudget: false,
            DegradedStages: []);

        var sp = ContextPromptBuilder.Build("S", ctx);

        sp.Should().Contain("child");
        sp.Should().Contain("parent context");
        sp.Should().Contain("(context continues)");
        sp.Should().NotContain("[2]");
    }

    private static RetrievedChunk MakeChild(string content, Guid? parentChunkId = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Doc", content, "Section", 1,
        "child", 0.9m, 0.4m, 0.7m, parentChunkId);

    private static RetrievedChunk MakeParent(Guid id, string content) => new(
        id, Guid.NewGuid(), "Doc", content, "Section", 1,
        "parent", 0m, 0m, 0m, null);
}
