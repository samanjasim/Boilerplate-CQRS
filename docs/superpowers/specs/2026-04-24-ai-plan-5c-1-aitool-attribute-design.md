# AI Module — Plan 5c-1: `[AiTool]` Attribute + Auto-Discovery (Design)

**Date:** 2026-04-24
**Status:** Design approved; plan pending
**Parent plan family:** Plan 5c — Agent Templates (see revised vision `2026-04-23-ai-module-vision-revised-design.md`)
**Successor:** Plan 5c-2 — Agent Templates (bundles, install, fork, starter content)

---

## 1. Purpose

Introduce an attribute-based mechanism for domain modules to expose MediatR commands and queries as AI-callable tools. Today, every tool requires a dedicated `IAiToolDefinition` class with a hand-written JSON Schema. Plan 5c-1 replaces that ceremony with `[AiTool]` on the command type, auto-derives the JSON Schema from the record shape, and auto-registers via per-module assembly scan.

This is one of the two substrate primitives underneath the Plan 5c vision. Templates (5c-2) reference tools by name — so a name-addressable, module-grouped catalog has to exist first.

### Out of scope

- Agent templates, install flow, fork semantics, per-tenant template seed — **deferred to 5c-2.**
- Bundled starter agents (Tutor, Brand Content Agent) — **deferred to 5c-2 / 5e.**
- Broad `[AiTool]` adoption across every module — this plan exercises the abstraction on two representative read-only queries (one module + one core) and leaves further adoption to the owning modules.
- Cost caps, rate limits, `[DangerousAction]` pause, content moderation — **deferred to 5d.**
- Runtime-computed schemas (dynamic tool shapes) — remain on the existing `IAiToolDefinition` escape hatch.

---

## 2. Context (current state as of 2026-04-24)

Relevant to this plan (verified against the current tree):

- **Tool contract.** `IAiToolDefinition` in [Starter.Abstractions.Capabilities](../../boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs) defines `Name`, `Description`, `ParameterSchema` (`JsonElement`), `CommandType`, `RequiredPermission`, `Category`, `IsReadOnly`. Its docstring enshrines the LLM-safe command contract: the LLM must never set server-trusted fields, the registrar picks the command shape carefully, FluentValidation handles adversarial input, handlers tolerate untrusted arguments.
- **Registry.** `IAiToolRegistry` (internal) joins code-defined definitions with the DB-tracked `AiTool` enable/disable row. `AiToolRegistrySyncHostedService` syncs the two on startup. `ResolveForAssistantAsync` does the per-turn intersection of `assistant.EnabledToolNames × admin-enabled × user-permitted`.
- **Existing tools.** One tool exists: `ListMyConversationsAiTool` ([Infrastructure/Tools/ListMyConversationsAiTool.cs](../../boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs)) wrapping `GetConversationsQuery`. Registered explicitly in `AIModule.ConfigureServices` as `services.AddSingleton<IAiToolDefinition, ListMyConversationsAiTool>()`.
- **Core application DI.** `Starter.Application.DependencyInjection.AddApplication(services, moduleAssemblies)` scans MediatR handlers and FluentValidation validators across core + all module assemblies. Good precedent for per-assembly scanning.
- **Module DI.** Each `IModule` implementation (AI, Products, Users, etc.) owns its own `ConfigureServices(services, configuration)`. Modules do not depend on AI module internals — the abstraction must live in `Starter.Abstractions.Capabilities`.
- **Candidate queries for sample adoption.** `GetProductsQuery` in [Starter.Module.Products](../../boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProducts/GetProductsQuery.cs) (has a `TenantId` field used for superadmin cross-tenant filtering — must be stripped from the LLM-facing schema); `GetUsersQuery` in [Starter.Application](../../boilerplateBE/src/Starter.Application/Features/Users/Queries/GetUsers/GetUsersQuery.cs) (inherits `PaginationQuery`; no server-trusted fields).
- **.NET 10.** `System.Text.Json.Schema.JsonSchemaExporter.GetJsonSchemaAsNode(JsonSerializerOptions, Type)` is available in-box; no NuGet dependency needed for schema generation.

---

## 3. Public contract

### 3.1 `[AiTool]` attribute

