namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Marker interface for capability contracts.
///
/// A capability is a service that core code may depend on, but whose
/// implementation lives in a module that may or may not be installed.
/// Core registers a Null Object fallback by default; modules replace it
/// when loaded. This lets core code call the capability unconditionally
/// without null checks or feature flags.
///
/// ── WHERE CAPABILITY CONTRACTS LIVE ──
///
/// All capability contracts live in this namespace
/// (<c>Starter.Abstractions.Capabilities</c>). There is no split — modules
/// looking for the contract to implement, or core looking for the contract
/// to consume, both look in exactly one place.
///
/// Current contracts:
/// <list type="bullet">
///   <item><c>IQuotaChecker</c></item>
///   <item><c>IBillingProvider</c></item>
///   <item><c>IWebhookPublisher</c></item>
///   <item><c>IImportExportRegistry</c></item>
///   <item><c>IUsageMetricCalculator</c></item>
///   <item><c>IMessageDispatcher</c></item>
///   <item><c>ICommunicationEventNotifier</c></item>
///   <item><c>ITemplateRegistrar</c></item>
/// </list>
///
/// ── DEPENDENCY RULES ──
///
/// <c>Starter.Abstractions</c> has **zero project references**. All types used
/// in capability contract signatures (including former domain enums like
/// <c>BillingInterval</c> and <c>FieldType</c>) live inside
/// <c>Starter.Abstractions.Capabilities</c> itself.
///
/// <c>Starter.Abstractions</c> may reference:
/// <list type="bullet">
///   <item><c>Microsoft.Extensions.*.Abstractions</c> (pure interface packages)</item>
/// </list>
///
/// <c>Starter.Abstractions</c> must NOT reference:
/// <list type="bullet">
///   <item>Any <c>Starter.*</c> project (including <c>Starter.Domain</c> and <c>Starter.Shared</c>)</item>
///   <item><c>Starter.Abstractions.Web</c> (only modules and the API host depend on web helpers)</item>
///   <item>Any framework package (ASP.NET, EF Core, MassTransit, etc.)</item>
/// </list>
///
/// When a capability contract needs a value type (enum, record, etc.)
/// that doesn't exist yet, define it inside this namespace rather than
/// adding a project reference. See <c>BillingInterval</c> and
/// <c>FieldType</c> for examples.
///
/// These rules are enforced by
/// <c>Starter.Api.Tests/Architecture/AbstractionsPurityTests.cs</c>.
/// </summary>
public interface ICapability
{
}
