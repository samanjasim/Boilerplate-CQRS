using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;

namespace Starter.Application.Features.ImportExport.Definitions;

public sealed class RoleExportDataProvider(IApplicationDbContext context) : IExportDataProvider
{
    public async Task<ExportDataResult> GetDataAsync(Guid? tenantId, string? filtersJson, CancellationToken ct = default)
    {
        var query = context.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => !r.IsSystemRole)
            .Where(r => tenantId == null || r.TenantId == tenantId)
            .AsQueryable();

        var filters = ParseFilters(filtersJson);

        if (filters.TryGetValue("searchTerm", out var searchTerm) && !string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(term) ||
                (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        query = query.OrderBy(r => r.Name);

        var roles = await query.ToListAsync(ct);

        var headers = new[] { "Name", "Description", "Is Active" };

        var rows = roles.Select(r => new[]
        {
            SanitizeCsvValue(r.Name),
            SanitizeCsvValue(r.Description),
            r.IsActive ? "true" : "false"
        }).ToList();

        return new ExportDataResult(headers, rows, rows.Count);
    }

    private static string SanitizeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        if (value.Length > 0 && "=@+-\t\r".Contains(value[0]))
            return "'" + value;
        return value;
    }

    private static Dictionary<string, string> ParseFilters(string? filtersJson)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(filtersJson);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => property.Value.GetRawText()
                };

                if (value is not null)
                    result[property.Name] = value;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
