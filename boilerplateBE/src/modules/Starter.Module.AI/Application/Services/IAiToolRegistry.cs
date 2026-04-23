using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Services;

/// <summary>
/// Abstraction over the set of AI tools available at runtime. The code-side registrations
/// (IAiToolDefinition) are the source of truth for schemas; the DB (AiTool) tracks admin
/// enable/disable state. The registry joins the two.
/// </summary>
internal interface IAiToolRegistry
{
    /// <summary>All tools known to the system, joined with their DB enable state. Used by the
    /// admin Tools controller — no permission filtering.</summary>
    Task<IReadOnlyList<AiToolDto>> ListAllAsync(CancellationToken ct);

    /// <summary>Look up a single tool by its code-defined name.</summary>
    IAiToolDefinition? FindByName(string name);

    /// <summary>
    /// Resolve the tools available to a single chat turn: intersection of
    /// (assistant.EnabledToolNames) × (registry.IsEnabled in DB) × (user.HasPermission).
    /// Returns a ToolResolutionResult with the provider-facing definitions plus the
    /// per-name lookup needed to dispatch results.
    /// </summary>
    Task<ToolResolutionResult> ResolveForAssistantAsync(
        AiAssistant assistant,
        CancellationToken ct);
}
