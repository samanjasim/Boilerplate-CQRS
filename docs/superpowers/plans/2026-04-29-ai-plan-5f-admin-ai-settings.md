# AI Plan 5f Admin AI Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Plan 5f backend source of truth for tenant AI settings, BYOK provider credentials, model defaults, cost/self-limit policy, brand defaults, and public widget credentials.

**Architecture:** Keep all AI-specific settings inside `Starter.Module.AI`. Subscription feature flags remain the entitlement ceiling; `AiTenantSettings` stores tenant preferences and self-limits below that ceiling. Internal AI runtime consumes provider credentials, model defaults, cost caps, safety fallback, and brand profile now; public widget enforcement remains a Plan 8f consumer of the 5f widget data model.

**Tech Stack:** .NET 10, EF Core module `AiDbContext`, MediatR CQRS, FluentValidation, ASP.NET Core controllers, Data Protection, Redis cost-cap accountant, xUnit, FluentAssertions, Moq.

**Command Working Directory:** Run Tasks 1-11 commands from `boilerplateBE/`. Run Task 12 commands from the repository root. Paths in file lists are repository-root paths.

---

## File Structure

### Domain and Persistence

- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ProviderCredentialPolicy.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ProviderCredentialStatus.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiAgentClass.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiPublicWidgetStatus.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiWidgetCredentialStatus.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ProviderCredentialSource.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiTenantSettings.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiProviderCredential.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiModelDefault.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPublicWidget.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiWidgetCredential.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiSettingsErrors.cs`
- Create EF configurations under `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiUsageLog.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiUsageLogConfiguration.cs`

### Application Services

- Create `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiSettingsDtos.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiEntitlementResolver.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiTenantSettingsResolver.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiProviderCredentialResolver.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiModelDefaultResolver.cs`
- Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiBrandPromptResolver.cs`
- Create matching implementations under `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Settings/`
- Create AI settings commands/queries under `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/` and `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/`

### Runtime Integration

- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OpenAiProvider.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AnthropicAiProvider.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/AgentRunContext.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/SafetyProfileResolver.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Costs/EffectiveCaps.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Costs/ICostCapAccountant.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Costs/RedisCostCapAccountant.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Costs/CostCapResolver.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/CostCapEnforcingAgentRuntime.cs`

### API, DI, Seeds, Tests

- Create `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiSettingsController.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`
- Modify `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`
- Modify `boilerplateBE/src/modules/Starter.Module.Billing/BillingModule.cs`
- Create tests under `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/`
- Create `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5fAcidTests.cs`

No EF migration files are committed in this boilerplate branch, and no task should run `dotnet ef migrations add` or create `Migrations/` artifacts. Match the existing AI module pattern: update model/configuration/tests and leave migration generation to downstream apps after the rename script creates a concrete application.

---

## Task 1: Domain Enums, Entities, EF Shape

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ProviderCredentialPolicy.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ProviderCredentialStatus.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiAgentClass.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiPublicWidgetStatus.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiWidgetCredentialStatus.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ProviderCredentialSource.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiTenantSettings.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiProviderCredential.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiModelDefault.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPublicWidget.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiWidgetCredential.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiSettingsErrors.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiSettingsDomainTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiTenantSettingsConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiProviderCredentialConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiModelDefaultConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiPublicWidgetConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiWidgetCredentialConfiguration.cs`

- [ ] **Step 1: Write the failing domain tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiSettingsDomainTests.cs`:

```csharp
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiSettingsDomainTests
{
    [Fact]
    public void TenantSettings_CreateDefault_Uses_PlatformOnly_And_Standard()
    {
        var tenantId = Guid.NewGuid();

        var settings = AiTenantSettings.CreateDefault(tenantId);

        settings.TenantId.Should().Be(tenantId);
        settings.RequestedProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.PlatformOnly);
        settings.DefaultSafetyPreset.Should().Be(SafetyPreset.Standard);
        settings.MonthlyCostCapUsd.Should().BeNull();
        settings.PlatformMonthlyCostCapUsd.Should().BeNull();
    }

    [Fact]
    public void TenantSettings_UpdatePolicy_Rejects_Invalid_Public_Rpm()
    {
        var settings = AiTenantSettings.CreateDefault(Guid.NewGuid());

        var act = () => settings.UpdatePublicWidgetDefaults(
            monthlyTokenCap: 1_000,
            dailyTokenCap: 100,
            requestsPerMinute: -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("requestsPerMinute");
    }

    [Fact]
    public void ProviderCredential_Rotate_Revokes_Old_And_Creates_Active_New()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var oldCredential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            encryptedSecret: "cipher-old",
            keyPrefix: "sk-live-old",
            createdBy);

        oldCredential.Revoke();
        var replacement = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            encryptedSecret: "cipher-new",
            keyPrefix: "sk-live-new",
            createdBy);

        oldCredential.Status.Should().Be(ProviderCredentialStatus.Revoked);
        replacement.Status.Should().Be(ProviderCredentialStatus.Active);
        replacement.Provider.Should().Be(AiProviderType.OpenAI);
    }

    [Fact]
    public void PublicWidget_Normalizes_Allowed_Origins()
    {
        var widget = AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: new[] { "https://Example.com/", "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        widget.AllowedOrigins.Should().Equal("https://example.com");
    }

    [Fact]
    public void WidgetCredential_Stores_Hash_Metadata_Only()
    {
        var credential = AiWidgetCredential.Create(
            tenantId: Guid.NewGuid(),
            widgetId: Guid.NewGuid(),
            keyPrefix: "pk_ai_12345678",
            keyHash: "$2a$12$abcdef",
            expiresAt: null,
            createdByUserId: Guid.NewGuid());

        credential.KeyPrefix.Should().Be("pk_ai_12345678");
        credential.KeyHash.Should().Be("$2a$12$abcdef");
        credential.Status.Should().Be(AiWidgetCredentialStatus.Active);
    }
}
```

- [ ] **Step 2: Run the domain tests to verify they fail**

Run from `boilerplateBE`:

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiSettingsDomainTests
```

Expected: FAIL with missing `AiTenantSettings`, `ProviderCredentialPolicy`, `AiProviderCredential`, `AiPublicWidget`, and `AiWidgetCredential`.

- [ ] **Step 3: Add enum files**

Create the enum files with these exact definitions:

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum ProviderCredentialPolicy
{
    PlatformOnly = 0,
    TenantKeysAllowed = 1,
    TenantKeysRequired = 2
}
```

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum ProviderCredentialStatus
{
    Active = 0,
    Revoked = 1
}
```

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AiAgentClass
{
    Chat = 0,
    ToolAgent = 1,
    Insight = 2,
    RagHelper = 3,
    Embedding = 4
}
```

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AiPublicWidgetStatus
{
    Active = 0,
    Paused = 1,
    Archived = 2
}
```

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AiWidgetCredentialStatus
{
    Active = 0,
    Revoked = 1
}
```

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum ProviderCredentialSource
{
    Platform = 0,
    Tenant = 1
}
```

