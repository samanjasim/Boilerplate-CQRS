// AUTO-GENERATED — DO NOT EDIT.
// Regenerate with `npm run generate:modules` from the repo root.
// CI fails on drift; modules.catalog.json is the single source of truth.
//
// Source: modules.catalog.json

using Starter.Abstractions.Modularity;

namespace Starter.Api.Modularity;

/// <summary>
/// Generated module registry. Used by the API host and out-of-process
/// tooling (<c>Program.ConfigureServicesForTooling</c>) instead of the
/// reflection-based <c>ModuleLoader.DiscoverModules()</c>. Discover
/// remains for tests that need runtime introspection.
/// </summary>
public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> All()
    {
        return new IModule[]
        {
            new Starter.Module.AI.AIModule(),
            new Starter.Module.Billing.BillingModule(),
            new Starter.Module.CommentsActivity.CommentsActivityModule(),
            new Starter.Module.Communication.CommunicationModule(),
            new Starter.Module.ImportExport.ImportExportModule(),
            new Starter.Module.Products.ProductsModule(),
            new Starter.Module.Webhooks.WebhooksModule(),
            new Starter.Module.Workflow.WorkflowModule(),
        };
    }
}
