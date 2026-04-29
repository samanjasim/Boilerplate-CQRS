using System.Reflection;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Entities;
using Starter.Infrastructure.Identity;
using Starter.Infrastructure.Identity.Authentication;
using Starter.Infrastructure.Persistence;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.CreateProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.Widgets.CreateWidgetCredential;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Controllers;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

public sealed class Plan5fAcidTests
{
    [Fact]
    public void Acid_5f_1_Default_Tenant_Policy_Is_PlatformOnly()
    {
        var settings = AiTenantSettings.CreateDefault(Guid.NewGuid());

        settings.RequestedProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.PlatformOnly);
        settings.DefaultSafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public async Task Acid_5f_2_Byok_Disabled_Cannot_Create_Tenant_Provider_Credential()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var protector = new Mock<IAiSecretProtector>();
        protector.Setup(x => x.Protect(It.IsAny<string>())).Returns("protected");
        protector.Setup(x => x.Prefix(It.IsAny<string>())).Returns("sk-test");
        protector.Setup(x => x.Mask(It.IsAny<string>())).Returns("sk-****");
        var handler = new CreateProviderCredentialCommandHandler(
            aiDb,
            appDb,
            CurrentUser(tenantId).Object,
            EntitlementResolver(Entitlements(byokEnabled: false)).Object,
            protector.Object);

        var result = await handler.Handle(new CreateProviderCredentialCommand(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI",
            "sk-secret"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.ByokDisabledByPlan");
        (await aiDb.AiProviderCredentials.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Acid_5f_3_TenantKeysRequired_Fails_Before_Provider_Call_When_Key_Missing()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.TenantKeysRequired, SafetyPreset.Standard);
        var resolver = new AiProviderCredentialResolver(
            aiDb,
            new StaticTenantSettingsResolver(settings),
            EntitlementResolver(Entitlements(byokEnabled: true)).Object,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AI:Providers:OpenAI:ApiKey"] = "sk-platform-available"
                })
                .Build(),
            Mock.Of<IAiSecretProtector>());