- [ ] **Step 4: Add `AiTenantSettings`**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiTenantSettings.cs`:

```csharp
using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiTenantSettings : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public ProviderCredentialPolicy RequestedProviderCredentialPolicy { get; private set; }
    public SafetyPreset DefaultSafetyPreset { get; private set; }
    public decimal? MonthlyCostCapUsd { get; private set; }
    public decimal? DailyCostCapUsd { get; private set; }
    public decimal? PlatformMonthlyCostCapUsd { get; private set; }
    public decimal? PlatformDailyCostCapUsd { get; private set; }
    public int? RequestsPerMinute { get; private set; }
    public int? PublicMonthlyTokenCap { get; private set; }
    public int? PublicDailyTokenCap { get; private set; }
    public int? PublicRequestsPerMinute { get; private set; }
    public string? AssistantDisplayName { get; private set; }
    public string? Tone { get; private set; }
    public Guid? AvatarFileId { get; private set; }
    public string? BrandInstructions { get; private set; }

    private AiTenantSettings() { }

    private AiTenantSettings(Guid tenantId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        RequestedProviderCredentialPolicy = ProviderCredentialPolicy.PlatformOnly;
        DefaultSafetyPreset = SafetyPreset.Standard;
    }

    public static AiTenantSettings CreateDefault(Guid tenantId) => new(tenantId);

    public void UpdatePolicy(ProviderCredentialPolicy policy, SafetyPreset safetyPreset)
    {
        RequestedProviderCredentialPolicy = policy;
        DefaultSafetyPreset = safetyPreset;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateCostSelfLimits(
        decimal? monthlyCostCapUsd,
        decimal? dailyCostCapUsd,
        decimal? platformMonthlyCostCapUsd,
        decimal? platformDailyCostCapUsd,
        int? requestsPerMinute)
    {
        if (monthlyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyCostCapUsd));
        if (dailyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(dailyCostCapUsd));
        if (platformMonthlyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(platformMonthlyCostCapUsd));
        if (platformDailyCostCapUsd is < 0) throw new ArgumentOutOfRangeException(nameof(platformDailyCostCapUsd));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));

        MonthlyCostCapUsd = monthlyCostCapUsd;
        DailyCostCapUsd = dailyCostCapUsd;
        PlatformMonthlyCostCapUsd = platformMonthlyCostCapUsd;
        PlatformDailyCostCapUsd = platformDailyCostCapUsd;
        RequestsPerMinute = requestsPerMinute;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdatePublicWidgetDefaults(int? monthlyTokenCap, int? dailyTokenCap, int? requestsPerMinute)
    {
        if (monthlyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyTokenCap));
        if (dailyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(dailyTokenCap));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));

        PublicMonthlyTokenCap = monthlyTokenCap;
        PublicDailyTokenCap = dailyTokenCap;
        PublicRequestsPerMinute = requestsPerMinute;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateBrandProfile(
        string? assistantDisplayName,
        string? tone,
        Guid? avatarFileId,
        string? brandInstructions)
    {
        AssistantDisplayName = string.IsNullOrWhiteSpace(assistantDisplayName) ? null : assistantDisplayName.Trim();
        Tone = string.IsNullOrWhiteSpace(tone) ? null : tone.Trim();
        AvatarFileId = avatarFileId == Guid.Empty ? null : avatarFileId;
        BrandInstructions = string.IsNullOrWhiteSpace(brandInstructions) ? null : brandInstructions.Trim();
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 5: Add credential, model default, widget entities**

Create the four entity files. Keep each class focused and match the property names used in the tests:

```csharp
using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiProviderCredential : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public AiProviderType Provider { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string EncryptedSecret { get; private set; } = default!;
    public string KeyPrefix { get; private set; } = default!;
    public ProviderCredentialStatus Status { get; private set; }
    public DateTimeOffset? LastValidatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private AiProviderCredential() { }

    private AiProviderCredential(Guid tenantId, AiProviderType provider, string displayName,
        string encryptedSecret, string keyPrefix, Guid? createdByUserId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        Provider = provider;
        DisplayName = displayName.Trim();
        EncryptedSecret = encryptedSecret;
        KeyPrefix = keyPrefix.Trim();
        Status = ProviderCredentialStatus.Active;
        CreatedByUserId = createdByUserId;
    }

    public static AiProviderCredential Create(Guid tenantId, AiProviderType provider, string displayName,
        string encryptedSecret, string keyPrefix, Guid? createdByUserId) =>
        new(tenantId, provider, displayName, encryptedSecret, keyPrefix, createdByUserId);

    public void Revoke()
    {
        Status = ProviderCredentialStatus.Revoked;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkValidated()
    {
        LastValidatedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

```csharp
using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiModelDefault : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public AiAgentClass AgentClass { get; private set; }
    public AiProviderType Provider { get; private set; }
    public string Model { get; private set; } = default!;
    public int? MaxTokens { get; private set; }
    public double? Temperature { get; private set; }

    private AiModelDefault() { }

    private AiModelDefault(Guid tenantId, AiAgentClass agentClass, AiProviderType provider,
        string model, int? maxTokens, double? temperature) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        AgentClass = agentClass;
        Provider = provider;
        Model = model.Trim();
        MaxTokens = maxTokens;
        Temperature = temperature;
    }

    public static AiModelDefault Create(Guid tenantId, AiAgentClass agentClass, AiProviderType provider,
        string model, int? maxTokens, double? temperature) =>
        new(tenantId, agentClass, provider, model, maxTokens, temperature);

    public void Update(AiProviderType provider, string model, int? maxTokens, double? temperature)
    {
        Provider = provider;
        Model = model.Trim();
        MaxTokens = maxTokens;
        Temperature = temperature;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiPublicWidget : AggregateRoot, ITenantEntity
{
    private List<string> _allowedOrigins = new();

    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public AiPublicWidgetStatus Status { get; private set; }
    public IReadOnlyList<string> AllowedOrigins
    {
        get => _allowedOrigins;
        private set => _allowedOrigins = value?.ToList() ?? new();
    }
    public Guid? DefaultAssistantId { get; private set; }
    public string DefaultPersonaSlug { get; private set; } = AiPersona.AnonymousSlug;
    public int? MonthlyTokenCap { get; private set; }
    public int? DailyTokenCap { get; private set; }
    public int? RequestsPerMinute { get; private set; }
    public string? MetadataJson { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private AiPublicWidget() { }

    private AiPublicWidget(Guid tenantId, string name, IEnumerable<string> allowedOrigins,
        Guid? defaultAssistantId, string defaultPersonaSlug, int? monthlyTokenCap,
        int? dailyTokenCap, int? requestsPerMinute, Guid? createdByUserId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        Name = name.Trim();
        Status = AiPublicWidgetStatus.Active;
        _allowedOrigins = NormalizeOrigins(allowedOrigins);
        DefaultAssistantId = defaultAssistantId;
        DefaultPersonaSlug = string.IsNullOrWhiteSpace(defaultPersonaSlug)
            ? AiPersona.AnonymousSlug
            : defaultPersonaSlug.Trim().ToLowerInvariant();
        MonthlyTokenCap = monthlyTokenCap;
        DailyTokenCap = dailyTokenCap;
        RequestsPerMinute = requestsPerMinute;
        CreatedByUserId = createdByUserId;
    }

    public static AiPublicWidget Create(Guid tenantId, string name, IEnumerable<string> allowedOrigins,
        Guid? defaultAssistantId, string defaultPersonaSlug, int? monthlyTokenCap,
        int? dailyTokenCap, int? requestsPerMinute, Guid? createdByUserId)
    {
        if (monthlyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyTokenCap));
        if (dailyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(dailyTokenCap));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));
        return new AiPublicWidget(tenantId, name, allowedOrigins, defaultAssistantId,
            defaultPersonaSlug, monthlyTokenCap, dailyTokenCap, requestsPerMinute, createdByUserId);
    }

    public void Update(string name, IEnumerable<string> allowedOrigins, Guid? defaultAssistantId,
        string defaultPersonaSlug, int? monthlyTokenCap, int? dailyTokenCap,
        int? requestsPerMinute, string? metadataJson)
    {
        if (monthlyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyTokenCap));
        if (dailyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(dailyTokenCap));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));
        Name = name.Trim();
        _allowedOrigins = NormalizeOrigins(allowedOrigins);
        DefaultAssistantId = defaultAssistantId;
        DefaultPersonaSlug = string.IsNullOrWhiteSpace(defaultPersonaSlug)
            ? AiPersona.AnonymousSlug
            : defaultPersonaSlug.Trim().ToLowerInvariant();
        MonthlyTokenCap = monthlyTokenCap;
        DailyTokenCap = dailyTokenCap;
        RequestsPerMinute = requestsPerMinute;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetStatus(AiPublicWidgetStatus status)
    {
        Status = status;
        ModifiedAt = DateTime.UtcNow;
    }

    private static List<string> NormalizeOrigins(IEnumerable<string> origins) =>
        origins
            .Select(NormalizeOrigin)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(o => o, StringComparer.Ordinal)
            .ToList();

    private static string NormalizeOrigin(string origin)
    {
        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid origin '{origin}'.", nameof(origin));
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            throw new ArgumentException($"Origin '{origin}' must use http or https.", nameof(origin));
        return uri.IsDefaultPort
            ? $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}"
            : $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }
}
```

```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiWidgetCredential : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid WidgetId { get; private set; }
    public string KeyPrefix { get; private set; } = default!;
    public string KeyHash { get; private set; } = default!;
    public AiWidgetCredentialStatus Status { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private AiWidgetCredential() { }

    private AiWidgetCredential(Guid tenantId, Guid widgetId, string keyPrefix,
        string keyHash, DateTimeOffset? expiresAt, Guid? createdByUserId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        WidgetId = widgetId;
        KeyPrefix = keyPrefix.Trim();
        KeyHash = keyHash;
        Status = AiWidgetCredentialStatus.Active;
        ExpiresAt = expiresAt;
        CreatedByUserId = createdByUserId;
    }

    public static AiWidgetCredential Create(Guid tenantId, Guid widgetId, string keyPrefix,
        string keyHash, DateTimeOffset? expiresAt, Guid? createdByUserId) =>
        new(tenantId, widgetId, keyPrefix, keyHash, expiresAt, createdByUserId);

    public void Revoke()
    {
        Status = AiWidgetCredentialStatus.Revoked;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 6: Add settings errors**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiSettingsErrors.cs`:

```csharp
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class AiSettingsErrors
{
    public static Error ByokDisabledByPlan =>
        new("AiSettings.ByokDisabledByPlan", "Tenant-owned AI provider keys are not enabled by this tenant's plan.", ErrorType.Forbidden);
    public static Error ProviderNotAllowed(string provider) =>
        Error.Validation("AiSettings.ProviderNotAllowed", $"AI provider '{provider}' is not allowed by this tenant's plan.");
    public static Error ModelNotAllowed(string model) =>
        Error.Validation("AiSettings.ModelNotAllowed", $"AI model '{model}' is not allowed by this tenant's plan.");
    public static Error TenantKeyRequired(string provider) =>
        Error.Validation("AiSettings.TenantKeyRequired", $"A tenant-owned key is required for provider '{provider}'.");
    public static Error SelfLimitExceedsEntitlement(string field) =>
        Error.Validation("AiSettings.SelfLimitExceedsEntitlement", $"AI self-limit '{field}' exceeds the tenant's plan entitlement.");
    public static Error WidgetDisabledByPlan =>
        new("AiSettings.WidgetDisabledByPlan", "Public AI widgets are not enabled by this tenant's plan.", ErrorType.Forbidden);
    public static Error WidgetQuotaExceedsEntitlement(string field) =>
        Error.Validation("AiSettings.WidgetQuotaExceedsEntitlement", $"Widget quota '{field}' exceeds the tenant's plan entitlement.");
    public static Error WidgetLimitExceeded(int limit) =>
        new("AiSettings.WidgetLimitExceeded", $"This tenant can create at most {limit} public AI widgets.", ErrorType.Forbidden);
    public static Error InvalidOrigin(string origin) =>
        Error.Validation("AiSettings.InvalidOrigin", $"Origin '{origin}' is not a valid http or https origin.");
    public static Error ProviderCredentialNotFound =>
        Error.NotFound("AiSettings.ProviderCredentialNotFound", "AI provider credential not found.");
    public static Error WidgetNotFound =>
        Error.NotFound("AiSettings.WidgetNotFound", "AI public widget not found.");
}
```

- [ ] **Step 7: Register DbSets and tenant filters**

Modify `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs`:

```csharp
public DbSet<AiTenantSettings> AiTenantSettings => Set<AiTenantSettings>();
public DbSet<AiProviderCredential> AiProviderCredentials => Set<AiProviderCredential>();
public DbSet<AiModelDefault> AiModelDefaults => Set<AiModelDefault>();
public DbSet<AiPublicWidget> AiPublicWidgets => Set<AiPublicWidget>();
public DbSet<AiWidgetCredential> AiWidgetCredentials => Set<AiWidgetCredential>();
```

Add query filters in `OnModelCreating`:

```csharp
modelBuilder.Entity<AiTenantSettings>().HasQueryFilter(e =>
    CurrentTenantId == null || e.TenantId == CurrentTenantId);
modelBuilder.Entity<AiProviderCredential>().HasQueryFilter(e =>
    CurrentTenantId == null || e.TenantId == CurrentTenantId);
modelBuilder.Entity<AiModelDefault>().HasQueryFilter(e =>
    CurrentTenantId == null || e.TenantId == CurrentTenantId);
modelBuilder.Entity<AiPublicWidget>().HasQueryFilter(e =>
    CurrentTenantId == null || e.TenantId == CurrentTenantId);
modelBuilder.Entity<AiWidgetCredential>().HasQueryFilter(e =>
    CurrentTenantId == null || e.TenantId == CurrentTenantId);
```

- [ ] **Step 8: Add EF configurations**

Create configuration classes with snake_case table/column names:

- `ai_tenant_settings`, unique index `tenant_id`
- `ai_provider_credentials`, filtered unique active index on `(tenant_id, provider)` where `status = 0`
- `ai_model_defaults`, unique index `(tenant_id, agent_class)`
- `ai_public_widgets`, index `(tenant_id, status)`
- `ai_widget_credentials`, index `(tenant_id, widget_id, status)` and unique index on `key_prefix`

Use JSON conversion for `AiPublicWidget.AllowedOrigins`, following `AiAssistantConfiguration` string-list converter/comparer.

- [ ] **Step 9: Run domain tests to verify they pass**

Run from `boilerplateBE`:

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiSettingsDomainTests
```

Expected: PASS.

- [ ] **Step 10: Commit domain model**

```bash
git add src/modules/Starter.Module.AI/Domain src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs src/modules/Starter.Module.AI/Infrastructure/Configurations tests/Starter.Api.Tests/Ai/Settings/AiSettingsDomainTests.cs
git commit -m "feat(ai): add tenant AI settings domain model"
```

---

## Task 2: Entitlement Resolver and Plan Feature Seeds

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiSettingsDtos.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiEntitlementResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Settings/AiEntitlementResolver.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiEntitlementResolverTests.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Billing/BillingModule.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write failing entitlement resolver tests**

Create `AiEntitlementResolverTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiEntitlementResolverTests
{
    [Fact]
    public async Task ResolveAsync_Reads_All_Ai_Entitlements()
    {
        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", default)).ReturnsAsync(20m);
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", default)).ReturnsAsync(2m);
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.platform_monthly_usd", default)).ReturnsAsync(10m);
        ff.Setup(x => x.GetValueAsync<decimal>("ai.cost.platform_daily_usd", default)).ReturnsAsync(1m);
        ff.Setup(x => x.GetValueAsync<int>("ai.agents.requests_per_minute_default", default)).ReturnsAsync(60);
        ff.Setup(x => x.GetValueAsync<bool>("ai.provider_keys.byok_enabled", default)).ReturnsAsync(true);
        ff.Setup(x => x.GetValueAsync<bool>("ai.widgets.enabled", default)).ReturnsAsync(true);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.max_count", default)).ReturnsAsync(3);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.monthly_tokens", default)).ReturnsAsync(50_000);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.daily_tokens", default)).ReturnsAsync(5_000);
        ff.Setup(x => x.GetValueAsync<int>("ai.widgets.requests_per_minute", default)).ReturnsAsync(30);
        ff.Setup(x => x.GetValueAsync<string[]>("ai.providers.allowed", default)).ReturnsAsync(new[] { "OpenAI", "Anthropic" });
        ff.Setup(x => x.GetValueAsync<string[]>("ai.models.allowed", default)).ReturnsAsync(new[] { "gpt-4o-mini" });

        var sut = new AiEntitlementResolver(ff.Object);
        var entitlements = await sut.ResolveAsync();

        entitlements.TotalMonthlyUsd.Should().Be(20m);
        entitlements.PlatformMonthlyUsd.Should().Be(10m);
        entitlements.ByokEnabled.Should().BeTrue();
        entitlements.WidgetsEnabled.Should().BeTrue();
        entitlements.WidgetMaxCount.Should().Be(3);
        entitlements.AllowedProviders.Should().Equal("OpenAI", "Anthropic");
        entitlements.AllowedModels.Should().Equal("gpt-4o-mini");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiEntitlementResolverTests
```

Expected: FAIL with missing `AiEntitlementResolver`.

- [ ] **Step 3: Add DTO and resolver interface**

In `AiSettingsDtos.cs`, add:

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record AiEntitlementsDto(
    decimal TotalMonthlyUsd,
    decimal TotalDailyUsd,
    decimal PlatformMonthlyUsd,
    decimal PlatformDailyUsd,
    int RequestsPerMinute,
    bool ByokEnabled,
    bool WidgetsEnabled,
    int WidgetMaxCount,
    int WidgetMonthlyTokens,
    int WidgetDailyTokens,
    int WidgetRequestsPerMinute,
    IReadOnlyList<string> AllowedProviders,
    IReadOnlyList<string> AllowedModels);
```

Create `IAiEntitlementResolver.cs`:

```csharp
using Starter.Module.AI.Application.DTOs;

namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiEntitlementResolver
{
    Task<AiEntitlementsDto> ResolveAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement `AiEntitlementResolver`**

Create `AiEntitlementResolver.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiEntitlementResolver(IFeatureFlagService featureFlags) : IAiEntitlementResolver
{
    public async Task<AiEntitlementsDto> ResolveAsync(CancellationToken ct = default)
    {
        var allowedProviders = await featureFlags.GetValueAsync<string[]>("ai.providers.allowed", ct);
        var allowedModels = await featureFlags.GetValueAsync<string[]>("ai.models.allowed", ct);

        return new AiEntitlementsDto(
            TotalMonthlyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", ct),
            TotalDailyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", ct),
            PlatformMonthlyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.platform_monthly_usd", ct),
            PlatformDailyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.platform_daily_usd", ct),
            RequestsPerMinute: await featureFlags.GetValueAsync<int>("ai.agents.requests_per_minute_default", ct),
            ByokEnabled: await featureFlags.GetValueAsync<bool>("ai.provider_keys.byok_enabled", ct),
            WidgetsEnabled: await featureFlags.GetValueAsync<bool>("ai.widgets.enabled", ct),
            WidgetMaxCount: await featureFlags.GetValueAsync<int>("ai.widgets.max_count", ct),
            WidgetMonthlyTokens: await featureFlags.GetValueAsync<int>("ai.widgets.monthly_tokens", ct),
            WidgetDailyTokens: await featureFlags.GetValueAsync<int>("ai.widgets.daily_tokens", ct),
            WidgetRequestsPerMinute: await featureFlags.GetValueAsync<int>("ai.widgets.requests_per_minute", ct),
            AllowedProviders: allowedProviders,
            AllowedModels: allowedModels);
    }
}
```

- [ ] **Step 5: Register the resolver**

In `AIModule.ConfigureServices`, add:

```csharp
services.AddScoped<IAiEntitlementResolver, AiEntitlementResolver>();
```

- [ ] **Step 6: Seed new feature flags**

In `DataSeeder.SeedFeatureFlagsAsync`, add these `FeatureFlag.Create` rows next to existing AI flags:

```csharp
FeatureFlag.Create("ai.cost.platform_monthly_usd", "AI Platform Monthly USD Credit", "Monthly USD ceiling for platform-key AI spend per tenant", "0", FlagValueType.Integer, FlagCategory.Ai, true),
FeatureFlag.Create("ai.cost.platform_daily_usd", "AI Platform Daily USD Credit", "Daily USD ceiling for platform-key AI spend per tenant", "0", FlagValueType.Integer, FlagCategory.Ai, true),
FeatureFlag.Create("ai.provider_keys.byok_enabled", "AI BYOK Enabled", "Allow tenant-owned AI provider keys", "false", FlagValueType.Boolean, FlagCategory.Ai, true),
FeatureFlag.Create("ai.widgets.enabled", "AI Public Widgets Enabled", "Allow public AI widgets", "false", FlagValueType.Boolean, FlagCategory.Ai, true),
FeatureFlag.Create("ai.widgets.max_count", "Max AI Public Widgets", "Maximum public AI widgets per tenant", "0", FlagValueType.Integer, FlagCategory.Ai, true),
FeatureFlag.Create("ai.widgets.monthly_tokens", "AI Widget Monthly Tokens", "Monthly public-widget token ceiling", "0", FlagValueType.Integer, FlagCategory.Ai, true),
FeatureFlag.Create("ai.widgets.daily_tokens", "AI Widget Daily Tokens", "Daily public-widget token ceiling", "0", FlagValueType.Integer, FlagCategory.Ai, true),
FeatureFlag.Create("ai.widgets.requests_per_minute", "AI Widget RPM", "Default public-widget requests-per-minute ceiling", "0", FlagValueType.Integer, FlagCategory.Ai, true),
FeatureFlag.Create("ai.providers.allowed", "Allowed AI Providers", "JSON array of allowed AI providers; empty means all registered providers", "[]", FlagValueType.Json, FlagCategory.Ai, true),
FeatureFlag.Create("ai.models.allowed", "Allowed AI Models", "JSON array of allowed AI models; empty means all configured models", "[]", FlagValueType.Json, FlagCategory.Ai, true),
```

If `FlagValueType.Json` does not exist, add it to `boilerplateBE/src/Starter.Domain/FeatureFlags/Enums/FlagValueType.cs` with the next integer value and add tests where existing enum tests require updates.

- [ ] **Step 7: Seed plan values in Billing plans**

Modify each plan feature JSON in `BillingModule.SeedDataAsync`:

Free:

```csharp
new { key = "ai.cost.platform_monthly_usd", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.cost.platform_daily_usd", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.provider_keys.byok_enabled", value = "false", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.enabled", value = "false", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.max_count", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.monthly_tokens", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.daily_tokens", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.requests_per_minute", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.providers.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.models.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
```

Starter:

```csharp
new { key = "ai.cost.platform_monthly_usd", value = "20", translations = new { en = new { label = "$20/mo platform AI credit" }, ar = new { label = "رصيد ذكاء اصطناعي 20$ شهريًا" } } },
new { key = "ai.cost.platform_daily_usd", value = "2", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.provider_keys.byok_enabled", value = "false", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.enabled", value = "false", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.max_count", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.monthly_tokens", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.daily_tokens", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.requests_per_minute", value = "0", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.providers.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.models.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
```

Pro:

```csharp
new { key = "ai.cost.platform_monthly_usd", value = "200", translations = new { en = new { label = "$200/mo platform AI credit" }, ar = new { label = "رصيد ذكاء اصطناعي 200$ شهريًا" } } },
new { key = "ai.cost.platform_daily_usd", value = "20", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.provider_keys.byok_enabled", value = "true", translations = new { en = new { label = "Tenant provider keys" }, ar = new { label = "مفاتيح مزود خاصة بالمستأجر" } } },
new { key = "ai.widgets.enabled", value = "true", translations = new { en = new { label = "Public AI widgets" }, ar = new { label = "ودجات ذكاء اصطناعي عامة" } } },
new { key = "ai.widgets.max_count", value = "3", translations = new { en = new { label = "Up to 3 AI widgets" }, ar = new { label = "حتى 3 ودجات ذكاء اصطناعي" } } },
new { key = "ai.widgets.monthly_tokens", value = "50000", translations = new { en = new { label = "50k widget tokens/mo" }, ar = new { label = "50 ألف رمز للودجات شهريًا" } } },
new { key = "ai.widgets.daily_tokens", value = "5000", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.requests_per_minute", value = "30", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.providers.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.models.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
```

Enterprise:

```csharp
new { key = "ai.cost.platform_monthly_usd", value = "2000", translations = new { en = new { label = "$2,000/mo platform AI credit" }, ar = new { label = "رصيد ذكاء اصطناعي 2,000$ شهريًا" } } },
new { key = "ai.cost.platform_daily_usd", value = "200", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.provider_keys.byok_enabled", value = "true", translations = new { en = new { label = "Tenant provider keys" }, ar = new { label = "مفاتيح مزود خاصة بالمستأجر" } } },
new { key = "ai.widgets.enabled", value = "true", translations = new { en = new { label = "Public AI widgets" }, ar = new { label = "ودجات ذكاء اصطناعي عامة" } } },
new { key = "ai.widgets.max_count", value = "25", translations = new { en = new { label = "Up to 25 AI widgets" }, ar = new { label = "حتى 25 ودجت ذكاء اصطناعي" } } },
new { key = "ai.widgets.monthly_tokens", value = "1000000", translations = new { en = new { label = "1M widget tokens/mo" }, ar = new { label = "مليون رمز للودجات شهريًا" } } },
new { key = "ai.widgets.daily_tokens", value = "100000", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.widgets.requests_per_minute", value = "120", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.providers.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
new { key = "ai.models.allowed", value = "[]", translations = new { en = new { label = "" }, ar = new { label = "" } } },
```

- [ ] **Step 8: Run entitlement tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiEntitlementResolverTests
```

Expected: PASS.

- [ ] **Step 9: Commit entitlement work**

```bash
git add src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs src/modules/Starter.Module.Billing/BillingModule.cs src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai/Settings
git commit -m "feat(ai): add AI entitlement resolver and plan features"
```

---

## Task 3: Tenant Settings Query and Upsert

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiTenantSettingsResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Settings/AiTenantSettingsResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/GetAiTenantSettings/GetAiTenantSettingsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/GetAiTenantSettings/GetAiTenantSettingsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/UpsertAiTenantSettings/UpsertAiTenantSettingsCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/UpsertAiTenantSettings/UpsertAiTenantSettingsCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/UpsertAiTenantSettings/UpsertAiTenantSettingsCommandHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiTenantSettingsHandlerTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiSettingsDtos.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write failing handler tests**

Create tests that assert:

- missing settings row returns default `PlatformOnly`
- BYOK disabled makes effective policy `PlatformOnly`
- upsert rejects self-limits above entitlements
- upsert stores brand profile and public defaults

Use this test name pattern:

```csharp
[Fact]
public async Task Get_Returns_Default_PlatformOnly_When_Row_Missing()
```

```csharp
[Fact]
public async Task Upsert_Rejects_Total_Monthly_Limit_Above_Entitlement()
```

Expected error for the rejection:

```csharp
result.Error.Code.Should().Be("AiSettings.SelfLimitExceedsEntitlement");
```

- [ ] **Step 2: Run handler tests to verify they fail**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiTenantSettingsHandlerTests
```

Expected: FAIL with missing command/query handlers.

- [ ] **Step 3: Extend DTOs**

Add to `AiSettingsDtos.cs`:

```csharp
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiTenantSettingsDto(
    Guid TenantId,
    ProviderCredentialPolicy RequestedProviderCredentialPolicy,
    ProviderCredentialPolicy EffectiveProviderCredentialPolicy,
    SafetyPreset DefaultSafetyPreset,
    decimal? MonthlyCostCapUsd,
    decimal? DailyCostCapUsd,
    decimal? PlatformMonthlyCostCapUsd,
    decimal? PlatformDailyCostCapUsd,
    int? RequestsPerMinute,
    int? PublicMonthlyTokenCap,
    int? PublicDailyTokenCap,
    int? PublicRequestsPerMinute,
    string? AssistantDisplayName,
    string? Tone,
    Guid? AvatarFileId,
    string? BrandInstructions,
    AiEntitlementsDto Entitlements);
```

- [ ] **Step 4: Implement settings resolver**

Create `IAiTenantSettingsResolver`:

```csharp
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiTenantSettingsResolver
{
    Task<AiTenantSettings> GetOrDefaultAsync(Guid tenantId, CancellationToken ct = default);
    Task<ProviderCredentialPolicy> ResolveEffectivePolicyAsync(Guid tenantId, CancellationToken ct = default);
}
```

Create `AiTenantSettingsResolver`:

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiTenantSettingsResolver(
    AiDbContext db,
    IAiEntitlementResolver entitlements) : IAiTenantSettingsResolver
{
    public async Task<AiTenantSettings> GetOrDefaultAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await db.AiTenantSettings
                   .AsNoTracking()
                   .IgnoreQueryFilters()
                   .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
               ?? AiTenantSettings.CreateDefault(tenantId);
    }

    public async Task<ProviderCredentialPolicy> ResolveEffectivePolicyAsync(Guid tenantId, CancellationToken ct = default)
    {
        var settings = await GetOrDefaultAsync(tenantId, ct);
        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        return resolvedEntitlements.ByokEnabled
            ? settings.RequestedProviderCredentialPolicy
            : ProviderCredentialPolicy.PlatformOnly;
    }
}
```

- [ ] **Step 5: Implement query and upsert handlers**

The query accepts `Guid? TenantId` for platform-admin cross-tenant reads. Tenant users ignore the route/query tenant and use `currentUser.TenantId`.

The command validates each nullable self-limit against `AiEntitlementsDto`. Use the exact field names in `AiSettingsErrors.SelfLimitExceedsEntitlement`.

Validation comparisons:

```csharp
if (request.MonthlyCostCapUsd is { } monthly && monthly > entitlements.TotalMonthlyUsd)
    return Result.Failure<AiTenantSettingsDto>(AiSettingsErrors.SelfLimitExceedsEntitlement(nameof(request.MonthlyCostCapUsd)));
if (request.PlatformMonthlyCostCapUsd is { } platformMonthly && platformMonthly > entitlements.PlatformMonthlyUsd)
    return Result.Failure<AiTenantSettingsDto>(AiSettingsErrors.SelfLimitExceedsEntitlement(nameof(request.PlatformMonthlyCostCapUsd)));
if (request.PublicRequestsPerMinute is { } publicRpm && publicRpm > entitlements.WidgetRequestsPerMinute)
    return Result.Failure<AiTenantSettingsDto>(AiSettingsErrors.WidgetQuotaExceedsEntitlement(nameof(request.PublicRequestsPerMinute)));
```

**Cache invalidation after persist.** `CostCapResolver` caches `EffectiveCaps` for 60 seconds (`CostCapResolver.cs:13`). When tenant self-limits change, the runtime must see the new ceiling on the next request, not 60 s later. The handler must call `ICostCapResolver.InvalidateTenantAsync(tenantId, ct)` after `SaveChangesAsync`. Inject the resolver and add this at the end of the success path:

```csharp
await context.SaveChangesAsync(ct);
await costCaps.InvalidateTenantAsync(tenantId, ct);   // ← new line
return Result.Success(dto);
```

Add a handler test: `Upsert_Invalidates_CostCap_Cache_For_Tenant()` — assert the cache key for `(tenantId, *)` is removed (or use a Mock<ICostCapResolver> to verify the call).

- [ ] **Step 6: Register resolver**

In `AIModule.ConfigureServices`, add:

```csharp
services.AddScoped<IAiTenantSettingsResolver, AiTenantSettingsResolver>();
```

- [ ] **Step 7: Run tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiTenantSettingsHandlerTests
```

Expected: PASS.

- [ ] **Step 8: Commit tenant settings handlers**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai/Settings
git commit -m "feat(ai): add tenant AI settings handlers"
```

---

## Task 4: Provider Credential Management

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiSecretProtector.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Settings/AiSecretProtector.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiSettingsDtos.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/ProviderCredentials/GetProviderCredentials/GetProviderCredentialsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/ProviderCredentials/GetProviderCredentials/GetProviderCredentialsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/CreateProviderCredential/CreateProviderCredentialCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/CreateProviderCredential/CreateProviderCredentialCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/CreateProviderCredential/CreateProviderCredentialCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/RotateProviderCredential/RotateProviderCredentialCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/RotateProviderCredential/RotateProviderCredentialCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/RotateProviderCredential/RotateProviderCredentialCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/RevokeProviderCredential/RevokeProviderCredentialCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/RevokeProviderCredential/RevokeProviderCredentialCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/TestProviderCredential/TestProviderCredentialCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ProviderCredentials/TestProviderCredential/TestProviderCredentialCommandHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiProviderCredentialHandlerTests.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiSecretProtectorTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write failing encryption and handler tests**

Test names:

```csharp
ProtectUnprotect_RoundTrip_Preserves_Secret()
Unprotect_Reads_Legacy_Unprefixed_Secret()        // forward-compat with key rotation
CreateCredential_Fails_When_Byok_Disabled()
CreateCredential_Revokes_Existing_Active_For_Same_Provider()
ListCredentials_Returns_Masked_Metadata()
RotateCredential_Replaces_Secret_And_Keeps_Metadata_Masked()
RevokeCredential_Marks_Credential_Revoked()
```

Expected failure: missing AI secret protector and provider credential handlers.

- [ ] **Step 2: Run tests to verify failure**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiProviderCredentialHandlerTests
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiSecretProtectorTests
```

Expected: FAIL.

- [ ] **Step 3: Add AI secret protector**

Mirrors the existing `WebhookSecretProtector` pattern (purpose-scoped Data Protection + version prefix for forward-compat key rotation). Different purpose string from Webhooks/Communication keeps cryptographic isolation per use-case while keeping API surface and naming consistent across modules.

Interface:

```csharp
namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiSecretProtector
{
    string Protect(string plaintextSecret);
    string Unprotect(string storedSecret);
    string Prefix(string secret);                  // safe display prefix (first 12 chars)
    string Mask(string keyPrefix);                 // masked render for list APIs
}
```

Implementation:

```csharp
using Microsoft.AspNetCore.DataProtection;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiSecretProtector : IAiSecretProtector
{
    private const string Purpose = "Starter.Module.AI.ProviderCredentials.v1";
    private const string VersionPrefix = "ai1:";

    private readonly IDataProtector _protector;

    public AiSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintextSecret) =>
        VersionPrefix + _protector.Protect(plaintextSecret);

    public string Unprotect(string storedSecret)
    {
        // Forward-compat: any future v2 prefix can be branched here.
        // Legacy / unprefixed secrets remain readable until next rotate.
        if (!storedSecret.StartsWith(VersionPrefix, StringComparison.Ordinal))
            return _protector.Unprotect(storedSecret);

        return _protector.Unprotect(storedSecret[VersionPrefix.Length..]);
    }

    public string Prefix(string secret)
    {
        var trimmed = secret.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12];
    }

    public string Mask(string keyPrefix) =>
        string.IsNullOrWhiteSpace(keyPrefix) ? "****" : $"{keyPrefix[..Math.Min(4, keyPrefix.Length)]}****";
}
```

Naming rationale: matches `IWebhookSecretProtector` so future readers find one consistent pattern across modules. Don't use `Encrypt/Decrypt` — that name is taken by `Communication.ICredentialEncryptionService` for `Dictionary<string,string>` payloads, which is a different shape.

- [ ] **Step 4: Implement provider credential DTOs**

Add:

```csharp
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiProviderCredentialDto(
    Guid Id,
    AiProviderType Provider,
    string DisplayName,
    string MaskedKey,
    ProviderCredentialStatus Status,
    DateTimeOffset? LastValidatedAt,
    DateTimeOffset? LastUsedAt,
    DateTime CreatedAt);
```

- [ ] **Step 5: Implement commands and queries**

Handlers:

- `GetProviderCredentialsQuery(Guid? TenantId)`
- `CreateProviderCredentialCommand(Guid? TenantId, AiProviderType Provider, string DisplayName, string Secret)`
- `RotateProviderCredentialCommand(Guid Id, string Secret)`
- `RevokeProviderCredentialCommand(Guid Id)`
- `TestProviderCredentialCommand(Guid Id)`

Create/rotate requirements:

- resolve entitlements
- if `ByokEnabled == false`, return `AiSettingsErrors.ByokDisabledByPlan`
- revoke existing active row for same tenant/provider before adding replacement
- protect the submitted secret via `IAiSecretProtector.Protect`
- store prefix only (`IAiSecretProtector.Prefix`)
- return DTO with masked key (`IAiSecretProtector.Mask`)
- **Audit:** every Create/Rotate/Revoke/Test handler must emit an `AuditLog` entry through the existing `IAuditLogService` (mirrors `RegenerateApiKeyCommandHandler`). Action codes: `AiProviderCredential.Created`, `AiProviderCredential.Rotated`, `AiProviderCredential.Revoked`, `AiProviderCredential.Tested`. Entity name: `AiProviderCredential`. Include the credential `Id`, `Provider`, and (where relevant) `KeyPrefix` in the audit detail JSON. **Never** include the plaintext secret. Add a test per command asserting one audit row is written with the right action code.

Test endpoint can mark validated after decrypting successfully. Do not call a paid provider operation in unit tests; provider SDK smoke can be covered by a manual integration check later.

- [ ] **Step 6: Register secret protector**

In `AIModule.ConfigureServices`, add:

```csharp
services.AddSingleton<IAiSecretProtector, AiSecretProtector>();   // matches IWebhookSecretProtector lifetime
```

- [ ] **Step 7: Run tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiProviderCredentialHandlerTests
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiSecretProtectorTests
```

Expected: PASS.

- [ ] **Step 8: Commit provider credential management**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai/Settings
git commit -m "feat(ai): add tenant provider credential management"
```

---

## Task 5: Provider Credential Resolver and Provider Runtime Integration

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/ResolvedProviderCredential.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiProviderCredentialResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Settings/AiProviderCredentialResolver.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiProviderCredentialResolverTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OpenAiProvider.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AnthropicAiProvider.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/AgentRunContext.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/AgentRuntimeBase.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiUsageLog.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiUsageLogConfiguration.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write failing resolver tests**

Test names:

```csharp
Resolve_PlatformOnly_Uses_Platform_Secret()
Resolve_TenantKeysAllowed_Uses_Tenant_Secret_When_Active()
Resolve_TenantKeysAllowed_Falls_Back_To_Platform_Secret_When_Missing()
Resolve_TenantKeysRequired_Fails_When_Tenant_Secret_Missing()
Resolve_Byok_Disabled_Forces_PlatformOnly()
Resolve_Disallowed_Provider_Fails()
```

Expected failure: missing resolver.

- [ ] **Step 2: Add resolved credential contracts**

Create:

```csharp
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Settings;

internal sealed record ResolvedProviderCredential(
    AiProviderType Provider,
    string Secret,
    ProviderCredentialSource Source,
    Guid? ProviderCredentialId);

internal interface IAiProviderCredentialResolver
{
    Task<Result<ResolvedProviderCredential>> ResolveAsync(
        Guid? tenantId,
        AiProviderType provider,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: Implement resolver**

Resolution order:

1. no tenant id means platform secret
2. `ByokEnabled == false` means platform secret
3. validate provider allowlist; empty allowlist permits all
4. `PlatformOnly` means platform secret
5. `TenantKeysAllowed` means active tenant credential else platform secret
6. `TenantKeysRequired` means active tenant credential else `TenantKeyRequired`

Platform secret keys:

- `AI:Providers:OpenAI:ApiKey`
- `AI:Providers:Anthropic:ApiKey`
- no required key for Ollama

- [ ] **Step 4: Thread resolved credentials into provider options**

Update `AiChatOptions` in `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs`:

```csharp
using Starter.Module.AI.Domain.Enums;

internal sealed record AiChatOptions(
    string Model,
    double Temperature = 0.7,
    int MaxTokens = 4096,
    string? SystemPrompt = null,
    IReadOnlyList<AiToolDefinitionDto>? Tools = null,
    string? ApiKey = null,
    ProviderCredentialSource ProviderCredentialSource = ProviderCredentialSource.Platform,
    Guid? ProviderCredentialId = null);
```

Update both `AiChatOptions` construction sites in `AgentRuntimeBase`:

```csharp
var chatOptions = new AiChatOptions(
    Model: ctx.ModelConfig.Model,
    Temperature: ctx.ModelConfig.Temperature,
    MaxTokens: ctx.ModelConfig.MaxTokens,
    SystemPrompt: ctx.SystemPrompt,
    Tools: ctx.Tools.ProviderTools.Count == 0 ? null : ctx.Tools.ProviderTools,
    ApiKey: ctx.ProviderApiKey,
    ProviderCredentialSource: ctx.ProviderCredentialSource,
    ProviderCredentialId: ctx.ProviderCredentialId);
```

Update `OpenAiProvider`:

```csharp
private string GetApiKey(string? resolvedApiKey = null)
    => !string.IsNullOrWhiteSpace(resolvedApiKey)
        ? resolvedApiKey
        : configuration["AI:Providers:OpenAI:ApiKey"]
          ?? throw new InvalidOperationException("OpenAI API key is not configured (AI:Providers:OpenAI:ApiKey).");
```

Then call `GetApiKey(options.ApiKey)` in `ChatAsync` and `StreamChatAsync`.

Update `AnthropicAiProvider`:

```csharp
private AnthropicClient CreateClient(string? resolvedApiKey = null)
{
    var apiKey = !string.IsNullOrWhiteSpace(resolvedApiKey)
        ? resolvedApiKey
        : configuration["AI:Providers:Anthropic:ApiKey"]
          ?? throw new InvalidOperationException("Anthropic API key is not configured (AI:Providers:Anthropic:ApiKey).");

    var httpClient = httpClientFactory.CreateClient(nameof(AnthropicAiProvider));
    return new AnthropicClient(new APIAuthentication(apiKey), httpClient);
}
```

Then call `CreateClient(options.ApiKey)` in `ChatAsync` and `StreamChatAsync`.

- [ ] **Step 5: Thread credential source into usage logs**

Add nullable/default properties:

```csharp
public ProviderCredentialSource ProviderCredentialSource { get; private set; }
public Guid? ProviderCredentialId { get; private set; }
```

Default constructor path should treat old rows as `ProviderCredentialSource.Platform`.

Update `AiUsageLog.Create` to accept `ProviderCredentialSource providerCredentialSource = ProviderCredentialSource.Platform` and `Guid? providerCredentialId = null`.

Add EF columns:

```csharp
builder.Property(e => e.ProviderCredentialSource)
    .HasColumnName("provider_credential_source")
    .HasConversion<int>()
    .HasDefaultValue(ProviderCredentialSource.Platform)
    .IsRequired();

builder.Property(e => e.ProviderCredentialId)
    .HasColumnName("provider_credential_id");
```

- [ ] **Step 6: Store credential resolution on run context**

Add to `AgentRunContext`:

```csharp
string? ProviderApiKey = null,
ProviderCredentialSource ProviderCredentialSource = ProviderCredentialSource.Platform,
Guid? ProviderCredentialId = null
```

Update `ChatExecutionService` to resolve provider credentials before constructing `AgentRunContext`. If resolution fails, return `Result.Failure<AiChatReplyDto>(error)` before provider calls.

```csharp
var credentialResult = await providerCredentialResolver.ResolveAsync(
    state.Assistant.TenantId,
    modelConfig.Provider,
    ct);

if (credentialResult.IsFailure)
    return Result.Failure<AiChatReplyDto>(credentialResult.Error);

var credential = credentialResult.Value;

var ctx = new AgentRunContext(
    Messages: messages,
    SystemPrompt: effectiveSystemPrompt,
    ModelConfig: modelConfig,
    Tools: tools,
    MaxSteps: maxSteps,
    LoopBreak: loopBreak,
    Streaming: streaming,
    Persona: persona,
    AssistantId: state.Assistant.Id,
    TenantId: state.Assistant.TenantId,
    CallerUserId: callerUserId,
    CallerHasPermission: callerHasPermission,
    AssistantName: state.Assistant.Name,
    ConversationId: conversationId,
    AgentTaskId: agentTaskId,
    ProviderApiKey: credential.Secret,
    ProviderCredentialSource: credential.Source,
    ProviderCredentialId: credential.ProviderCredentialId);
```

When usage logs are created from the run result, pass `ctx.ProviderCredentialSource` and `ctx.ProviderCredentialId` into `AiUsageLog.Create`.

- [ ] **Step 7: Register resolver and run tests**

```csharp
services.AddScoped<IAiProviderCredentialResolver, AiProviderCredentialResolver>();
```

Run:

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiProviderCredentialResolverTests
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiUsageLogShapeTests
```

Expected: PASS.

- [ ] **Step 8: Commit provider runtime integration**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai
git commit -m "feat(ai): resolve tenant provider credentials at runtime"
```

---

## Task 6: Model Defaults Resolver and Runtime Hook

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/ResolvedModelDefault.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiModelDefaultResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Settings/AiModelDefaultResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/ModelDefaults/GetModelDefaults/GetModelDefaultsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/ModelDefaults/GetModelDefaults/GetModelDefaultsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ModelDefaults/UpsertModelDefault/UpsertModelDefaultCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ModelDefaults/UpsertModelDefault/UpsertModelDefaultCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/ModelDefaults/UpsertModelDefault/UpsertModelDefaultCommandHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiModelDefaultResolverTests.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiModelDefaultHandlerTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/IAiProvider.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OpenAiProvider.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OllamaAiProvider.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AnthropicAiProvider.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/QuestionClassifier.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/QueryRewriter.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/EmbeddingService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/CachingEmbeddingService.cs`

- [ ] **Step 1: Write failing model default tests**

Test names:

```csharp
Resolve_Assistant_Explicit_Model_Wins()
Resolve_Tenant_Class_Default_Wins_When_Assistant_Model_Missing()
Resolve_Platform_Default_Wins_When_No_Tenant_Default()
Resolve_Disallowed_Model_Fails()
UpsertModelDefault_Rejects_Disallowed_Provider()
```

- [ ] **Step 2: Add model default contracts**

```csharp
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Settings;

internal sealed record ResolvedModelDefault(
    AiProviderType Provider,
    string Model,
    double Temperature,
    int MaxTokens);

internal interface IAiModelDefaultResolver
{
    // Temperature/MaxTokens are nullable because Embedding and RagHelper classes
    // have no "assistant" — call sites in EmbeddingService / QueryRewriter / Reranker
    // pass null and rely on the tenant default or platform fallback.
    Task<Result<ResolvedModelDefault>> ResolveAsync(
        Guid? tenantId,
        AiAgentClass agentClass,
        AiProviderType? assistantProvider,
        string? assistantModel,
        double? assistantTemperature,
        int? assistantMaxTokens,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: Implement resolver**

Rules:

- provider allowlist and model allowlist are case-insensitive
- empty provider/model allowlist means no restriction
- assistant provider/model pair wins
- tenant `AiModelDefault` wins next
- platform default uses existing `IAiProviderFactory.GetDefaultProviderType()` and `GetDefaultChatModelId()` for text classes
- embedding class uses `GetEmbeddingProviderType()` and `GetEmbeddingModelId()`

- [ ] **Step 4: Implement model default handlers**

Add:

- `GetModelDefaultsQuery(Guid? TenantId)`
- `UpsertModelDefaultCommand(Guid? TenantId, AiAgentClass AgentClass, AiProviderType Provider, string Model, int? MaxTokens, double? Temperature)`

Validation:

- `Model` required, max 100
- `MaxTokens` between 1 and 128000 when set
- `Temperature` between 0 and 2 when set
- provider/model allowlists enforced in handler so feature flags are current

- [ ] **Step 5: Update `ChatExecutionService`**

Replace direct assistant provider/model fallback with:

```csharp
var modelResult = await modelDefaults.ResolveAsync(
    state.Assistant.TenantId,
    state.Assistant.ExecutionMode == AssistantExecutionMode.Agent
        ? AiAgentClass.ToolAgent
        : AiAgentClass.Chat,
    state.Assistant.Provider,
    state.Assistant.Model,
    state.Assistant.Temperature,
    state.Assistant.MaxTokens,
    ct);
if (modelResult.IsFailure)
    return Result.Failure<AiChatReplyDto>(modelResult.Error);
var modelConfig = modelResult.Value;
```

Then use `modelConfig.Provider`, `modelConfig.Model`, `modelConfig.Temperature`, `modelConfig.MaxTokens`.

- [ ] **Step 6: Update RAG helper and embedding call sites**

Add embedding options to `AiProviderTypes.cs`:

```csharp
internal sealed record AiEmbeddingOptions(
    string? Model = null,
    string? ApiKey = null,
    ProviderCredentialSource ProviderCredentialSource = ProviderCredentialSource.Platform,
    Guid? ProviderCredentialId = null);
```

Update `IAiProvider` so existing call sites keep compiling and new embedding callers can pass resolved model/key state:

```csharp
Task<float[]> EmbedAsync(
    string text,
    CancellationToken ct = default,
    AiEmbeddingOptions? options = null);

Task<float[][]> EmbedBatchAsync(
    IReadOnlyList<string> texts,
    CancellationToken ct = default,
    AiEmbeddingOptions? options = null);
```

Update OpenAI embedding methods:

```csharp
public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default, AiEmbeddingOptions? options = null)
{
    var apiKey = GetApiKey(options?.ApiKey);
    var embeddingModel = !string.IsNullOrWhiteSpace(options?.Model) ? options.Model : ResolveEmbeddingModel();
    var client = new EmbeddingClient(embeddingModel, new ApiKeyCredential(apiKey), BuildClientOptions());

    var result = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
    return result.Value.ToFloats().ToArray();
}

public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default, AiEmbeddingOptions? options = null)
{
    var apiKey = GetApiKey(options?.ApiKey);
    var embeddingModel = !string.IsNullOrWhiteSpace(options?.Model) ? options.Model : ResolveEmbeddingModel();
    var client = new EmbeddingClient(embeddingModel, new ApiKeyCredential(apiKey), BuildClientOptions());

    var result = await client.GenerateEmbeddingsAsync(texts, cancellationToken: ct);
    return result.Value.Select(e => e.ToFloats().ToArray()).ToArray();
}
```

Update Ollama embedding methods to use `options?.Model ?? ResolveEmbeddingModel()`. Keep Anthropic throwing `NotSupportedException` with the new method signatures.

In `EmbeddingService`, inject `IAiModelDefaultResolver` and `IAiProviderCredentialResolver`. Resolve model and credential once per `EmbedAsync` call:

```csharp
var tenantId = attribution?.TenantId ?? currentUser.TenantId;
var modelResult = await modelDefaults.ResolveAsync(
    tenantId,
    AiAgentClass.Embedding,
    explicitProvider: null,
    explicitModel: null,
    explicitTemperature: null,
    explicitMaxTokens: null,
    ct);
