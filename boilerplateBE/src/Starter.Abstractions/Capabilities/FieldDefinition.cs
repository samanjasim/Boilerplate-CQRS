namespace Starter.Abstractions.Capabilities;

/// <summary>
/// One field on an importable/exportable entity. <see cref="FieldType"/> lives
/// in this same namespace so the contract is fully self-contained —
/// <c>Starter.Abstractions</c> does not reference any other project.
/// </summary>
public sealed record FieldDefinition(
    string Name, string DisplayName, FieldType Type, bool Required = false,
    bool ExportOnly = false, bool ImportOnly = false,
    string? ValidationRegex = null, string[]? EnumOptions = null, int? MaxLength = null);
