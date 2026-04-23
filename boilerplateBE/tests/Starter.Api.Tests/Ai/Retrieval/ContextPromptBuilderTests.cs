using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;
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
            DegradedStages: [],
            Siblings: [],
            FusedCandidates: 0,
            DetectedLanguage: "unknown");

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
            DegradedStages: [],
            Siblings: [],
            FusedCandidates: 0,
            DetectedLanguage: "unknown");

        var sp = ContextPromptBuilder.Build("S", ctx);

        sp.Should().Contain("child");
        sp.Should().Contain("parent context");
        sp.Should().Contain("(context continues)");
        sp.Should().NotContain("[2]");
    }

    [Fact]
    public void Siblings_Rendered_Under_Nearby_Context_Separator()
    {
        var sibDocId = Guid.NewGuid();
        var ctx = new RetrievedContext(
            Children: new List<RetrievedChunk> { MakeChild("childAnchor") },
            Parents: [],
            TotalTokens: 10,
            TruncatedByBudget: false,
            DegradedStages: [],
            Siblings: new List<RetrievedChunk>
            {
                new(Guid.NewGuid(), sibDocId, "doc-x", "sibA", "SibSec", 3, "child", 0, 0, 0, null, 4),
                new(Guid.NewGuid(), sibDocId, "doc-x", "sibB", null, null, "child", 0, 0, 0, null, 6)
            },
            FusedCandidates: 0,
            DetectedLanguage: "unknown");

        var output = ContextPromptBuilder.Build("Sys", ctx);

        output.Should().Contain("--- Nearby context ---");
        output.Should().Contain("sibA");
        output.Should().Contain("sibB");
        output.Should().Contain("[Document: \"doc-x\"");
        // Siblings must not be numbered as citation targets
        output.IndexOf("sibA").Should().BeGreaterThan(output.IndexOf("--- Nearby context ---"));
        output.IndexOf("sibB").Should().BeGreaterThan(output.IndexOf("--- Nearby context ---"));
        // [2] would indicate siblings were numbered — they must not be
        output.Should().NotContain("[2]");
    }

    [Fact]
    public void Code_chunk_is_wrapped_in_triple_backticks()
    {
        var ctx = new RetrievedContext([MakeTyped("x = 1", ChunkType.Code)], [], 10, false, [], [], 0, "unknown");
        var prompt = ContextPromptBuilder.Build("sys", ctx);
        prompt.Should().Contain("```\nx = 1\n```");
    }

    [Fact]
    public void Math_chunk_is_wrapped_in_dollar_dollar()
    {
        var ctx = new RetrievedContext([MakeTyped("a = b + c", ChunkType.Math)], [], 10, false, [], [], 0, "unknown");
        var prompt = ContextPromptBuilder.Build("sys", ctx);
        prompt.Should().Contain("$$\na = b + c\n$$");
    }

    [Fact]
    public void Body_chunk_is_unwrapped()
    {
        var ctx = new RetrievedContext([MakeTyped("hello", ChunkType.Body)], [], 10, false, [], [], 0, "unknown");
        var prompt = ContextPromptBuilder.Build("sys", ctx);
        prompt.Should().Contain("hello").And.NotContain("```hello");
    }

    private static RetrievedChunk MakeTyped(string content, ChunkType type) => new(
        ChunkId: Guid.NewGuid(), DocumentId: Guid.NewGuid(), DocumentName: "d", Content: content,
        SectionTitle: "S", PageNumber: 1, ChunkLevel: "child",
        SemanticScore: 0, KeywordScore: 0, HybridScore: 1, ParentChunkId: null, ChunkIndex: 0,
        ChunkType: type);

    private static RetrievedChunk MakeChild(string content, Guid? parentChunkId = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Doc", content, "Section", 1,
        "child", 0.9m, 0.4m, 0.7m, parentChunkId, 0);

    private static RetrievedChunk MakeParent(Guid id, string content) => new(
        id, Guid.NewGuid(), "Doc", content, "Section", 1,
        "parent", 0m, 0m, 0m, null, 0);
}
