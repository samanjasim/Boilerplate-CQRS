namespace Starter.Module.AI.Application.Services.Runtime;

/// <summary>
/// AsyncLocal accessor exposing the current agent run's assistant + conversation/task linkage
/// to scoped services (notably <c>AgentToolDispatcher</c>) without threading it through every
/// method. Set by the runtime decorator at run start, cleared on dispose.
/// </summary>
internal interface ICurrentAgentRunContextAccessor
{
    Guid? AssistantId { get; }
    string? AssistantName { get; }
    Guid? AgentPrincipalId { get; }
    Guid? ConversationId { get; }
    Guid? AgentTaskId { get; }
    Guid? RequestingUserId { get; }
    Guid? TenantId { get; }
}
