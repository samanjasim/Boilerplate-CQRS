using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Dispatches a single tool call: deserialise args → permission check → ISender.Send →
/// unwrap Result/Result&lt;T&gt; → serialise JSON for the provider.
///
/// Extracted verbatim from ChatExecutionService.DispatchToolAsync (Plan 5a). Shape of
/// the returned JSON — {"ok":true,"value":...} on success, {"ok":false,"error":{"code":...,"message":...}}
/// on failure — is part of the contract the model sees and must not drift.
/// </summary>
internal sealed class AgentToolDispatcher(
    ISender sender,
    ICurrentUserService currentUser,
    ILogger<AgentToolDispatcher> logger) : IAgentToolDispatcher
{
    public async Task<AgentToolDispatchResult> DispatchAsync(
        AiToolCall call,
        ToolResolutionResult tools,
        CancellationToken ct)
    {
        if (!tools.DefinitionsByName.TryGetValue(call.Name, out var def))
            return Failure(AiErrors.ToolNotFound);

        if (!currentUser.HasPermission(def.RequiredPermission))
            return Failure(AiErrors.ToolPermissionDenied(call.Name));

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
