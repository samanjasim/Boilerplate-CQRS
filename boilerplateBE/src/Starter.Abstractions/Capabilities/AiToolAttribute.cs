namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Marks a MediatR request type (command or query) as an AI-callable tool. At DI
/// registration time the attributed type is wrapped in an <see cref="IAiToolDefinition"/>
/// adapter — the JSON Schema is auto-derived from the record shape, the adapter is added
/// to the tool catalog, and the tool becomes available to assistants that enable it.
///
/// <para>The LLM-safe command contract documented on <see cref="IAiToolDefinition"/> applies
/// identically here. In particular: do not attribute a command whose record shape contains
/// fields bound to server-trusted state (user id, tenant id, role flags) unless those fields
/// are explicitly excluded from the schema via <see cref="AiParameterIgnoreAttribute"/>.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class AiToolAttribute : Attribute
{
    /// <summary>Tool name used in LLM function calling (snake_case, unique across the process).</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description for the LLM to decide when to call this tool.</summary>
    public required string Description { get; init; }

    /// <summary>Grouping category shown in the admin UI and LLM catalog.</summary>
    public required string Category { get; init; }

    /// <summary>Permission the current user must hold for the tool to be offered.</summary>
    public required string RequiredPermission { get; init; }

    /// <summary>Hint for UI + LLM only; does not bypass <see cref="RequiredPermission"/>.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Optional JSON Schema override. When set, skips auto-derivation and uses the supplied
    /// schema verbatim. Use when the schema cannot be expressed by the record shape
    /// (dynamic enums, polymorphic payloads). Prefer auto-derivation when possible.
    /// </summary>
    public string? ParameterSchemaJson { get; init; }
}
