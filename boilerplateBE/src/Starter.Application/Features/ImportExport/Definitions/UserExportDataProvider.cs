using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Domain.Identity.Enums;

namespace Starter.Application.Features.ImportExport.Definitions;

public sealed class UserExportDataProvider(IApplicationDbContext context) : IExportDataProvider
{
    public async Task<ExportDataResult> GetDataAsync(Guid? tenantId, string? filtersJson, CancellationToken ct = default)
    {
        var query = context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => tenantId == null || u.TenantId == tenantId)
            .AsQueryable();

        var filters = ParseFilters(filtersJson);

        if (filters.TryGetValue("status", out var statusStr) && !string.IsNullOrWhiteSpace(statusStr))
        {
            var status = UserStatus.FromName(statusStr);
            if (status is not null)
                query = query.Where(u => u.Status == status);
        }

        if (filters.TryGetValue("searchTerm", out var searchTerm) && !string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(u =>
                u.Email.Value.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term) ||
                u.FullName.FirstName.ToLower().Contains(term) ||
                u.FullName.LastName.ToLower().Contains(term));
        }

        query = query.OrderBy(u => u.CreatedAt);

        var users = await query.ToListAsync(ct);

        var headers = new[] { "Email", "First Name", "Last Name", "Username", "Status", "Roles", "Created At" };

        var rows = users.Select(u => new[]
        {
            SanitizeCsvValue(u.Email.Value),
            SanitizeCsvValue(u.FullName.FirstName),
            SanitizeCsvValue(u.FullName.LastName),
            SanitizeCsvValue(u.Username),
            SanitizeCsvValue(u.Status.Name),
            SanitizeCsvValue(string.Join(", ", u.UserRoles
                .Where(ur => ur.Role is not null)
                .Select(ur => ur.Role!.Name))),
            u.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
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