if (modelResult.IsFailure)
    throw new InvalidOperationException(modelResult.Error.Message);

var modelConfig = modelResult.Value;
var credentialResult = await providerCredentials.ResolveAsync(tenantId, modelConfig.Provider, ct);
if (credentialResult.IsFailure)
    throw new InvalidOperationException(credentialResult.Error.Message);

var credential = credentialResult.Value;
var provider = providerFactory.Create(modelConfig.Provider);
var embeddingOptions = new AiEmbeddingOptions(
    Model: modelConfig.Model,
    ApiKey: credential.Secret,
    ProviderCredentialSource: credential.Source,
    ProviderCredentialId: credential.ProviderCredentialId);
```

Call `EmbedBatchWithRetryAsync(provider, batch, embeddingOptions, ct)`, update that helper signature to accept `AiEmbeddingOptions`, and call `provider.EmbedBatchAsync(batch, ct, embeddingOptions)`. In `AiUsageLog.Create`, log `provider: modelConfig.Provider`, `model: modelConfig.Model`, `providerCredentialSource: credential.Source`, and `providerCredentialId: credential.ProviderCredentialId`.

In `CachingEmbeddingService`, inject `IAiModelDefaultResolver` and `ICurrentUserService`. Replace the cache-key model id with:

```csharp
var tenantId = attribution?.TenantId ?? _currentUser.TenantId;
var modelResult = await _modelDefaults.ResolveAsync(
    tenantId,
    AiAgentClass.Embedding,
    explicitProvider: null,
    explicitModel: null,
    explicitTemperature: null,
    explicitMaxTokens: null,
    ct);
