using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Services;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Starter.Module.AI.Infrastructure.Settings;

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

        services.AddDbContext<AiDbContext>(options =>
        {
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
        services.AddSingleton<IVectorStore, Infrastructure.Ingestion.QdrantVectorStore>();
        services.AddScoped<Infrastructure.Ingestion.EmbeddingService>();
        services.AddScoped<IEmbeddingService>(sp => new Infrastructure.Ingestion.CachingEmbeddingService(
            sp.GetRequiredService<Infrastructure.Ingestion.EmbeddingService>(),
            sp.GetRequiredService<Starter.Application.Common.Interfaces.ICacheService>(),
            sp.GetRequiredService<Infrastructure.Providers.IAiProviderFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Settings.AiRagSettings>>()));
        services.AddScoped<IKeywordSearchService, Infrastructure.Retrieval.PostgresKeywordSearchService>();
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
        services.AddSingleton<IAiToolDefinition, Infrastructure.Tools.ListMyConversationsAiTool>();
        services.AddHostedService<AiToolRegistrySyncHostedService>();

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
            AiPermissions.SearchKnowledgeBase
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
            AiPermissions.SearchKnowledgeBase
        ]);

        yield return ("User", [
            AiPermissions.Chat,
            AiPermissions.ViewConversations,
            AiPermissions.DeleteConversation
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
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>("AI:SeedSampleAssistant"))
            return;

        var context = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        const string SampleName = "AI Tools Demo";
        var exists = await context.AiAssistants
            .AnyAsync(a => a.Name == SampleName, cancellationToken);

        if (exists)
            return;

        var sample = AiAssistant.Create(
            tenantId: null,
            name: SampleName,
            description: "Demonstrates AI function calling. Ask about your conversations.",
            systemPrompt:
                "You are a friendly assistant. When the user asks about their own " +
                "conversations, call the list_my_conversations tool and summarise the results.",
            provider: null,
            model: null,
            temperature: 0.2,
            maxTokens: 1024,
            executionMode: AssistantExecutionMode.Chat,
            maxAgentSteps: 5,
            isActive: true);
        sample.SetEnabledTools(new[] { "list_my_conversations" });
        context.AiAssistants.Add(sample);
        await context.SaveChangesAsync(cancellationToken);
    }
}
