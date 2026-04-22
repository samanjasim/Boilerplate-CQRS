using FluentAssertions;
using Starter.Domain.Exceptions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class AiAssistantRagScopeTests
{
    private static AiAssistant CreateAssistant() =>
        AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Test Assistant",
            description: null,
            systemPrompt: "You are a helpful assistant.",
            createdByUserId: Guid.NewGuid());

    [Fact]
    public void SetRagScope_None_IsDefault()
    {
        var assistant = CreateAssistant();

        assistant.RagScope.Should().Be(AiRagScope.None);
    }

    [Fact]
    public void SetRagScope_AllTenantDocuments_Allowed_Without_DocIds()
    {
        var assistant = CreateAssistant();

        var act = () => assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        act.Should().NotThrow();
        assistant.RagScope.Should().Be(AiRagScope.AllTenantDocuments);
    }

    [Fact]
    public void SetRagScope_SelectedDocuments_Requires_DocIds()
    {
        var assistant = CreateAssistant();

        var act = () => assistant.SetRagScope(AiRagScope.SelectedDocuments);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(AiErrors.RagScopeRequiresDocuments.Code);
    }

    [Fact]
    public void SetRagScope_SelectedDocuments_Passes_When_DocIds_Present()
    {
        var assistant = CreateAssistant();
        assistant.SetKnowledgeBase([Guid.NewGuid()]);

        var act = () => assistant.SetRagScope(AiRagScope.SelectedDocuments);

        act.Should().NotThrow();
        assistant.RagScope.Should().Be(AiRagScope.SelectedDocuments);
    }
}