var modelId = modelResult.IsSuccess
    ? $"{modelResult.Value.Provider}:{modelResult.Value.Model}"
    : _providerFactory.GetEmbeddingModelId();
var key = BuildKey(modelId, texts[0]);
```

In `QueryRewriter`, `ContextualQueryResolver`, `QuestionClassifier`, `PointwiseReranker`, and `ListwiseReranker`, inject `IAiModelDefaultResolver` and `IAiProviderCredentialResolver`. Add this helper to each class, using that class's existing logger field:

```csharp
private async Task<(IAiProvider Provider, AiChatOptions Options)?> ResolveRagHelperProviderAsync(
    Guid tenantId,
    string? overrideModel,
    double temperature,
    int maxTokens,
    string? systemPrompt,
    CancellationToken ct)
{
    var modelResult = await _modelDefaults.ResolveAsync(
        tenantId,
        AiAgentClass.RagHelper,
        explicitProvider: null,
        explicitModel: overrideModel,
        explicitTemperature: temperature,
        explicitMaxTokens: maxTokens,
        ct);
    if (modelResult.IsFailure)
    {
        _logger.LogWarning("RAG helper model resolution failed: {Error}", modelResult.Error.Message);
        return null;
    }

    var credentialResult = await _providerCredentials.ResolveAsync(tenantId, modelResult.Value.Provider, ct);
    if (credentialResult.IsFailure)
    {
        _logger.LogWarning("RAG helper provider credential resolution failed: {Error}", credentialResult.Error.Message);
        return null;
    }

    var credential = credentialResult.Value;
    var provider = _factory.Create(modelResult.Value.Provider);
    var options = new AiChatOptions(
        Model: modelResult.Value.Model,
        Temperature: modelResult.Value.Temperature,
        MaxTokens: modelResult.Value.MaxTokens,
        SystemPrompt: systemPrompt,
        ApiKey: credential.Secret,
        ProviderCredentialSource: credential.Source,
        ProviderCredentialId: credential.ProviderCredentialId);

    return (provider, options);
}
```

Apply the helper at the current LLM call sites:

- `QueryRewriter`: pass `tenantId` into `TryCallLlmAsync`, call `ResolveRagHelperProviderAsync(tenantId, _settings.RewriterModel, 0.2, 256, systemPrompt, ct)`, and return `Array.Empty<string>()` when it returns null so rule-based variants still work.
- `ContextualQueryResolver`: pass the existing `tenantId`, call `ResolveRagHelperProviderAsync(tenantId, _settings.ContextualRewriterModel, 0.2, 200, systemPrompt, ct)`, and return null when it returns null so the raw message fallback remains active.
- `QuestionClassifier`: call `ResolveRagHelperProviderAsync(tenantId, _settings.ClassifierModel, 0.0, 8, system, ct)`, and return null when it returns null so the existing `QuestionType.Other` path remains active.
- `PointwiseReranker`: change `BuildPrompt` to accept `AiChatOptions options`, build messages only, and call the helper with `_settings.RerankerModel`, `0.0`, `64`, and the existing system prompt.
- `ListwiseReranker`: change `BuildPrompt` to accept `AiChatOptions options`, build messages only, and call the helper with `_settings.RerankerModel`, `0.0`, `256`, and the existing system prompt.

Keep cache keys provider/model-aware after the change.

- [ ] **Step 7: Run model default tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiModelDefault
```

