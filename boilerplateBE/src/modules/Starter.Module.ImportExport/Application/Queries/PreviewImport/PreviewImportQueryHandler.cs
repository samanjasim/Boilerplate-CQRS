using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Module.ImportExport.Domain.Errors;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.PreviewImport;

internal sealed class PreviewImportQueryHandler(
    IApplicationDbContext context,
    IStorageService storageService,
    IImportExportRegistry registry) : IRequestHandler<PreviewImportQuery, Result<ImportPreviewDto>>
{
    private const int MaxPreviewRows = 5;

    public async Task<Result<ImportPreviewDto>> Handle(
        PreviewImportQuery request, CancellationToken cancellationToken)
    {
        var definition = registry.GetDefinition(request.EntityType);
        if (definition is null)
            return Result.Failure<ImportPreviewDto>(ImportExportErrors.EntityTypeNotFound);

        if (!definition.SupportsImport)
            return Result.Failure<ImportPreviewDto>(ImportExportErrors.ImportNotSupported);

        var fileMetadata = await context.Set<FileMetadata>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FileId, cancellationToken);

        if (fileMetadata is null)
            return Result.Failure<ImportPreviewDto>(ImportExportErrors.FileNotFound);

        Stream stream;
        try
        {
            stream = await storageService.DownloadAsync(fileMetadata.StorageKey, cancellationToken);
        }
        catch
        {
            return Result.Failure<ImportPreviewDto>(ImportExportErrors.FileNotFound);
        }

        using (stream)
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            var headerLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(headerLine))
                return Result.Failure<ImportPreviewDto>(ImportExportErrors.InvalidCsvFormat);

            var csvHeaders = ParseCsvLine(headerLine);

            var knownFieldNames = definition.Fields
                .Where(f => !f.ExportOnly)
                .Select(f => f.DisplayName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var knownFieldsByDisplay = definition.Fields
                .Where(f => !f.ExportOnly)
                .ToDictionary(f => f.DisplayName, f => f, StringComparer.OrdinalIgnoreCase);

            var unrecognizedColumns = csvHeaders
                .Where(h => !knownFieldNames.Contains(h))
                .ToArray();

            var validationErrors = new List<string>();

            var requiredFields = definition.Fields
                .Where(f => f.Required && !f.ExportOnly)
                .Select(f => f.DisplayName)
                .ToArray();

            foreach (var required in requiredFields)
            {
                if (!csvHeaders.Any(h => string.Equals(h, required, StringComparison.OrdinalIgnoreCase)))
                    validationErrors.Add($"Required column '{required}' is missing.");
            }

            var previewRows = new List<string[]>();
            int totalDataRows = 0;
            string? line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                totalDataRows++;

                if (previewRows.Count < MaxPreviewRows)
                {
                    var cells = ParseCsvLine(line);

                    // Validate type mismatches in preview rows
                    for (int i = 0; i < cells.Length && i < csvHeaders.Length; i++)
                    {
                        var header = csvHeaders[i];
                        if (knownFieldsByDisplay.TryGetValue(header, out var fieldDef))
                        {
                            var cellValue = cells[i];
                            if (!string.IsNullOrWhiteSpace(cellValue))
                            {
                                var typeError = ValidateFieldType(fieldDef.Type, cellValue, header, totalDataRows);
                                if (typeError is not null)
                                    validationErrors.Add(typeError);
                            }
                        }
                    }

                    previewRows.Add(cells);
                }
            }

            return Result.Success(new ImportPreviewDto(
                Headers: csvHeaders,
                PreviewRows: previewRows.ToArray(),
                ValidationErrors: validationErrors.ToArray(),
                TotalRowCount: totalDataRows,
                UnrecognizedColumns: unrecognizedColumns));
        }
    }

    private static string? ValidateFieldType(FieldType type, string value, string column, int rowNumber)
    {
        return type switch
        {
            FieldType.Boolean when !bool.TryParse(value, out _) && value != "0" && value != "1" =>
                $"Row {rowNumber}, column '{column}': '{value}' is not a valid boolean.",
            FieldType.Integer when !long.TryParse(value, out _) =>
                $"Row {rowNumber}, column '{column}': '{value}' is not a valid integer.",
            FieldType.Decimal when !decimal.TryParse(value, out _) =>
                $"Row {rowNumber}, column '{column}': '{value}' is not a valid decimal.",
            FieldType.DateTime when !DateTime.TryParse(value, out _) =>
                $"Row {rowNumber}, column '{column}': '{value}' is not a valid date/time.",
            _ => null
        };
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString().Trim());
        return fields.ToArray();
    }
}
