using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Costs;

public sealed class AiUsageLogShapeTests
{
    [Fact]
    public void Create_Accepts_AssistantId_And_PrincipalId()
    {
        var log = AiUsageLog.Create(
            tenantId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            provider: AiProviderType.OpenAI,
            model: "gpt-4o",
            inputTokens: 100, outputTokens: 50,
            estimatedCost: 0.001m,
            requestType: AiRequestType.Chat,
            aiAssistantId: Guid.NewGuid(),
            agentPrincipalId: Guid.NewGuid());

        log.AiAssistantId.Should().NotBeNull();
        log.AgentPrincipalId.Should().NotBeNull();
    }

    [Fact]
    public void Create_Defaults_AssistantId_And_PrincipalId_To_Null()
    {
        var log = AiUsageLog.Create(
            tenantId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            provider: AiProviderType.OpenAI,
            model: "gpt-4o",
            inputTokens: 100, outputTokens: 50,
            estimatedCost: 0.001m,
            requestType: AiRequestType.Chat);

        log.AiAssistantId.Should().BeNull();
        log.AgentPrincipalId.Should().BeNull();
    }
}