Expected: PASS.

- [ ] **Step 8: Commit model defaults**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai/Settings
git commit -m "feat(ai): add tenant model defaults"
```

---

## Task 7: Total and Platform-Credit Cost Enforcement

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Costs/EffectiveCaps.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Costs/ICostCapAccountant.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Costs/RedisCostCapAccountant.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Costs/CostCapResolver.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/CostCapEnforcingAgentRuntime.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTenantUsage/GetTenantUsageQuery.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTenantUsage/GetTenantUsageQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAgentBudget/GetAgentBudgetQuery.cs` (consumes `EffectiveCaps` shape — must compile after the record extends)
- Create/modify: tests under `boilerplateBE/tests/Starter.Api.Tests/Ai/Costs/`

> **Sweep before extending the record:** run `rg -n "new EffectiveCaps\\(" boilerplateBE` and update **every** call site listed. Defaults below keep the build green for unrelated test fixtures, but production query handlers must populate platform values explicitly so the UI surfaces the real ceiling.

- [ ] **Step 1: Write failing cost tests**

Add tests:

```csharp
Resolve_Includes_Tenant_Self_Limits_Below_Plan()
Resolve_Platform_Credit_Caps_Are_Separate_From_Total_Caps()
Runtime_Platform_Source_Claims_Total_And_Platform_Credit()
Runtime_Tenant_Source_Claims_Total_Only()
```