        var result = await resolver.ResolveAsync(tenantId, AiProviderType.OpenAI);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.TenantKeyRequired");
    }

    [Fact]
    public async Task Acid_5f_4_Byok_Run_Does_Not_Consume_Platform_Credit()
    {
        var tenantId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var accountant = AccountantGranted();
        var runtime = BuildRuntime(accountant);

        var result = await runtime.RunAsync(
            RunContext(tenantId, assistantId, ProviderCredentialSource.Tenant),
            Mock.Of<IAgentRunSink>());

        result.Status.Should().Be(AgentRunStatus.Completed);
        accountant.Verify(a => a.TryClaimAsync(
            tenantId,
            assistantId,
            It.IsAny<decimal>(),
            It.IsAny<CapWindow>(),
            It.IsAny<decimal>(),
            CostCapBucket.PlatformCredit,
            It.IsAny<CancellationToken>()), Times.Never);
        accountant.Verify(a => a.RecordActualAsync(
            tenantId,
            assistantId,
            It.IsAny<decimal>(),
            It.IsAny<CapWindow>(),
            CostCapBucket.PlatformCredit,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Acid_5f_5a_Widget_Credential_Does_Not_Create_Core_ApiKey_Row()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var widget = Widget(tenantId);
        aiDb.AiPublicWidgets.Add(widget);
        await aiDb.SaveChangesAsync();
        var handler = new CreateWidgetCredentialCommandHandler(aiDb, CurrentUser(tenantId).Object);

        var result = await handler.Handle(new CreateWidgetCredentialCommand(widget.Id, ExpiresAt: null), default);

        result.IsSuccess.Should().BeTrue();
        (await aiDb.AiWidgetCredentials.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await appDb.Set<ApiKey>().IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Acid_5f_5b_Widget_Credential_Cannot_Authenticate_Core_ApiKey_Handler()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var widget = Widget(tenantId);
        aiDb.AiPublicWidgets.Add(widget);
        await aiDb.SaveChangesAsync();
        var widgetHandler = new CreateWidgetCredentialCommandHandler(aiDb, CurrentUser(tenantId).Object);
        var widgetKey = await widgetHandler.Handle(new CreateWidgetCredentialCommand(widget.Id, ExpiresAt: null), default);
        widgetKey.IsSuccess.Should().BeTrue();

        var authHandler = new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            appDb);
        var context = new DefaultHttpContext();
        context.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = widgetKey.Value.FullKey;
        await authHandler.InitializeAsync(
            new AuthenticationScheme(ApiKeyAuthenticationHandler.SchemeName, displayName: null, typeof(ApiKeyAuthenticationHandler)),
            context);

        var auth = await authHandler.AuthenticateAsync();

        auth.Succeeded.Should().BeFalse();
        auth.Failure.Should().NotBeNull();
        auth.Failure!.Message.Should().Be("Invalid API key.");
    }

    [Fact]
    public async Task Acid_5f_6_No_Public_Auth_Surface_Wired_For_Widget_Credentials()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "0123456789abcdef0123456789abcdef",
                ["JwtSettings:Issuer"] = "tests",
                ["JwtSettings:Audience"] = "tests"
            })
            .Build());
        using var provider = services.BuildServiceProvider();
        var schemes = await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();

        schemes.Select(s => s.Name).Should().NotContain(s => s.StartsWith("AiWidget", StringComparison.Ordinal));
        schemes.Select(s => s.HandlerType?.FullName ?? "")
            .Should().NotContain(h => h.Contains("AiWidgetCredential", StringComparison.Ordinal));

        var aiControllerTypes = typeof(AiSettingsController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .ToArray();

        var routeTemplates = aiControllerTypes
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>(inherit: false).Select(r => r.Template ?? ""))
            .Concat(aiControllerTypes.SelectMany(t => t
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: false).Select(a => a.Template ?? ""))))
            .ToArray();

        routeTemplates.Should().NotContain(r =>
            r.Contains("public", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("public/ai/widgets", StringComparison.OrdinalIgnoreCase));

        var anonymousWidgetActions = aiControllerTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Where(m => (m.DeclaringType?.FullName + "." + m.Name)
                .Contains("WidgetCredential", StringComparison.OrdinalIgnoreCase))
            .ToList();

        anonymousWidgetActions.Should().BeEmpty();
    }

    private static CostCapEnforcingAgentRuntime BuildRuntime(Mock<ICostCapAccountant> accountant)
    {
        var caps = new Mock<ICostCapResolver>();
        caps.Setup(c => c.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveCaps(20m, 2m, 60, 10m, 1m));

        var rateLimiter = new Mock<IAgentRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var pricing = new Mock<IModelPricingService>();
        pricing.SetupSequence(p => p.EstimateCostAsync(
                It.IsAny<AiProviderType>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1m)
            .ReturnsAsync(0.6m);

        return new CostCapEnforcingAgentRuntime(
            new CompletedRuntime(),
            caps.Object,
            accountant.Object,
            rateLimiter.Object,
            pricing.Object,
            NullLogger<CostCapEnforcingAgentRuntime>.Instance);
    }

    private static Mock<ICostCapAccountant> AccountantGranted()
    {
        var accountant = new Mock<ICostCapAccountant>();
        accountant.Setup(a => a.TryClaimAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<CapWindow>(),
                It.IsAny<decimal>(),
                It.IsAny<CostCapBucket>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, decimal amount, CapWindow _, decimal cap, CostCapBucket _, CancellationToken _) =>
                new ClaimResult(true, amount, cap));
        return accountant;
    }

    private static AgentRunContext RunContext(Guid tenantId, Guid assistantId, ProviderCredentialSource source) =>
        new(
            Messages: new[] { new AiChatMessage("user", "hello") },
            SystemPrompt: "system",
            ModelConfig: new AgentModelConfig(AiProviderType.OpenAI, "gpt-4o-mini", 0.7, 100),
            Tools: new ToolResolutionResult(
                ProviderTools: Array.Empty<AiToolDefinitionDto>(),
                DefinitionsByName: new Dictionary<string, IAiToolDefinition>()),
            MaxSteps: 1,
            LoopBreak: LoopBreakPolicy.Default,
            AssistantId: assistantId,
            TenantId: tenantId,
            ProviderCredentialSource: source);

    private static AiPublicWidget Widget(Guid tenantId) =>
        AiPublicWidget.Create(
            tenantId,
            "Public support widget",
            ["https://example.com"],
            defaultAssistantId: null,
            defaultPersonaSlug: AiPersona.AnonymousSlug,
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

    private static AiDbContext CreateAiDb(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"plan-5f-ai-{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options, CurrentUser(tenantId).Object);
    }

    private static ApplicationDbContext CreateAppDb(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"plan-5f-core-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options, CurrentUser(tenantId).Object);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid? tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.Email).Returns("admin@example.test");
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        return currentUser;
    }

    private static Mock<IAiEntitlementResolver> EntitlementResolver(AiEntitlementsDto entitlements)
    {
        var resolver = new Mock<IAiEntitlementResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlements);
        return resolver;
    }

    private static AiEntitlementsDto Entitlements(bool byokEnabled = true) =>
        new(
            TotalMonthlyUsd: 20m,
            TotalDailyUsd: 2m,
            PlatformMonthlyUsd: 10m,
            PlatformDailyUsd: 1m,
            RequestsPerMinute: 60,
            ByokEnabled: byokEnabled,
            WidgetsEnabled: true,
            WidgetMaxCount: 3,
            WidgetMonthlyTokens: 50_000,
            WidgetDailyTokens: 5_000,
            WidgetRequestsPerMinute: 30,
            AllowedProviders: ["OpenAI", "Anthropic"],
            AllowedModels: ["gpt-4o-mini"]);

    private sealed class StaticTenantSettingsResolver(AiTenantSettings settings) : IAiTenantSettingsResolver
    {
        public Task<AiTenantSettings> GetOrDefaultAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(settings);

        public Task<ProviderCredentialPolicy> ResolveEffectivePolicyAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(settings.RequestedProviderCredentialPolicy);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => NullDisposable.Instance;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }

    private sealed class CompletedRuntime : IAiAgentRuntime
    {
        public Task<AgentRunResult> RunAsync(AgentRunContext context, IAgentRunSink sink, CancellationToken ct = default)
        {
            return Task.FromResult(new AgentRunResult(
                AgentRunStatus.Completed,
                FinalContent: "ok",
                Steps: Array.Empty<AgentStepEvent>(),
                TotalInputTokens: 10,
                TotalOutputTokens: 10,
                TerminationReason: null));
        }
    }
}
