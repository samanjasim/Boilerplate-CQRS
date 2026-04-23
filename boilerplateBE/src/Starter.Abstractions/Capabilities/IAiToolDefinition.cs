using System.Text.Json;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Interface for registering MediatR commands as AI-callable tools.
/// Modules implement this in their ConfigureServices to expose commands
/// to the AI execution engine. When AI module is absent, registrations are unused.
///
/// <para><b>LLM-safe command contract.</b> A command registered as an AI tool is invoked with
/// arguments the LLM synthesised from <see cref="ParameterSchema"/>. The registrar is responsible
/// for making sure the command type is safe to expose in this trust boundary:</para>
/// <list type="bullet">
///   <item><description>Every field the LLM can set MUST appear in <see cref="ParameterSchema"/>.
///     Properties on the command that are not in the schema will deserialize as default values —
///     do not depend on them being populated by the caller.</description></item>
///   <item><description>The command MUST NOT accept fields that bind to server-trusted state (user id,
///     tenant id, role, elevated flags). Those are resolved by handlers from
///     <c>ICurrentUserService</c>, not from LLM-provided JSON.</description></item>
///   <item><description>All mutating commands MUST be authorized by <see cref="RequiredPermission"/>;
///     the registry filters out tools the current user is not permitted to invoke. <see cref="IsReadOnly"/>
///     is a hint to the UI/LLM and does not relax authorization.</description></item>
///   <item><description>Commands SHOULD define a FluentValidation validator. The dispatcher relies on the
///     validation pipeline behavior to reject malformed LLM arguments and surface actionable error text
///     back to the model.</description></item>
///   <item><description>Handlers MUST tolerate adversarial inputs: treat LLM arguments as untrusted user input,
///     never interpolate into queries, and prefer enums/ids over free-form strings where possible.</description></item>
/// </list>
/// </summary>
public interface IAiToolDefinition
{
    /// <summary>Tool name used in LLM function calling (e.g., "create_product").</summary>
    string Name { get; }

    /// <summary>Human-readable description for the LLM to understand when to use this tool.</summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema describing the tool's parameters. MUST enumerate every field the LLM is permitted
    /// to set on <see cref="CommandType"/>. Omit server-trusted fields (tenant id, user id, etc.) —
    /// they will be silently ignored during deserialization.
    /// </summary>
    JsonElement ParameterSchema { get; }

    /// <summary>
    /// Fully qualified type of the MediatR command to execute. The type must be deserializable from the
    /// JSON shape declared in <see cref="ParameterSchema"/> and conform to the LLM-safe command contract
    /// documented on the interface summary.
    /// </summary>
    Type CommandType { get; }

    /// <summary>Permission the user must have to invoke this tool (e.g., "Products.Create").</summary>
    string RequiredPermission { get; }

    /// <summary>Grouping category (e.g., "Products", "Users", "Orders").</summary>
    string Category { get; }

    /// <summary>
    /// Whether this tool only reads data (no mutations). Purely a hint for UI grouping and LLM prompting;
    /// it does NOT bypass <see cref="RequiredPermission"/> enforcement.
    /// </summary>
    bool IsReadOnly { get; }
}