- [ ] **Step 2: Update `EffectiveCaps`**

Change to (defaults of `0m` keep test fixtures and unrelated callers compiling — `0` means "blocked" per the existing class doc-comment, which is the safe default if a caller forgets to populate platform values):

```csharp
namespace Starter.Module.AI.Application.Services.Costs;

public sealed record EffectiveCaps(
    decimal MonthlyUsd,
    decimal DailyUsd,
    int Rpm,
    decimal PlatformMonthlyUsd = 0m,
    decimal PlatformDailyUsd = 0m);
```

After this change, **every** real call site (resolver + display query handlers) must pass the platform values explicitly — `0m` defaults are a build-safety net only, not a substitute for resolver wiring. The sweep above lists them; verify each has been updated before committing.

- [ ] **Step 3: Extend accountant with bucket parameter**

Add enum:

```csharp
namespace Starter.Module.AI.Application.Services.Costs;

public enum CostCapBucket
{
    Total = 0,
    PlatformCredit = 1
}
```

Update methods to accept `CostCapBucket bucket`, defaulting to `Total` only at call sites where old behavior is intentionally retained.

Redis key format — `bucketName` already encodes the bucket, do **not** append the enum a second time. The shape mirrors `CostCapResolver`'s existing cache key (`ai:cap:{tenantId}:{assistantId}`) so ops grep stays predictable:

