using System.Text.Json;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Interface for registering MediatR commands as AI-callable tools.
/// Modules implement this in their ConfigureServices to expose commands
/// to the AI execution engine. When AI module is absent, registrations are unused.
/// </summary>
public interface IAiToolDefinition
{
    /// <summary>Tool name used in LLM function calling (e.g., "create_product").</summary>
    string Name { get; }

    /// <summary>Human-readable description for the LLM to understand when to use this tool.</summary>
    string Description { get; }

    /// <summary>JSON Schema describing the tool's parameters.</summary>
    JsonElement ParameterSchema { get; }

    /// <summary>Fully qualified type of the MediatR command to execute.</summary>
    Type CommandType { get; }

    /// <summary>Permission the user must have to invoke this tool (e.g., "Products.Create").</summary>
    string RequiredPermission { get; }

    /// <summary>Grouping category (e.g., "Products", "Users", "Orders").</summary>
    string Category { get; }

    /// <summary>Whether this tool only reads data (no mutations).</summary>
    bool IsReadOnly { get; }
}
