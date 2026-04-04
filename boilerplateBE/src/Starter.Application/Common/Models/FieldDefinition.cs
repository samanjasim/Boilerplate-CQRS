using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Common.Models;

public sealed record FieldDefinition(
    string Name, string DisplayName, FieldType Type, bool Required = false,
    bool ExportOnly = false, bool ImportOnly = false,
    string? ValidationRegex = null, string[]? EnumOptions = null, int? MaxLength = null);
