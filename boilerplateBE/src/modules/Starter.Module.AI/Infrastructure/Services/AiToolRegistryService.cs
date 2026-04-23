using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiToolRegistryService(
    IEnumerable<IAiToolDefinition> definitions,
    IServiceScopeFactory scopeFactory)
    : IAiToolRegistry
{
    // Definitions are singleton DI registrations — snapshot them once.
    private readonly IReadOnlyDictionary<string, IAiToolDefinition> _byName =
        definitions
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            // First-wins when a duplicate name is registered — the sync service will log
            // the duplicate so the developer can rename.
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public IAiToolDefinition? FindByName(string name) =>
        _byName.TryGetValue(name, out var d) ? d : null;

    public async Task<IReadOnlyList<AiToolDto>> ListAllAsync(CancellationToken ct)
    {
        // Use a short-lived scope so the registry (singleton) can safely resolve a scoped
        // DbContext for the sync query.
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        var rows = await db.AiTools.AsNoTracking().ToListAsync(ct);
        var rowByName = rows.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        return _byName.Values
            .OrderBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => d.ToDto(rowByName.GetValueOrDefault(d.Name)))
            .ToList();
    }

    public async Task<ToolResolutionResult> ResolveForAssistantAsync(
        AiAssistant assistant,
        CancellationToken ct)
    {
        if (assistant.EnabledToolNames.Count == 0)
            return EmptyResolution;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        // Resolve per-call to avoid capturing the first request's user into this singleton.
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

        // Load the enable-state rows in one round trip.
        var enabledRowNames = new HashSet<string>(
            await db.AiTools.AsNoTracking()
                .Where(t => t.IsEnabled)
                .Select(t => t.Name)
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var resolved = new List<(AiToolDefinitionDto dto, IAiToolDefinition def)>();

        foreach (var name in assistant.EnabledToolNames)
        {
            if (!_byName.TryGetValue(name, out var def)) continue;       // stale assistant config
            if (!enabledRowNames.Contains(def.Name)) continue;           // globally disabled
            if (!currentUser.HasPermission(def.RequiredPermission)) continue; // user not allowed

            resolved.Add((
                new AiToolDefinitionDto(def.Name, def.Description, def.ParameterSchema),
                def));
        }

        if (resolved.Count == 0)
            return EmptyResolution;

        return new ToolResolutionResult(
            resolved.Select(r => r.dto).ToList(),
            resolved.ToDictionary(r => r.def.Name, r => r.def, StringComparer.OrdinalIgnoreCase));
    }

    private static readonly ToolResolutionResult EmptyResolution = new(
        Array.Empty<AiToolDefinitionDto>(),
        new Dictionary<string, IAiToolDefinition>(StringComparer.OrdinalIgnoreCase));
}