```csharp
var bucketName = bucket == CostCapBucket.PlatformCredit ? "platform-cost" : "cost";
return $"ai:{bucketName}:{tenantId}:{assistantId}:{window.ToString().ToLowerInvariant()}";
```

- [ ] **Step 4: Update cap resolver**

Read:

- existing total plan caps
- new platform-credit plan caps
- tenant self-limits from `AiTenantSettings`
- assistant caps from `AiAssistant`

Resolution:

```csharp
var monthly = MinNonNull(planMonthly, tenant?.MonthlyCostCapUsd, assistant?.MonthlyCostCapUsd);
var daily = MinNonNull(planDaily, tenant?.DailyCostCapUsd, assistant?.DailyCostCapUsd);
var rpm = MinNonNull(planRpm, tenant?.RequestsPerMinute, assistant?.RequestsPerMinute);
var platformMonthly = MinNonNull(planPlatformMonthly, tenant?.PlatformMonthlyCostCapUsd, assistant?.MonthlyCostCapUsd);
var platformDaily = MinNonNull(planPlatformDaily, tenant?.PlatformDailyCostCapUsd, assistant?.DailyCostCapUsd);
```

Use a variadic helper:

```csharp
private static decimal MinNonNull(decimal requiredPlan, params decimal?[] values)
{
    var result = requiredPlan;
    foreach (var value in values)
    {
        if (value.HasValue)
            result = Math.Min(result, value.Value);
    }
    return result;
}
```

- [ ] **Step 5: Update runtime enforcement**

For every run:

- claim total monthly and total daily
- if `ctx.ProviderCredentialSource == ProviderCredentialSource.Platform`, also claim platform monthly and daily
- rollback every successful claim when later claim or rate-limit fails
- reconcile every bucket that was claimed

- [ ] **Step 6: Update usage query DTO**

Add platform-credit rollup fields:

```csharp
decimal TotalPlatformEstimatedCostUsdMonthly,
decimal TotalPlatformEstimatedCostUsdDaily
```

Filter by `ProviderCredentialSource == ProviderCredentialSource.Platform`.

- [ ] **Step 7: Run cost tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Ai.Costs
```

Expected: PASS.

- [ ] **Step 8: Commit cost enforcement**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai/Costs
git commit -m "feat(ai): enforce tenant total and platform AI credit caps"
```

---

## Task 8: Tenant Default Safety and Brand Prompt Injection

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Settings/IAiBrandPromptResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Settings/AiBrandPromptResolver.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/SafetyProfileResolver.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiBrandPromptResolverTests.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/SafetyProfileResolverTests.cs`

- [ ] **Step 1: Write failing tests**

Tests:

```csharp
SafetyResolver_Uses_Tenant_Default_When_Assistant_And_Persona_Missing()
SafetyResolver_Assistant_Override_Wins_Over_Tenant_Default()
BrandPromptResolver_Returns_Empty_When_No_Brand_Profile()
BrandPromptResolver_Includes_Name_Tone_And_Instructions()
ChatExecution_Appends_Brand_Profile_To_System_Prompt()
```

- [ ] **Step 2: Implement brand prompt resolver**

Interface:

```csharp
namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiBrandPromptResolver
{
    Task<string?> ResolveClauseAsync(Guid? tenantId, CancellationToken ct = default);
}
```

Implementation — consume `IAiTenantSettingsResolver` (introduced in Task 3) instead of querying `AiDbContext` directly. One reader, one path; SafetyProfileResolver does the same in Step 3 below. This keeps SRP, enables future caching of tenant settings in one place, and means a missing-row default still resolves to `CreateDefault(tenantId)` (which has null brand fields → no clause):

```csharp
using Starter.Module.AI.Application.Services.Settings;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiBrandPromptResolver(IAiTenantSettingsResolver tenantSettings) : IAiBrandPromptResolver
{
    public async Task<string?> ResolveClauseAsync(Guid? tenantId, CancellationToken ct = default)
    {
        if (tenantId is not { } tid) return null;
        var settings = await tenantSettings.GetOrDefaultAsync(tid, ct);

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.AssistantDisplayName))
            lines.Add($"- Name: {settings.AssistantDisplayName}");
        if (!string.IsNullOrWhiteSpace(settings.Tone))
            lines.Add($"- Tone: {settings.Tone}");
        if (!string.IsNullOrWhiteSpace(settings.BrandInstructions))
            lines.Add($"- Brand guidance: {settings.BrandInstructions}");
        return lines.Count == 0 ? null : "Tenant AI brand profile:\n" + string.Join('\n', lines);
    }
}
```

- [ ] **Step 3: Update safety fallback**

Inject `IAiTenantSettingsResolver` into `SafetyProfileResolver`. Change preset selection:

```csharp
var tenantDefault = tenantId is { } tid
    ? (await tenantSettings.GetOrDefaultAsync(tid, ct)).DefaultSafetyPreset
    : SafetyPreset.Standard;
var preset = assistant.SafetyPresetOverride ?? personaPreset ?? tenantDefault;
```

- [ ] **Step 4: Update chat prompt composition**

Inject `IAiBrandPromptResolver` into `ChatExecutionService`.

After the existing `ResolveSystemPrompt` call, **prepend** the brand clause when present so the assistant prompt is read last and remains authoritative (matches the safety-clause convention at `ResolveSystemPrompt` line 1109: `clause + "\n\n" + basePrompt`). Per spec §6.5: "Do not override explicit assistant specialization." Appending brand AFTER the assistant prompt would let brand instructions override assistant guidance — that is the bug we are avoiding.

```csharp
var brandClause = await brandPromptResolver.ResolveClauseAsync(state.Assistant.TenantId, ct);
var effectiveSystemPrompt = string.IsNullOrWhiteSpace(brandClause)
    ? baseSystemPrompt
    : $"{brandClause}\n\n{baseSystemPrompt}";
```

Also add a test asserting the prepend order:

```csharp
[Fact]
public async Task ChatExecution_Brand_Profile_Precedes_Assistant_Prompt()
{
    // assert: effectiveSystemPrompt.IndexOf(brandClause) < effectiveSystemPrompt.IndexOf(assistantPrompt)
}
```

- [ ] **Step 5: Register service and run tests**

Register:

```csharp
services.AddScoped<IAiBrandPromptResolver, AiBrandPromptResolver>();
```

Run:

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiBrandPromptResolverTests
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~SafetyProfileResolverTests
```

Expected: PASS.

- [ ] **Step 6: Commit safety and brand defaults**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai
git commit -m "feat(ai): apply tenant safety and brand defaults"
```

---

## Task 9: Public Widget and Widget Credential Handlers

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiSettingsDtos.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/Widgets/GetPublicWidgets/GetPublicWidgetsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Settings/Widgets/GetPublicWidgets/GetPublicWidgetsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/CreatePublicWidget/CreatePublicWidgetCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/CreatePublicWidget/CreatePublicWidgetCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/CreatePublicWidget/CreatePublicWidgetCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/UpdatePublicWidget/UpdatePublicWidgetCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/UpdatePublicWidget/UpdatePublicWidgetCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/UpdatePublicWidget/UpdatePublicWidgetCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/CreateWidgetCredential/CreateWidgetCredentialCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/CreateWidgetCredential/CreateWidgetCredentialCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/RotateWidgetCredential/RotateWidgetCredentialCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/RotateWidgetCredential/RotateWidgetCredentialCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/RevokeWidgetCredential/RevokeWidgetCredentialCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Settings/Widgets/RevokeWidgetCredential/RevokeWidgetCredentialCommandHandler.cs`
- Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiPublicWidgetHandlerTests.cs`

- [ ] **Step 1: Write failing widget handler tests**

Tests:

```csharp
CreateWidget_Fails_When_Widgets_Disabled_By_Plan()
CreateWidget_Fails_When_Widget_Count_Exceeds_Entitlement()
CreateWidget_Fails_When_Quota_Exceeds_Entitlement()
CreateWidget_Stores_Normalized_Origins()
CreateCredential_Returns_Full_Key_Once_And_Stores_Hash()
RotateCredential_Creates_New_Active_Credential()
RevokeCredential_Marks_Credential_Revoked()
WidgetCredential_Does_Not_Create_Core_ApiKey_Row()
```

- [ ] **Step 2: Add widget DTOs**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiPublicWidgetDto(
    Guid Id,
    string Name,
    AiPublicWidgetStatus Status,
    IReadOnlyList<string> AllowedOrigins,
    Guid? DefaultAssistantId,
    string DefaultPersonaSlug,
    int? MonthlyTokenCap,
    int? DailyTokenCap,
    int? RequestsPerMinute,
    string? MetadataJson,
    DateTime CreatedAt);

public sealed record AiWidgetCredentialDto(
    Guid Id,
    Guid WidgetId,
    string KeyPrefix,
    string MaskedKey,
    AiWidgetCredentialStatus Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTime CreatedAt);

public sealed record CreateAiWidgetCredentialResponse(
    AiWidgetCredentialDto Credential,
    string FullKey);
```

