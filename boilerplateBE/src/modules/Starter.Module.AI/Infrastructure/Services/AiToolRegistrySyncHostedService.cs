using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiToolRegistrySyncHostedService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IAiToolDefinition> definitions,
    ILogger<AiToolRegistrySyncHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var distinct = new Dictionary<string, IAiToolDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in definitions)
        {
            if (!distinct.TryAdd(def.Name, def))
                logger.LogWarning(
                    "Duplicate IAiToolDefinition registered for '{Name}'. Keeping first.", def.Name);
        }

        if (distinct.Count == 0)
        {
            logger.LogInformation("No IAiToolDefinition registrations to sync.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        var existingRows = await db.AiTools.ToDictionaryAsync(
            t => t.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var added = 0;
        var updated = 0;

        foreach (var def in distinct.Values)
        {
            var schemaJson = def.ParameterSchema.GetRawText();
            if (existingRows.TryGetValue(def.Name, out var row))
            {
                // Rehydrate schema/description/permission while preserving IsEnabled.
                // The domain entity exposes only Toggle/Create, so we use a dedicated
                // method added in this task.
                row.RefreshFromDefinition(
                    description: def.Description,
                    commandType: def.CommandType.AssemblyQualifiedName ?? def.CommandType.FullName!,
                    requiredPermission: def.RequiredPermission,
                    category: def.Category,
                    parameterSchema: schemaJson,
                    isReadOnly: def.IsReadOnly);
                updated++;
            }
            else
            {
                var tool = AiTool.Create(
                    name: def.Name,
                    description: def.Description,
                    commandType: def.CommandType.AssemblyQualifiedName ?? def.CommandType.FullName!,
                    requiredPermission: def.RequiredPermission,
                    category: def.Category,
                    parameterSchema: schemaJson,
                    isEnabled: true,
                    isReadOnly: def.IsReadOnly);
                db.AiTools.Add(tool);
                added++;
            }
        }

        if (added + updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "AI tool registry synced. Added={Added} Updated={Updated}", added, updated);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
