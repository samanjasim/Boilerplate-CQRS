using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Access;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Infrastructure.Access;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Identity;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Services.Costs;
using Starter.Module.AI.Infrastructure.Services.Pricing;
using Starter.Module.AI.Infrastructure.Services;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Faithfulness;
using Starter.Module.AI.Infrastructure.Eval;
using Starter.Module.AI.Infrastructure.Eval.Faithfulness;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Starter.Module.AI.Infrastructure.Settings;
using Starter.Module.AI.Infrastructure.Persistence.Seed;

namespace Starter.Module.AI;

public sealed class AIModule : IModule
{
    public string Name => "Starter.Module.AI";
    public string DisplayName => "AI Integration";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiRagSettings>()
            .Bind(configuration.GetSection(AiRagSettings.SectionName))
            .Validate(
                s => s.RerankStrategy != Application.Services.Retrieval.RerankStrategy.FallbackRrf,
                "AI:Rag:RerankStrategy: 'FallbackRrf' is a runtime outcome (reported via RerankResult.StrategyUsed), not a valid configuration value. Use Off, Listwise, Pointwise, or Auto.")
            .ValidateOnStart();
        services.Configure<AiQdrantSettings>(configuration.GetSection(AiQdrantSettings.SectionName));
        services.Configure<AiOcrSettings>(configuration.GetSection(AiOcrSettings.SectionName));
        services.Configure<AiRagEvalSettings>(
            configuration.GetSection(AiRagEvalSettings.SectionName));