- [ ] **Step 3: Implement widget handlers**

Commands/queries:

- `GetPublicWidgetsQuery(Guid? TenantId)`
- `CreatePublicWidgetCommand(Guid? TenantId, string Name, IReadOnlyList<string> AllowedOrigins, Guid? DefaultAssistantId, string DefaultPersonaSlug, int? MonthlyTokenCap, int? DailyTokenCap, int? RequestsPerMinute, string? MetadataJson)`
- `UpdatePublicWidgetCommand(Guid Id, string Name, IReadOnlyList<string> AllowedOrigins, Guid? DefaultAssistantId, string DefaultPersonaSlug, int? MonthlyTokenCap, int? DailyTokenCap, int? RequestsPerMinute, AiPublicWidgetStatus Status, string? MetadataJson)`
- `CreateWidgetCredentialCommand(Guid WidgetId, DateTimeOffset? ExpiresAt)`
- `RotateWidgetCredentialCommand(Guid WidgetId, Guid CredentialId, DateTimeOffset? ExpiresAt)`
- `RevokeWidgetCredentialCommand(Guid WidgetId, Guid CredentialId)`

Credential generation:

```csharp
var randomBytes = RandomNumberGenerator.GetBytes(32);
var randomPart = Convert.ToBase64String(randomBytes)
    .Replace("+", "", StringComparison.Ordinal)
    .Replace("/", "", StringComparison.Ordinal)
    .Replace("=", "", StringComparison.Ordinal);
if (randomPart.Length > 32) randomPart = randomPart[..32];
var fullKey = $"pk_ai_{randomPart}";
var keyPrefix = $"pk_ai_{randomPart[..8]}";
var keyHash = BCrypt.Net.BCrypt.HashPassword(fullKey);
```

Rules:

- widget creation checks `WidgetsEnabled`
- count checks `WidgetMaxCount`
- quotas check widget token/RPM entitlements
- `DefaultAssistantId` must exist in same tenant when provided
- `DefaultPersonaSlug` must exist in same tenant or be `anonymous`
- do not write to core `ApiKey`

- [ ] **Step 4: Run widget tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiPublicWidgetHandlerTests
```

Expected: PASS.

- [ ] **Step 5: Commit widget handlers**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai/Settings
git commit -m "feat(ai): add public widget settings backend"
```

---

## Task 10: AI Settings Controller and Permissions Wiring

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiSettingsController.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Settings/AiSettingsControllerShapeTests.cs`

- [ ] **Step 1: Write failing controller shape tests**

Tests assert:

- controller route is `api/v{version:apiVersion}/ai/settings`
- write actions require `Ai.ManageSettings`
- provider credential endpoints exist
- model default endpoints exist
- widget endpoints exist

- [ ] **Step 2: Add controller**

Create one controller dispatching to MediatR:

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;
using Starter.Module.AI.Application.Queries.Settings.GetAiTenantSettings;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/settings")]
public sealed class AiSettingsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> Get([FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAiTenantSettingsQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPut]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> Upsert([FromBody] UpsertAiTenantSettingsCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }
}
```

Add the provider credential, model default, widget, and widget credential actions in the same controller using the command/query types from previous tasks. Keep route names from the design spec exactly.

- [ ] **Step 3: Grant Admin `Ai.ManageSettings`**

Keep existing `Ai.ManageSettings` constant. Do not create extra permissions unless tests prove a missing distinction.

`AIModule.GetDefaultRolePermissions()` (currently at `AIModule.cs:236-292`) grants SuperAdmin `ManageSettings` (`AIModule.cs:248`) but **does not** grant it to Admin (`AIModule.cs:263-283`). The whole point of 5f is that **tenant admins** configure their own AI — spec §7 says "tenant users operate on their own tenant". Without this grant, only SuperAdmin can use the new endpoints, which defeats multi-tenancy.

**Add `AiPermissions.ManageSettings` to the Admin role array** in `AIModule.GetDefaultRolePermissions()`:

```csharp
yield return ("Admin", [
    AiPermissions.Chat,
    AiPermissions.ViewConversations,
    // ... existing permissions ...
    AiPermissions.ModerationView,
    AiPermissions.ManageSettings,   // ← Plan 5f: tenant admins manage their own AI settings
]);
```

Add an acid-style test that fails if the grant regresses:

```csharp
[Fact]
public void Admin_Default_Permissions_Include_AiManageSettings()
{
    var module = new AIModule();
    var admin = module.GetDefaultRolePermissions().Single(r => r.Role == "Admin");
    admin.Permissions.Should().Contain(AiPermissions.ManageSettings);
}
```

- [ ] **Step 4: Run controller shape tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiSettingsControllerShapeTests
```

Expected: PASS.

- [ ] **Step 5: Commit controller**

```bash
git add src/modules/Starter.Module.AI tests/Starter.Api.Tests/Ai/Settings
git commit -m "feat(ai): expose AI settings admin API"
```

---

## Task 11: Plan 5f Acid Tests and Regression Suite

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5fAcidTests.cs`
- Modify existing tests when constructor signatures change

- [ ] **Step 1: Add acid tests**

Create acid tests covering:

```csharp
Acid_5f_1_Default_Tenant_Policy_Is_PlatformOnly()
Acid_5f_2_Byok_Disabled_Cannot_Create_Tenant_Provider_Credential()
Acid_5f_3_TenantKeysRequired_Fails_Before_Provider_Call_When_Key_Missing()
Acid_5f_4_Byok_Run_Does_Not_Consume_Platform_Credit()
Acid_5f_5a_Widget_Credential_Does_Not_Create_Core_ApiKey_Row()
Acid_5f_5b_Widget_Credential_Cannot_Authenticate_Core_ApiKey_Handler()
Acid_5f_6_No_Public_Auth_Surface_Wired_For_Widget_Credentials()
```

**`Acid_5f_5a`** — assert that creating an `AiWidgetCredential` writes zero rows to the core `ApiKeys` table.

**`Acid_5f_5b`** — positive attack assertion. Create a widget credential, take the returned full key (`pk_ai_*`), simulate an authenticated HTTP call against any core controller using `X-Api-Key: <full key>`, and assert the request returns 401 / fails authentication. The existing `ApiKeyAuthenticationHandler` reads the core `ApiKeys` table by prefix; it must not find a row, and any future regression that mistakenly writes widget keys into core `ApiKeys` will surface here. Use the in-memory test server pattern from prior plans.

**`Acid_5f_6`** — assert the phase boundary structurally, not by hope:
1. `services.GetServices<IAuthenticationHandler>()` (or `IAuthenticationSchemeProvider.GetAllSchemesAsync()`) contains **no** scheme whose name starts with `"AiWidget"` or whose handler resolves widget credentials.
2. No controller in `Starter.Module.AI` has an action whose route prefix is `/api/v*/public` or that carries `[AllowAnonymous]` plus widget-credential lookup logic. Scan via reflection.
3. `AiWidgetCredential` rows are addressable only via `Ai.ManageSettings`-gated handlers — try a `GET` on a hypothetical public endpoint (`/api/v1/public/ai/widgets/{id}/chat`) and assert the route is unmapped (404) rather than 401/403.

These three concrete assertions are what make the boundary load-bearing. Without them the test is decorative.

- [ ] **Step 2: Run acid tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Plan5fAcidTests
```

Expected: PASS.

- [ ] **Step 3: Run focused AI settings suite**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Ai.Settings
```

Expected: PASS.

- [ ] **Step 4: Run cost, moderation, runtime regression filters**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai.Costs|FullyQualifiedName~Ai.Moderation|FullyQualifiedName~Ai.Runtime"
```

Expected: PASS.

- [ ] **Step 5: Run full backend test project**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit acid tests**

```bash
git add tests/Starter.Api.Tests/Ai src/modules/Starter.Module.AI
git commit -m "test(ai): add plan 5f acid coverage"
```

---

## Task 12: Final Verification and Documentation Check

Run this task from the repository root.

**Files:**
- Modify: `docs/superpowers/specs/2026-04-29-ai-plan-5f-admin-ai-settings-design.md` only if implementation reveals a spec correction
- Modify: `docs/superpowers/specs/2026-04-23-ai-module-vision-revised-design.md` only if the 8f forward link needs a wording correction

- [ ] **Step 1: Verify no unrelated files are dirty**

```bash
git status --short
```

Expected: only intended 5f implementation files are dirty before the final commit, or clean after the final commit.

- [ ] **Step 2: Verify no EF migration artifacts were created**

```bash
git status --short | rg -n "Migrations|Migration|ModelSnapshot"
```

Expected: no output. If this finds generated EF artifacts, remove them from the boilerplate branch and keep only model/configuration/tests.

- [ ] **Step 3: Search for accidental placeholders**

```bash
rg -n -e "TB[D]" -e "TO[D]O" -e "PLACEHOLD[E]R" -e "implement[[:space:]]later" -e "fill[[:space:]]in[[:space:]]details" boilerplateBE/src/modules/Starter.Module.AI boilerplateBE/tests/Starter.Api.Tests/Ai docs/superpowers/specs/2026-04-29-ai-plan-5f-admin-ai-settings-design.md
```

Expected: no output.

- [ ] **Step 4: Verify 8f forward-link still exists**

```bash
rg -n "AiPublicWidget|AiWidgetCredential|8f" docs/superpowers/specs/2026-04-23-ai-module-vision-revised-design.md
```

Expected: output includes the Plan 8f row saying it consumes `AiPublicWidget` and `AiWidgetCredential`.

- [ ] **Step 5: Build backend**

```bash
dotnet build boilerplateBE/src/Starter.Api/Starter.Api.csproj
```

Expected: build succeeds with 0 errors.

- [ ] **Step 6: Run full backend tests one final time**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj
```

Expected: PASS.

- [ ] **Step 7: Final commit**

```bash
git add boilerplateBE/src boilerplateBE/tests docs
git commit -m "feat(ai): complete admin AI settings backend"
```

Skip this commit if every task already committed all final files and `git status --short` is clean.