New file: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolAttribute.cs`.

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Marks a MediatR request type (command or query) as an AI-callable tool. At DI registration
/// time the attributed type is wrapped in an IAiToolDefinition adapter, the JSON Schema is
/// auto-derived from the record shape, and the adapter is added to the tool catalog.
///
/// See IAiToolDefinition for the LLM-safe command contract — all of which applies to attributed
/// commands identically. In particular: the attribute MUST NOT be applied to a command whose
/// record shape contains fields bound to server-trusted state (user id, tenant id, role flags)
/// unless those fields are explicitly excluded via [AiParameterIgnore].
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

    /// <summary>Hint for UI + LLM only; does not bypass RequiredPermission.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Optional JSON Schema override. When set, skips auto-derivation entirely and uses the
    /// supplied schema verbatim. Use when the schema cannot be expressed by the record shape
    /// (dynamic enums, polymorphic payloads). Prefer auto-derivation when possible.
    /// </summary>
    public string? ParameterSchemaJson { get; init; }
}
```

### 3.2 `[AiParameterIgnore]` attribute

New file: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiParameterIgnoreAttribute.cs`.

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Excludes a property from the auto-derived JSON Schema of a tool. Apply to fields that exist
/// on the command for non-LLM callers (e.g., superadmin cross-tenant TenantId override) but
/// must not be set by the LLM. The property is left on the type and unchanged during non-LLM
/// dispatch.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class AiParameterIgnoreAttribute : Attribute;
```

### 3.3 Property-level description

Property descriptions on the generated schema come from `System.ComponentModel.DataAnnotations.DescriptionAttribute` (the standard `[Description("...")]`). No new attribute is introduced for this — modules use the BCL one.

### 3.4 Example usage

```csharp
using System.ComponentModel;
using Starter.Abstractions.Capabilities;

[AiTool(
    Name = "list_products",
    Description = "List products in the current tenant, paged and optionally filtered by status.",
    Category = "Products",
    RequiredPermission = Permissions.Products.Read,
    IsReadOnly = true)]
public sealed record GetProductsQuery(
    [property: Description("Page number, 1-based. Default 1.")]
    int PageNumber = 1,
    [property: Description("Page size, 1–100. Default 20.")]
    int PageSize = 20,
    [property: Description("Free-text search across product name and SKU.")]
    string? SearchTerm = null,
    [property: Description("Status filter: 'active', 'draft', or 'archived'.")]
    string? Status = null,
    [AiParameterIgnore] Guid? TenantId = null)
    : IRequest<Result<PaginatedList<ProductDto>>>;
```

---

## 4. Schema inference

At registration time, for each attributed type:

1. **If `ParameterSchemaJson` is set on the attribute →** parse and use it verbatim. Skip steps 2–6.
2. **Generate base schema.** Use `JsonSchemaExporter.GetJsonSchemaAsNode(options, attributedType)` with default `JsonSerializerOptions` augmented to match the rest of the codebase (camelCase property naming, string enum converter) so the schema property names match what the dispatcher deserializes.
3. **Strip ignored properties.** Remove any property from the generated `properties` object whose CLR property carries `[AiParameterIgnore]`. Also remove it from the `required` array if present.
4. **Validate trust boundary.** After stripping, scan the remaining property names against a known set of server-trusted identifiers: `tenantId`, `userId`, `createdByUserId`, `modifiedByUserId`, `impersonatedBy`, `isSystemAdmin`. If any of these appear, fail registration with a startup error naming the attributed type and the offending property — the registrar must either mark the property `[AiParameterIgnore]` or remove it from the record.
5. **Enrich descriptions.** For each property in the schema, look up the CLR property on the type and, if it carries `[Description]`, copy the value into the schema property's `description` field. Properties without a description are left undescribed (they still validate; the LLM just has less context).
6. **Cache.** Store the generated `JsonElement` on the adapter instance; generation does not repeat.

The auto-derived schema is always `"additionalProperties": false` — unknown LLM-supplied fields are rejected during deserialization.

Generator failures (unsupported types, cycles, non-serializable members) surface as startup errors with the offending type name. No silent fallback.

---

## 5. Discovery and registration

### 5.1 Extension method

New file: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolDiscoveryExtensions.cs`.

```csharp
namespace Starter.Abstractions.Capabilities;