        services.AddDbContext<AiDbContext>((sp, options) =>
        {
            // Subscribe to all registered ISaveChangesInterceptors (DomainEventDispatcher,
            // IntegrationEventOutbox, AuditLogAgentAttribution, ...). Without this the AI
            // module's own AssistantUpdatedEvent never publishes and downstream cache
            // invalidation handlers never fire. Matches every other module's registration.
            options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());

            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_AI");
                    npgsqlOptions.MigrationsAssembly(typeof(AiDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddScoped<AnthropicAiProvider>();
        services.AddScoped<OpenAiProvider>();
        services.AddScoped<OllamaAiProvider>();
        services.AddScoped<AiProviderFactory>();
        services.AddScoped<IAiProviderFactory>(sp => sp.GetRequiredService<AiProviderFactory>());
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IUsageMetricCalculator, AiUsageMetricCalculator>();
        services.AddScoped<IChatExecutionService, ChatExecutionService>();

        // Agent runtime (Plan 5a)
        services.AddScoped<IAgentToolDispatcher, AgentToolDispatcher>();
        services.AddScoped<OpenAiAgentRuntime>();
        services.AddScoped<AnthropicAgentRuntime>();
        services.AddScoped<OllamaAgentRuntime>();
        services.AddScoped<IAiAgentRuntimeFactory, AiAgentRuntimeFactory>();

        // Personas (Plan 5b)
        services.AddScoped<ISlugGenerator, SlugGenerator>();
        services.AddScoped<IPersonaResolver, PersonaResolver>();
        services.AddScoped<ISafetyPresetClauseProvider, ResxSafetyPresetClauseProvider>();
        services.AddScoped<IPersonaContextAccessor, PersonaContextAccessor>();

        // Pricing + cost enforcement (Plan 5d-1)
        services.AddScoped<IModelPricingService, ModelPricingService>();
        services.AddScoped<ICostCapResolver, CostCapResolver>();
        services.AddSingleton<ICostCapAccountant, RedisCostCapAccountant>();
        services.AddSingleton<IAgentRateLimiter, RedisAgentRateLimiter>();
        services.AddScoped<IAgentPermissionResolver, AgentPermissionResolver>();
        services.AddHostedService<Infrastructure.Background.AiCostReconciliationJob>();
        services.AddHostedService<Infrastructure.Background.AiPendingApprovalExpirationJob>();

        // Moderation + safety profile resolution (Plan 5d-2)
        services.AddScoped<ISafetyProfileResolver, SafetyProfileResolver>();
        services.AddScoped<IPendingApprovalService, PendingApprovalService>();
        services.AddScoped<IModerationKeyResolver, ConfigurationModerationKeyResolver>();
        services.AddScoped<IPiiRedactor, RegexPiiRedactor>();
        services.AddSingleton<IModerationRefusalProvider, ResxModerationRefusalProvider>();
        services.AddScoped<IContentModerator>(sp =>
        {
            var resolver = sp.GetRequiredService<IModerationKeyResolver>();
            var keyAvailable = !string.IsNullOrWhiteSpace(resolver.Resolve());
            if (keyAvailable)
                return ActivatorUtilities.CreateInstance<OpenAiContentModerator>(sp);

            // Spec §5.1 — without an explicit opt-in, refuse to silently register
            // NoOpContentModerator. The factory only fires lazily on first resolution
            // (i.e., on the first chat invocation in a missing-key tenant), so this is
            // a per-tenant lazy-fail, not a startup-fail. For 5d-2 simplicity that is
            // acceptable — admins see the failure on first chat rather than at boot.
            var allowFallback = configuration.GetValue<bool>("Ai:Moderation:AllowUnmoderatedFallback");
            if (allowFallback)
                return new NoOpContentModerator();

            throw new InvalidOperationException(
                "No OpenAI moderation key configured (AI:Moderation:OpenAi:ApiKey or AI:Providers:OpenAI:ApiKey). " +
                "Set the key, or set Ai:Moderation:AllowUnmoderatedFallback=true to permit unmoderated runs (Standard preset only).");
        });
        services.AddScoped<CurrentAgentRunContextAccessor>();
        services.AddScoped<ICurrentAgentRunContextAccessor>(sp =>
            sp.GetRequiredService<CurrentAgentRunContextAccessor>());

        services.AddSingleton<TokenCounter>();

        var ocrEnabled = configuration.GetValue<bool?>("AI:Ocr:Enabled") ?? true;
        if (ocrEnabled)
            services.AddScoped<IOcrService, Infrastructure.Ingestion.Ocr.TesseractOcrService>();
        else
            services.AddScoped<IOcrService, Infrastructure.Ingestion.Ocr.NullOcrService>();

        services.AddSingleton<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.PlainTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.CsvTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.DocxTextExtractor>();
        services.AddScoped<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.PdfTextExtractor>();
        services.AddScoped<IDocumentTextExtractorRegistry, Infrastructure.Ingestion.DocumentTextExtractorRegistry>();
        services.AddSingleton<Infrastructure.Ingestion.HierarchicalDocumentChunker>();
        services.AddSingleton<Infrastructure.Ingestion.Structured.MarkdownBlockTokenizer>();
        services.AddSingleton<Infrastructure.Ingestion.Structured.HtmlToMarkdownConverter>();
        services.AddSingleton<Infrastructure.Ingestion.Structured.StructuredMarkdownChunker>();
        services.AddSingleton<IDocumentChunker, Infrastructure.Ingestion.Structured.ChunkerRouter>();
        services.AddSingleton<RagCircuitBreakerRegistry>();
        services.AddSingleton<Infrastructure.Ingestion.QdrantVectorStore>();
        services.AddSingleton<IVectorStore>(sp => new CircuitBreakingVectorStore(
            sp.GetRequiredService<Infrastructure.Ingestion.QdrantVectorStore>(),
            sp.GetRequiredService<RagCircuitBreakerRegistry>()));
        services.AddScoped<Infrastructure.Ingestion.EmbeddingService>();
        services.AddScoped<IEmbeddingService>(sp => new Infrastructure.Ingestion.CachingEmbeddingService(
            sp.GetRequiredService<Infrastructure.Ingestion.EmbeddingService>(),
            sp.GetRequiredService<Starter.Application.Common.Interfaces.ICacheService>(),
            sp.GetRequiredService<Infrastructure.Providers.IAiProviderFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Settings.AiRagSettings>>()));
        services.AddScoped<Infrastructure.Retrieval.PostgresKeywordSearchService>();
        services.AddScoped<IKeywordSearchService>(sp => new CircuitBreakingKeywordSearch(
            sp.GetRequiredService<Infrastructure.Retrieval.PostgresKeywordSearchService>(),
            sp.GetRequiredService<RagCircuitBreakerRegistry>()));
        services.AddScoped<IQueryRewriter, Infrastructure.Retrieval.QueryRewriting.QueryRewriter>();
        services.AddScoped<IContextualQueryResolver, Infrastructure.Retrieval.QueryRewriting.ContextualQueryResolver>();
        services.AddScoped<Infrastructure.Retrieval.Reranking.RerankStrategySelector>(sp =>
            new Infrastructure.Retrieval.Reranking.RerankStrategySelector(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiRagSettings>>().Value));
        services.AddScoped<Infrastructure.Retrieval.Reranking.ListwiseReranker>();
        services.AddScoped<Infrastructure.Retrieval.Reranking.PointwiseReranker>();
        services.AddScoped<IReranker, Infrastructure.Retrieval.Reranking.Reranker>();
        services.AddScoped<IQuestionClassifier, QuestionClassifier>();
        services.AddScoped<INeighborExpander, NeighborExpander>();
        services.AddScoped<IRagRetrievalService, Infrastructure.Retrieval.RagRetrievalService>();

        services.AddSingleton<IAiToolRegistry, AiToolRegistryService>();
        services.AddAiToolsFromAssembly(typeof(AIModule).Assembly);
        services.AddAiAgentTemplatesFromAssembly(typeof(AIModule).Assembly);
        services.AddSingleton<IAiAgentTemplateRegistry, AiAgentTemplateRegistry>();
        services.AddHostedService<AiToolRegistrySyncHostedService>();

        services.AddScoped<IResourceOwnershipHandler, AiAssistantOwnershipHandler>();

        services.AddScoped<EvalFixtureIngester>();
        services.AddScoped<IFaithfulnessJudge, LlmJudgeFaithfulness>();
        services.AddScoped<IRagEvalHarness, RagEvalHarness>();

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (AiPermissions.Chat, "Send messages to AI assistants", "AI");
        yield return (AiPermissions.ViewConversations, "View AI conversation history", "AI");
        yield return (AiPermissions.DeleteConversation, "Delete AI conversations", "AI");
        yield return (AiPermissions.ManageAssistants, "Create and manage AI assistants", "AI");
        yield return (AiPermissions.ManageDocuments, "Upload and manage AI knowledge documents", "AI");
        yield return (AiPermissions.ManageTools, "Create and manage AI tools", "AI");
        yield return (AiPermissions.ManageTriggers, "Create and manage AI agent triggers", "AI");
        yield return (AiPermissions.ViewUsage, "View AI usage statistics", "AI");
        yield return (AiPermissions.RunAgentTasks, "Execute AI agent tasks", "AI");
        yield return (AiPermissions.ManageSettings, "Manage AI module settings", "AI");
        yield return (AiPermissions.SearchKnowledgeBase, "Search knowledge base content directly", "AI");
        yield return (AiPermissions.RunEval, "Run RAG evaluation harness", "AI");
        yield return (AiPermissions.ViewPersonas, "View AI personas", "AI");
        yield return (AiPermissions.ManagePersonas, "Create and manage AI personas", "AI");
        yield return (AiPermissions.AssignPersona, "Assign AI personas to users", "AI");
        yield return (AiPermissions.AssignAgentRole, "Assign roles to AI agent principals", "AI");
        yield return (AiPermissions.ManageAgentBudget, "Set per-agent cost caps and rate limits", "AI");
        yield return (AiPermissions.ManagePricing, "Manage AI model pricing (superadmin)", "AI");
        yield return (AiPermissions.SafetyProfilesManage, "Manage AI safety preset profiles", "AI");
        yield return (AiPermissions.AgentsApproveAction, "Approve or deny dangerous AI agent actions", "AI");
        yield return (AiPermissions.AgentsViewApprovals, "View pending AI agent approval inbox", "AI");
        yield return (AiPermissions.ModerationView, "View AI moderation events", "AI");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            AiPermissions.Chat,
            AiPermissions.ViewConversations,
            AiPermissions.DeleteConversation,
            AiPermissions.ManageAssistants,
            AiPermissions.ManageDocuments,
            AiPermissions.ManageTools,
            AiPermissions.ManageTriggers,
            AiPermissions.ViewUsage,
            AiPermissions.RunAgentTasks,
            AiPermissions.ManageSettings,
            AiPermissions.SearchKnowledgeBase,
            AiPermissions.RunEval,
            AiPermissions.ViewPersonas,
            AiPermissions.ManagePersonas,
            AiPermissions.AssignPersona,
            AiPermissions.AssignAgentRole,
            AiPermissions.ManageAgentBudget,
            AiPermissions.ManagePricing,
            AiPermissions.SafetyProfilesManage,
            AiPermissions.AgentsApproveAction,
            AiPermissions.AgentsViewApprovals,
            AiPermissions.ModerationView
        ]);

        yield return ("Admin", [
            AiPermissions.Chat,
            AiPermissions.ViewConversations,
            AiPermissions.DeleteConversation,
            AiPermissions.ManageAssistants,
            AiPermissions.ManageDocuments,
            AiPermissions.ManageTools,
            AiPermissions.ManageTriggers,
            AiPermissions.ViewUsage,
            AiPermissions.RunAgentTasks,
            AiPermissions.SearchKnowledgeBase,
            AiPermissions.ViewPersonas,
            AiPermissions.ManagePersonas,
            AiPermissions.AssignPersona,
            AiPermissions.AssignAgentRole,
            AiPermissions.ManageAgentBudget,
            AiPermissions.SafetyProfilesManage,
            AiPermissions.AgentsApproveAction,
            AiPermissions.AgentsViewApprovals,
            AiPermissions.ModerationView
        ]);

        yield return ("User", [
            AiPermissions.Chat,
            AiPermissions.ViewConversations,
            AiPermissions.DeleteConversation,
            AiPermissions.ViewPersonas,
            AiPermissions.AgentsViewApprovals
        ]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        // Always seed role metadata (idempotent — locks SuperAdmin/TenantAdmin from agent assignment)
        var aiDb = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        await AiRoleMetadataSeed.SeedAsync(aiDb, appDb, cancellationToken);

        // Always seed model pricing (idempotent — skips if any rows already exist)
        await ModelPricingSeed.SeedAsync(aiDb, cancellationToken);

        // Always seed platform-default safety preset profiles (idempotent — skips if any platform row exists)
        await SafetyPresetProfileSeed.SeedAsync(aiDb, cancellationToken);

        // Plan 5d-1 backfill: pair any pre-existing assistant with an AiAgentPrincipal
        // (idempotent — only inserts for assistants without a paired principal).
        await AgentPrincipalBackfill.RunAsync(aiDb, cancellationToken);

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>("AI:InstallDemoTemplatesOnStartup"))
            return;

        var registry = scope.ServiceProvider.GetRequiredService<IAiAgentTemplateRegistry>();
        var demoSlugs = registry.GetAll().Select(t => t.Slug).ToList();
        if (demoSlugs.Count == 0)
            return;

        var tenantIds = await appDb.Tenants
            .IgnoreQueryFilters()
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        if (tenantIds.Count == 0)
            return;

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var logger = scope.ServiceProvider.GetService<ILogger<AIModule>>();

        foreach (var tenantId in tenantIds)
        {
            foreach (var slug in demoSlugs)
            {
                var result = await mediator.Send(
                    new InstallTemplateCommand(slug, TargetTenantId: tenantId, CreatedByUserIdOverride: Guid.Empty),
                    cancellationToken);

                if (result.IsFailure)
                {
                    var code = result.Error.Code;
                    if (code == "Template.AlreadyInstalled")
                        logger?.LogDebug("Demo template {Slug} already installed in tenant {TenantId}; skipping.",
                            slug, tenantId);
                    else
                        logger?.LogWarning("Demo template install failed: tenant={TenantId} slug={Slug} code={Code} message={Message}",
                            tenantId, slug, code, result.Error.Description);
                }
            }
        }
    }
}
