namespace Starter.Abstractions.Capabilities;

/// <summary>
/// The logical type of a field on an importable/exportable entity. Used by
/// <see cref="FieldDefinition"/> in the import/export capability contract;
/// co-located here so <c>Starter.Abstractions</c> doesn't have to reference
/// any module's domain types.
/// </summary>
public enum FieldType { String = 0, Integer = 1, Decimal = 2, Boolean = 3, DateTime = 4, Enum = 5, Email = 6 }