public static class AiToolDiscoveryExtensions
{
    /// <summary>
    /// Scans the supplied assembly for types decorated with [AiTool] and registers each as
    /// an IAiToolDefinition. Call once per module from ConfigureServices. Safe to call with
    /// an assembly that has no [AiTool]s — no-op in that case.
    /// </summary>
    public static IServiceCollection AddAiToolsFromAssembly(
        this IServiceCollection services,
        Assembly assembly);
}
```

### 5.2 Mechanism

1. Enumerate public, non-abstract types in the assembly that carry `[AiTool]`.
2. For each candidate:
   - Verify it implements `MediatR.IBaseRequest` (otherwise it can't be dispatched). Startup error if not.
   - Verify `RequiredPermission` is a non-empty string.
   - Generate the schema per section 4. Startup errors surface with the full type name.
   - Construct an `AttributedAiToolDefinition` instance wrapping `(type, attribute, cachedSchemaJsonElement, moduleSource)` where `moduleSource` is derived from `assembly.GetName().Name` by stripping a `Starter.Module.` or `Starter.` prefix (e.g., `Starter.Module.Products` → `"Products"`, `Starter.Application` → `"Application"` — later mapped to `"Core"` in the catalog DTO; see 7.1).
3. After enumeration, validate no intra-assembly `Name` collision. Intra-assembly collision is a fatal error thrown from `AddAiToolsFromAssembly`.
4. Register each adapter as a singleton: `services.AddSingleton<IAiToolDefinition>(adapter)`.
5. **Cross-assembly `Name` collisions** are detected by `IAiToolRegistry` at first enumeration — the existing `AiToolRegistryService` gains a uniqueness check in its constructor (or first `ListAllAsync` call) that throws if any `Name` appears more than once. This is still fail-fast: the check fires before any chat turn runs, during startup when the registry hosted service warms the DB sync.

### 5.3 Adapter

New internal file in the AI module: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/AttributedAiToolDefinition.cs` — or co-located with the scanner in `Starter.Abstractions.Capabilities` since the adapter is a pure data holder implementing the public interface. **Decision:** co-locate with scanner in `Starter.Abstractions.Capabilities` so modules depending only on the abstractions layer can consume the primitive without pulling in the AI module.

```csharp
internal sealed class AttributedAiToolDefinition : IAiToolDefinition
{
    public AttributedAiToolDefinition(
        Type commandType,
        AiToolAttribute attribute,
        JsonElement parameterSchema,
        string moduleSource);

    public string Name { get; }
    public string Description { get; }
    public JsonElement ParameterSchema { get; }
    public Type CommandType { get; }
    public string RequiredPermission { get; }
    public string Category { get; }
    public bool IsReadOnly { get; }

    // Not on IAiToolDefinition — exposed via a separate IAiToolDefinitionModuleSource
    // capability interface so the registry/DTO layer can read it without changing the
    // existing public contract.
    public string ModuleSource { get; }
}

public interface IAiToolDefinitionModuleSource
{
    /// <summary>
    /// The module that owns this tool (e.g., "Products", "AI", "Core"). Null when the
    /// definition is hand-authored and does not report a source.
    /// </summary>
    string? ModuleSource { get; }
}
```

Existing hand-authored `IAiToolDefinition` implementations do not implement `IAiToolDefinitionModuleSource`; they report a null module source, which the DTO falls back to display as `"Unknown"`.

### 5.4 Registration call sites

- `AIModule.ConfigureServices` — already loops into the AI module's assembly; add `services.AddAiToolsFromAssembly(typeof(AIModule).Assembly)`. Remove the explicit `AddSingleton<IAiToolDefinition, ListMyConversationsAiTool>()` line.
- `Starter.Application.DependencyInjection.AddApplication` — add `services.AddAiToolsFromAssembly(typeof(DependencyInjection).Assembly)` at the end so core queries (like `GetUsersQuery`) are picked up.
- `ProductsModule.ConfigureServices` — add `services.AddAiToolsFromAssembly(typeof(ProductsModule).Assembly)` after existing registrations.

Other modules remain untouched in 5c-1 — they adopt `[AiTool]` when their owners decide to.

---

## 6. Sample adoption

Two existing queries receive `[AiTool]` to exercise the primitive end-to-end.

### 6.1 `GetConversationsQuery` (AI module — migrated)

- Delete `Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs`.
- Apply `[AiTool(Name = "list_my_conversations", Category = "AI", RequiredPermission = AiPermissions.ViewConversations, IsReadOnly = true, Description = "List the current user's recent AI conversations with title, message count, and last-message timestamp.")]` to `GetConversationsQuery`.
- Property descriptions via `[Description]` on each field.
- Remove the explicit `AddSingleton<IAiToolDefinition, ListMyConversationsAiTool>()` line in `AIModule.cs` (replaced by `AddAiToolsFromAssembly`).

### 6.2 `GetProductsQuery` (Products module)

