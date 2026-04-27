using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Attributes;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Dispatches a single tool call: deserialise args → permission check → [DangerousAction]
/// approval gate → ISender.Send → unwrap Result/Result&lt;T&gt; → serialise JSON for the provider.
///
/// Extracted verbatim from ChatExecutionService.DispatchToolAsync (Plan 5a). Shape of
/// the returned JSON — {"ok":true,"value":...} on success, {"ok":false,"error":{"code":...,"message":...}}
/// on failure — is part of the contract the model sees and must not drift.
///
/// Plan 5d-2: when the resolved MediatR command type carries <see cref="DangerousActionAttribute"/>
/// and no approval grant is active, the dispatcher persists an <c>AiPendingApproval</c> row,
/// emits the <c>ai_dangerous_action_blocks_total</c> metric, and returns an
/// <c>AwaitingApproval</c> tool-result. The provider runtime treats this as a tool failure and
/// terminates the loop, surfacing <c>AgentRunStatus.AwaitingApproval</c> upstream.
/// </summary>
internal sealed class AgentToolDispatcher(
    ISender sender,
    IExecutionContext execution,
    ICurrentAgentRunContextAccessor runContext,
    IPendingApprovalService pendingApprovals,
    IConfiguration configuration,
    ILogger<AgentToolDispatcher> logger) : IAgentToolDispatcher
{
    private int ApprovalExpirationHours =>
        configuration.GetValue<int?>("Ai:Moderation:ApprovalExpirationHours") ?? 24;

    public async Task<AgentToolDispatchResult> DispatchAsync(
        AiToolCall call,
        ToolResolutionResult tools,
        CancellationToken ct)
    {
        if (!tools.DefinitionsByName.TryGetValue(call.Name, out var def))
            return Failure(AiErrors.ToolNotFound);

        // Plan 5d-1: when called from inside an agent run, IExecutionContext resolves to
        // AgentExecutionScope which applies hybrid-intersection (caller ∩ agent). Outside
        // an agent run (HTTP path), it resolves to HttpExecutionContext which delegates
        // to ICurrentUserService unchanged.
        if (!execution.HasPermission(def.RequiredPermission))
            return Failure(AiErrors.ToolPermissionDenied(call.Name));

        // Plan 5d-2: [DangerousAction] check. Skipped when the caller already holds an
        // approval grant (one-shot bypass installed via ApprovalGrantExecutionContext).
        var attr = def.CommandType.GetCustomAttribute<DangerousActionAttribute>();
        if (attr is not null && !execution.DangerousActionApprovalGrant)
        {
            var runCtx = runContext.AssistantId is { } aid && runContext.AgentPrincipalId is { } apid
                ? (assistantId: aid, principalId: apid)
                : default((Guid assistantId, Guid principalId)?);

            if (runCtx is null)
            {
                // Defensive: no agent context — refuse outright. The HTTP path can't trigger
                // [DangerousAction] because the runtime is the only thing that sets the
                // ICurrentAgentRunContextAccessor AsyncLocal.
                logger.LogWarning(
                    "Dispatcher saw [DangerousAction] tool {Tool} but no agent run context was active.",
                    call.Name);
                return Failure(PendingApprovalErrors.AccessDenied);
            }

            var pa = await pendingApprovals.CreateAsync(
                tenantId: runContext.TenantId,
                assistantId: runCtx.Value.assistantId,
                assistantName: runContext.AssistantName ?? "agent",
                agentPrincipalId: runCtx.Value.principalId,
                conversationId: runContext.ConversationId,
                agentTaskId: runContext.AgentTaskId,
                requestingUserId: runContext.RequestingUserId,
                toolName: call.Name,
                commandTypeName: def.CommandType.AssemblyQualifiedName ?? def.CommandType.FullName!,
                argumentsJson: call.ArgumentsJson,
                reasonHint: attr.Reason,
                expiresIn: TimeSpan.FromHours(ApprovalExpirationHours),
                ct: ct);

            AiAgentMetrics.DangerousActionBlocks.Add(1,
                new KeyValuePair<string, object?>("ai.tool_name", call.Name));

            return new AgentToolDispatchResult(
                JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = new
                    {
                        code = "AiAgent.AwaitingApproval",
                        message = $"Approval required for tool '{call.Name}'.",
                        approvalId = pa.Id,
                        expiresAt = pa.ExpiresAt
                    }
                }, AiJsonDefaults.Serializer),
                IsError: true,
                AwaitingApproval: true,
                ApprovalId: pa.Id);
        }

        object? command;
        try
        {
            command = JsonSerializer.Deserialize(call.ArgumentsJson, def.CommandType, AiJsonDefaults.Serializer);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize args for tool {Tool}.", call.Name);
            return Failure(AiErrors.ToolArgumentsInvalid(call.Name, ex.Message));
        }

        if (command is null)
            return Failure(AiErrors.ToolArgumentsInvalid(call.Name, "Deserialized arguments were null."));

        object? rawResult;
        try
        {
            rawResult = await sender.Send(command, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool {Tool} threw during dispatch.", call.Name);
            return Failure(AiErrors.ToolExecutionFailed(call.Name, ex.Message));
        }

        // Commands that return Result / Result<T> surface failure through Error rather than throwing.
        if (rawResult is Result r)
        {
            if (r.IsFailure)
                return Failure(r.Error);

            var resultType = rawResult.GetType();
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var value = resultType.GetProperty("Value")!.GetValue(rawResult);
                return Success(value);
            }

            return Success(null);
        }

        return Success(rawResult);
    }

    private static AgentToolDispatchResult Success(object? value) => new(
        JsonSerializer.Serialize(new { ok = true, value }, AiJsonDefaults.Serializer),
        IsError: false);

    private static AgentToolDispatchResult Failure(Error error) => new(
        JsonSerializer.Serialize(
            new { ok = false, error = new { code = error.Code, message = error.Description } },
            AiJsonDefaults.Serializer),
        IsError: true);
}
