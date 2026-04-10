using System.Text;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.ImportExport.Domain.Errors;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportTemplate;

internal sealed class GetImportTemplateQueryHandler(
    IImportExportRegistry registry) : IRequestHandler<GetImportTemplateQuery, Result<byte[]>>
{
    public Task<Result<byte[]>> Handle(
        GetImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var definition = registry.GetDefinition(request.EntityType);
        if (definition is null)
            return Task.FromResult(Result.Failure<byte[]>(ImportExportErrors.EntityTypeNotFound));

        if (!definition.SupportsImport)
            return Task.FromResult(Result.Failure<byte[]>(ImportExportErrors.ImportNotSupported));

        var importFields = definition.Fields
            .Where(f => !f.ExportOnly)
            .ToArray();

        var headers = importFields.Select(f => f.DisplayName).ToArray();
        var exampleRow = importFields.Select(f => GetExampleValue(f)).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));
        sb.AppendLine(string.Join(",", exampleRow.Select(EscapeCsvField)));

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = preamble.Concat(content).ToArray();

        return Task.FromResult(Result.Success(bytes));
    }

    private static string GetExampleValue(FieldDefinition field) =>
        field.Type switch
        {
            FieldType.Email => "user@example.com",
            FieldType.Boolean => "true",
            FieldType.Integer => "1",
            FieldType.Decimal => "1.00",
            FieldType.DateTime => DateTime.UtcNow.ToString("yyyy-MM-dd"),
            FieldType.Enum => field.EnumOptions?.FirstOrDefault() ?? "value",
            _ => field.Name == "FirstName" || field.DisplayName.Contains("First") ? "John"
               : field.Name == "LastName" || field.DisplayName.Contains("Last") ? "Doe"
               : "Example"
        };

    private static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