- Apply `[AiTool(Name = "list_products", Category = "Products", RequiredPermission = Permissions.Products.Read, IsReadOnly = true, Description = "List products in the current tenant, paged and optionally filtered.")]`.
- `TenantId` property gets `[AiParameterIgnore]` (existing superadmin-only cross-tenant field stays on the record but never hits the LLM schema).
- Property descriptions via `[Description]`.
- `ProductsModule.ConfigureServices` gains the `AddAiToolsFromAssembly` call.

### 6.3 `GetUsersQuery` (core `Starter.Application`)

- Apply `[AiTool(Name = "list_users", Category = "Users", RequiredPermission = Permissions.Users.Read, IsReadOnly = true, Description = "List users in the current tenant, paged and optionally filtered.")]`.
- Property descriptions via `[Description]` on `Status`, `Role`. Paging/sort fields inherit from `PaginationQuery` and get their descriptions from the base record.
- `Starter.Application.DependencyInjection.AddApplication` gains the `AddAiToolsFromAssembly` call for its own assembly.

None of the three queries has its handler or validator modified. This plan only adds the attribute, the `[AiParameterIgnore]` marker where needed, property descriptions, and module-level registration calls.

---

## 7. Registry and catalog impact

### 7.1 `AiToolDto` gains `Module`

The admin-facing tool catalog DTO ([Application/DTOs/AiToolMappers.cs](../../boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolMappers.cs)) adds a `Module` string field. For attributed tools, `Module` comes from the assembly-derived source (with `Starter.Application` mapped to `"Core"` and `Starter.Module.X` mapped to `"X"`). For hand-authored `IAiToolDefinition` instances that don't implement `IAiToolDefinitionModuleSource`, the DTO emits `"Unknown"`.

### 7.2 No change to the registry contract

`IAiToolRegistry.ListAllAsync`, `FindByName`, and `ResolveForAssistantAsync` are unchanged. Attribute-derived adapters are indistinguishable from hand-authored definitions at the registry level — they just arrive via DI.

### 7.3 DB sync unaffected

`AiToolRegistrySyncHostedService` reads all `IAiToolDefinition` services and writes the DB row keyed by `Name`. Attribute-derived tools produce identical DB rows to hand-authored ones; nothing changes in the sync path.

---

## 8. Failure modes

All failures are **fatal at DI composition / startup time** — `AddAiToolsFromAssembly` throws and the process refuses to start. None are warnings, none are silent.

| Condition | Behaviour |
|---|---|
| `Name` collision with an already-registered tool | Startup error listing both types |
| Attributed type does not implement `IBaseRequest` | Startup error with full type name |
| `RequiredPermission` is empty / whitespace | Startup error with full type name |
| Schema generation throws | Startup error wrapping the inner exception; the message names the type |
| Generated schema contains a server-trusted property without `[AiParameterIgnore]` | Startup error listing the type + property |
| `ParameterSchemaJson` override is not valid JSON | Startup error with parse exception |
| Two assemblies register the same attributed type (unlikely, defensive) | Startup error |

Fail-fast reflects the LLM-safety importance of the contract: a silently-dropped tool is a behavior regression; a silently-exposed trust-boundary field is a security incident.

---

## 9. Tests

New folder: `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/`.

| Test class | Coverage |
|---|---|
| `AiToolAttributeDiscoveryTests` | Scan of a test assembly finds types decorated with `[AiTool]`; adapter reports `Name/Description/Category/RequiredPermission/IsReadOnly/CommandType/ModuleSource` correctly. Unannotated types are ignored. |
| `AiToolSchemaGenerationTests` | Record with `[Description]` props produces schema with those descriptions. `[AiParameterIgnore]` properties do not appear in the schema or the `required` array. `ParameterSchemaJson` override wins over auto-derivation (byte-identical). Records containing trust-boundary fields without `[AiParameterIgnore]` throw at registration. Records with cyclic or unsupported property types throw with the type name in the message. |
| `AiToolRegistrationCollisionTests` | Two assemblies registering the same `Name` throw at composition. `IBaseRequest`-less types throw. Missing permission throws. |
| `AiToolRegistryAttributedPathTests` | Attributed tool flows through `IAiToolRegistry.ResolveForAssistantAsync` identically to a hand-authored one — registry join with DB enable/disable row still applies, permission filtering still applies. |
| `AiToolDbSyncAttributedTests` | After startup, `AiToolRegistrySyncHostedService` has written a DB row for every attributed tool with the expected `Name`, `Category`, `Description`, `IsReadOnly`. |

Existing `AiToolRegistryServiceTests` is updated to cover the `GetConversationsQuery` migration (tool by name `"list_my_conversations"` resolves via attribute path).

Test assemblies need to carry `[AiTool]`-decorated types — prefer a small fixture assembly or `InternalsVisibleTo` + internal attributed types inside the test assembly itself. Decision: use internal attributed test-only types inside the test assembly; avoid a dedicated fixture project.

---

## 10. Documentation

- `IAiToolDefinition` docstring updated to recommend `[AiTool]` as the default mechanism and to describe the interface as the escape hatch for dynamic/computed schemas.
- New short doc comment on `AiToolAttribute` explaining usage + pointing at the LLM-safe command contract on `IAiToolDefinition`.
- CLAUDE.md is **not** updated in this plan — attribute-based tool exposure becomes a general pattern only after broader adoption in 5c-2 / 5d. For now a single doc comment is sufficient.

---

## 11. Migration and backward compatibility

- No breaking changes to `IAiToolDefinition`. All existing hand-authored tools continue to work.
- The sole existing hand-authored tool (`ListMyConversationsAiTool`) is migrated; the file is deleted.
- No database migration required — the `AiTool` DB row is keyed by `Name`, and the migrated `GetConversationsQuery` reports the same `Name` (`"list_my_conversations"`), so the existing row is updated in place by the startup sync.
- Permission constants are referenced as string `const` values (attribute limitation), which is already how `IAiToolDefinition` implementations do it (`AiPermissions.ViewConversations` is a `const string`). No change.

---

## 12. Design trade-offs acknowledged

- **Attribute-based registration is compile-time but reflection-heavy at startup.** Mitigation: scan happens once per module at DI composition; schema generation is cached per adapter.
- **`[Description]` reuse from BCL** instead of a custom `[AiParameter]` attribute means descriptions are shared with other schema tooling (Swagger, DataAnnotations UI). That's a feature, not a bug — but a module author who wants a different description for the LLM than for Swagger cannot have it without introducing a new attribute. Accepted: rare enough that `ParameterSchemaJson` override handles it.
- **Module source is assembly-derived, not attribute-set.** A module author cannot override the displayed "module" name. Accepted: assembly name is the source of truth for module identity; custom labels are a 7a/UI concern.
- **Fail-fast startup.** A broken `[AiTool]` brings the whole app down. Accepted: same trade-off the existing `IAiToolDefinition` path makes implicitly — misregistration there surfaces as null-ref at first use, which is worse.

---

## 13. Flagship acid test

This plan does not directly address the Plan 5c flagship acid test (which is about installing templates). But it is the enabling substrate: the Tutor and Brand Content templates in 5c-2 reference tool names like `list_my_students`, `grade_quiz`, `search_brand_assets`. Those tools will be expressed as `[AiTool]`-decorated MediatR commands in their owning modules — the School tenant's school module, the Social tenant's content module, etc. 5c-1 ships the mechanism those modules will use.

---

## 14. Deliverables checklist

Scope-locking list for the plan-writing step:

- [ ] `AiToolAttribute` in `Starter.Abstractions.Capabilities`
- [ ] `AiParameterIgnoreAttribute` in `Starter.Abstractions.Capabilities`
- [ ] `AiToolDiscoveryExtensions.AddAiToolsFromAssembly` in `Starter.Abstractions.Capabilities`
- [ ] `AttributedAiToolDefinition` adapter + `IAiToolDefinitionModuleSource` capability interface in `Starter.Abstractions.Capabilities`
- [ ] Schema generation with trust-boundary validation and description enrichment
- [ ] `AIModule.ConfigureServices` switches from explicit tool registration to `AddAiToolsFromAssembly`; `ListMyConversationsAiTool` deleted; `GetConversationsQuery` decorated
- [ ] `ProductsModule.ConfigureServices` calls `AddAiToolsFromAssembly`; `GetProductsQuery` decorated with `[AiParameterIgnore]` on `TenantId`
- [ ] `Starter.Application.DependencyInjection.AddApplication` calls `AddAiToolsFromAssembly`; `GetUsersQuery` decorated
- [ ] `AiToolDto` gains `Module` field; `GetToolsQueryHandler` populates it; frontend type mirrored
- [ ] Tests per section 9
- [ ] Build + all existing tests green
- [ ] `IAiToolDefinition` docstring updated
