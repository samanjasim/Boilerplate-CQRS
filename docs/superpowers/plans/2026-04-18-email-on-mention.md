# Email-on-Mention Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send email notifications via the Communication module when users are @mentioned in comments, with graceful degradation when Communication is absent and per-user opt-out via notification preferences.

**Architecture:** New MediatR handler `EmailMentionedUsersOnCommentCreatedHandler` in the Comments module calls `IMessageDispatcher.SendAsync` (Communication capability). A new `INotificationPreferenceReader` capability in `Starter.Abstractions` lets the handler check per-user preferences without coupling to `Starter.Application`. The frontend adds a `CommentMention` row to the existing notification preferences panel with the email toggle disabled when `isModuleActive('communication')` is false.

**Tech Stack:** .NET 10 (MediatR, EF Core), React 19 (TypeScript), TanStack Query, Tailwind CSS 4

---

### Task 1: `INotificationPreferenceReader` capability contract + Null Object

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/INotificationPreferenceReader.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullNotificationPreferenceReader.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs:87` (add `TryAddScoped` registration)

- [ ] **Step 1: Create the capability interface**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/INotificationPreferenceReader.cs`:

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Read-only check for per-user notification preferences. Used by modules
/// (e.g. CommentsActivity) to decide whether to dispatch email notifications
/// without coupling to <c>Starter.Application</c> or <c>IApplicationDbContext</c>.
///
/// Default when no preference row exists: <c>true</c> (opt-out semantics —
/// high-signal notifications like mentions default to enabled).
/// </summary>
public interface INotificationPreferenceReader : ICapability
{
    Task<bool> IsEmailEnabledAsync(Guid userId, string notificationType, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create the Null Object fallback**

Create `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullNotificationPreferenceReader.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="INotificationPreferenceReader"/>.
/// Returns <c>true</c> (email enabled) as the default — preferences are
/// always resolved from the real implementation in practice, but the Null
/// Object keeps isolated module tests compilable.
/// </summary>
public sealed class NullNotificationPreferenceReader(
    ILogger<NullNotificationPreferenceReader> logger) : INotificationPreferenceReader
{
    public Task<bool> IsEmailEnabledAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Notification preference check skipped — returning default enabled (user: {UserId}, type: {Type})",
            userId, notificationType);
        return Task.FromResult(true);
    }
}
```

- [ ] **Step 3: Register Null Object in core DI**

In `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`, add after the existing `IEntityWatcherService` Null Object registration (around line 87):

```csharp
services.TryAddScoped<INotificationPreferenceReader, NullNotificationPreferenceReader>();
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build boilerplateBE/src/Starter.Infrastructure/Starter.Infrastructure.csproj --nologo`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/INotificationPreferenceReader.cs \
       boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullNotificationPreferenceReader.cs \
       boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs
git commit -m "feat: add INotificationPreferenceReader capability contract + Null Object"
```

---

### Task 2: `NotificationPreferenceReaderService` implementation

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NotificationPreferenceReaderService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs` (add `AddScoped` after readers)

- [ ] **Step 1: Write the test**

Create `boilerplateBE/tests/Starter.Api.Tests/Capabilities/NotificationPreferenceReaderTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Domain.Common;
using Starter.Infrastructure.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Capabilities;

public sealed class NotificationPreferenceReaderTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task IsEmailEnabledAsync_NoPreferenceRow_ReturnsTrue()
    {
        using var db = CreateInMemoryContext();
        var sut = new NotificationPreferenceReaderService(db);

        var result = await sut.IsEmailEnabledAsync(UserId, "CommentMention");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailEnabledAsync_ExplicitlyDisabled_ReturnsFalse()
    {
        using var db = CreateInMemoryContext();
        db.Set<NotificationPreference>().Add(
            NotificationPreference.Create(UserId, "CommentMention", emailEnabled: false, inAppEnabled: true));
        await db.SaveChangesAsync();

        var sut = new NotificationPreferenceReaderService(db);

        var result = await sut.IsEmailEnabledAsync(UserId, "CommentMention");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmailEnabledAsync_ExplicitlyEnabled_ReturnsTrue()
    {
        using var db = CreateInMemoryContext();
        db.Set<NotificationPreference>().Add(
            NotificationPreference.Create(UserId, "CommentMention", emailEnabled: true, inAppEnabled: true));
        await db.SaveChangesAsync();

        var sut = new NotificationPreferenceReaderService(db);

        var result = await sut.IsEmailEnabledAsync(UserId, "CommentMention");

        result.Should().BeTrue();
    }

    private static Starter.Infrastructure.Persistence.ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<Starter.Infrastructure.Persistence.ApplicationDbContext>()
            .UseInMemoryDatabase($"pref-reader-{Guid.NewGuid()}")
            .Options;
        return new Starter.Infrastructure.Persistence.ApplicationDbContext(options);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo --filter "FullyQualifiedName~NotificationPreferenceReaderTests"`
Expected: FAIL — `NotificationPreferenceReaderService` does not exist yet

- [ ] **Step 3: Create the implementation**

Create `boilerplateBE/src/Starter.Infrastructure/Capabilities/NotificationPreferenceReaderService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Domain.Common;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Capabilities;

/// <summary>
/// Queries the core <see cref="NotificationPreference"/> table to check
/// per-user email preferences. Returns <c>true</c> (enabled) when no
/// preference row exists — opt-out semantics for high-signal notification
/// types like comment mentions.
/// </summary>
public sealed class NotificationPreferenceReaderService(
    ApplicationDbContext context) : INotificationPreferenceReader
{
    public async Task<bool> IsEmailEnabledAsync(
        Guid userId, string notificationType, CancellationToken ct = default)
    {
        var pref = await context.Set<NotificationPreference>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                np => np.UserId == userId && np.NotificationType == notificationType, ct);

        return pref?.EmailEnabled ?? true;
    }
}
```

- [ ] **Step 4: Register in DI**

In `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`, add after the existing reader registrations (around line 72, after `services.AddScoped<IFileReader, FileReader>()`):

```csharp
services.AddScoped<INotificationPreferenceReader, NotificationPreferenceReaderService>();
```

Note: `AddScoped` (not `TryAddScoped`) because this is the real implementation in core — it overrides the Null Object registered via `TryAddScoped` below.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo --filter "FullyQualifiedName~NotificationPreferenceReaderTests"`
Expected: PASS — 3/3

Note: The `ApplicationDbContext` constructor that takes only `DbContextOptions` may require adjustment if it mandates additional dependencies. If the in-memory constructor fails, check how the Comments module's `TestDbContextFactory` works and adapt — the key is that `context.Set<NotificationPreference>()` must resolve correctly.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Infrastructure/Capabilities/NotificationPreferenceReaderService.cs \
       boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs \
       boilerplateBE/tests/Starter.Api.Tests/Capabilities/NotificationPreferenceReaderTests.cs
git commit -m "feat: implement NotificationPreferenceReaderService with opt-out defaults"
```

---

### Task 3: Add `CommentMention` to the preferences query + seed type

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/Notifications/Queries/GetNotificationPreferences/GetNotificationPreferencesQueryHandler.cs:16-25` (add to `AllTypes` array)

- [ ] **Step 1: Add `CommentMention` to the `AllTypes` array**

The constant `NotificationType.CommentMentioned` already exists at `boilerplateBE/src/Starter.Application/Common/Constants/NotificationType.cs:12`. Add it to the `AllTypes` array in `GetNotificationPreferencesQueryHandler.cs` so the preferences query returns it to the frontend:

In `GetNotificationPreferencesQueryHandler.cs`, change the `AllTypes` array (line 16-25):

```csharp
private static readonly string[] AllTypes =
[
    NotificationType.UserCreated,
    NotificationType.UserInvited,
    NotificationType.RoleChanged,
    NotificationType.PasswordChanged,
    NotificationType.TenantCreated,
    NotificationType.InvitationAccepted,
    NotificationType.LoginFromNewDevice,
    NotificationType.CommentMentioned,
];
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build boilerplateBE/src/Starter.Application/Starter.Application.csproj --nologo`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Notifications/Queries/GetNotificationPreferences/GetNotificationPreferencesQueryHandler.cs
git commit -m "feat: include CommentMention in notification preferences query"
```

---

### Task 4: `EmailMentionedUsersOnCommentCreatedHandler`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Application/EventHandlers/EmailMentionedUsersOnCommentCreatedHandler.cs`

- [ ] **Step 1: Write the tests**

Create `boilerplateBE/tests/Starter.Api.Tests/CommentsActivity/Application/EmailMentionedUsersTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.CommentsActivity.Application.EventHandlers;
using Starter.Module.CommentsActivity.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Application;

public sealed class EmailMentionedUsersTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid RecipientA = Guid.NewGuid();
    private static readonly Guid RecipientB = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly Mock<IMessageDispatcher> _dispatcher = new();
    private readonly Mock<IUserReader> _userReader = new();
    private readonly Mock<INotificationPreferenceReader> _prefReader = new();
    private readonly IConfiguration _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["AppSettings:BaseUrl"] = "https://app.example.com" })
        .Build();

    private EmailMentionedUsersOnCommentCreatedHandler CreateSut() =>
        new(_dispatcher.Object, _userReader.Object, _prefReader.Object, _config,
            NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

    private CommentCreatedEvent CreateEvent(List<Guid>? mentionedIds, string body = "hello world") =>
        new(Guid.NewGuid(), "Product", Guid.NewGuid(), TenantId, AuthorId,
            mentionedIds is null ? null : JsonSerializer.Serialize(mentionedIds),
            ParentCommentId: null);

    private void SetupRecipients(params (Guid Id, Guid? TenantId, string Name)[] users)
    {
        var summaries = users.Select(u =>
            new UserSummary(u.Id, u.TenantId, u.Name.ToLower(), $"{u.Name}@test.com", u.Name, "Active")).ToList();
        _userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);
    }

    [Fact]
    public async Task Handle_TwoRecipients_DispatchesEmailToEach()
    {
        SetupRecipients((RecipientA, TenantId, "Alice"), (RecipientB, TenantId, "Bob"));
        _prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(CreateEvent([RecipientA, RecipientB]), CancellationToken.None);

        _dispatcher.Verify(d => d.SendAsync(
            "notification.comment-mention",
            RecipientA,
            It.IsAny<Dictionary<string, object>>(),
            TenantId,
            It.IsAny<CancellationToken>()), Times.Once);

        _dispatcher.Verify(d => d.SendAsync(
            "notification.comment-mention",
            RecipientB,
            It.IsAny<Dictionary<string, object>>(),
            TenantId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SelfMention_Excluded()
    {
        SetupRecipients((RecipientA, TenantId, "Alice"));
        _prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(CreateEvent([AuthorId, RecipientA]), CancellationToken.None);

        _dispatcher.Verify(d => d.SendAsync(
            It.IsAny<string>(), AuthorId, It.IsAny<Dictionary<string, object>>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);

        _dispatcher.Verify(d => d.SendAsync(
            It.IsAny<string>(), RecipientA, It.IsAny<Dictionary<string, object>>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PreferenceOptOut_SkipsDispatch()
    {
        SetupRecipients((RecipientA, TenantId, "Alice"));
        _prefReader.Setup(p => p.IsEmailEnabledAsync(RecipientA, "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateSut().Handle(CreateEvent([RecipientA]), CancellationToken.None);

        _dispatcher.Verify(d => d.SendAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, object>>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidMentionsJson_BailsWithoutDispatching()
    {
        var brokenEvent = new CommentCreatedEvent(
            Guid.NewGuid(), "Product", Guid.NewGuid(), TenantId, AuthorId,
            "not-valid-json!!!", ParentCommentId: null);

        await CreateSut().Handle(brokenEvent, CancellationToken.None);

        _dispatcher.Verify(d => d.SendAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, object>>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullMentionsJson_BailsEarly()
    {
        await CreateSut().Handle(CreateEvent(null), CancellationToken.None);

        _dispatcher.Verify(d => d.SendAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, object>>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FirstRecipientThrows_SecondStillDispatched()
    {
        SetupRecipients((RecipientA, TenantId, "Alice"), (RecipientB, TenantId, "Bob"));
        _prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _dispatcher.Setup(d => d.SendAsync(
                "notification.comment-mention", RecipientA, It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        await CreateSut().Handle(CreateEvent([RecipientA, RecipientB]), CancellationToken.None);

        _dispatcher.Verify(d => d.SendAsync(
            "notification.comment-mention", RecipientB, It.IsAny<Dictionary<string, object>>(),
            TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullObjectDispatcher_CompletesWithoutError()
    {
        var nullDispatcher = new Starter.Infrastructure.Capabilities.NullObjects.NullMessageDispatcher(
            NullLogger<Starter.Infrastructure.Capabilities.NullObjects.NullMessageDispatcher>.Instance);

        SetupRecipients((RecipientA, TenantId, "Alice"));
        _prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new EmailMentionedUsersOnCommentCreatedHandler(
            nullDispatcher, _userReader.Object, _prefReader.Object, _config,
            NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        var act = async () => await sut.Handle(CreateEvent([RecipientA]), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_VariablesIncludeCorrectTemplateName()
    {
        SetupRecipients((RecipientA, TenantId, "Alice"));
        _prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Dictionary<string, object>? capturedVars = null;
        _dispatcher.Setup(d => d.SendAsync(
                "notification.comment-mention", RecipientA,
                It.IsAny<Dictionary<string, object>>(),
                TenantId, It.IsAny<CancellationToken>()))
            .Callback<string, Guid, Dictionary<string, object>, Guid?, CancellationToken>(
                (_, _, vars, _, _) => capturedVars = vars)
            .ReturnsAsync(Guid.NewGuid());

        await CreateSut().Handle(CreateEvent([RecipientA]), CancellationToken.None);

        capturedVars.Should().NotBeNull();
        capturedVars!.Should().ContainKey("entityType");
        capturedVars["entityType"].Should().Be("Product");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo --filter "FullyQualifiedName~EmailMentionedUsersTests"`
Expected: FAIL — `EmailMentionedUsersOnCommentCreatedHandler` does not exist

- [ ] **Step 3: Create the handler**

Create `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Application/EventHandlers/EmailMentionedUsersOnCommentCreatedHandler.cs`:

```csharp
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.CommentsActivity.Domain.Events;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Dispatches an email notification to every user mentioned in a comment via
/// <see cref="IMessageDispatcher"/>. When the Communication module is not
/// installed, <c>NullMessageDispatcher</c> silently returns <c>Guid.Empty</c>.
///
/// This handler is independent of <see cref="NotifyMentionedUsersOnCommentCreatedHandler"/>
/// (in-app notifications) — a failure here never affects the in-app bell.
/// </summary>
internal sealed class EmailMentionedUsersOnCommentCreatedHandler(
    IMessageDispatcher messageDispatcher,
    IUserReader userReader,
    INotificationPreferenceReader preferenceReader,
    IConfiguration configuration,
    ILogger<EmailMentionedUsersOnCommentCreatedHandler> logger)
    : INotificationHandler<CommentCreatedEvent>
{
    private const string TemplateName = "notification.comment-mention";
    private const string PreferenceType = "CommentMentioned";
    private const int MaxBodyPreviewLength = 200;

    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.MentionsJson)) return;

        List<Guid>? mentionedUserIds;
        try
        {
            mentionedUserIds = JsonSerializer.Deserialize<List<Guid>>(notification.MentionsJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse MentionsJson for comment {CommentId}", notification.CommentId);
            return;
        }

        if (mentionedUserIds is null || mentionedUserIds.Count == 0) return;

        var recipientIds = mentionedUserIds
            .Where(id => id != notification.AuthorId)
            .Distinct()
            .ToList();

        if (recipientIds.Count == 0) return;

        var recipients = await userReader.GetManyAsync(recipientIds, cancellationToken);
        if (recipients.Count == 0) return;

        // Resolve author name for the email template
        var author = await userReader.GetAsync(notification.AuthorId, cancellationToken);
        var mentionerName = author?.DisplayName ?? "Someone";

        var appUrl = configuration.GetValue<string>("AppSettings:BaseUrl") ?? "";

        foreach (var recipient in recipients)
        {
            try
            {
                var emailEnabled = await preferenceReader.IsEmailEnabledAsync(
                    recipient.Id, PreferenceType, cancellationToken);

                if (!emailEnabled)
                {
                    logger.LogDebug(
                        "Skipping mention email for {UserId} — preference disabled", recipient.Id);
                    continue;
                }

                var variables = new Dictionary<string, object>
                {
                    ["mentionerName"] = mentionerName,
                    ["entityType"] = notification.EntityType,
                    ["entityId"] = notification.EntityId.ToString(),
                    ["commentBody"] = "", // Body not in domain event; empty for v1
                    ["appUrl"] = appUrl,
                };

                await messageDispatcher.SendAsync(
                    TemplateName,
                    recipient.Id,
                    variables,
                    recipient.TenantId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to dispatch mention email to {UserId} for comment {CommentId}",
                    recipient.Id, notification.CommentId);
            }
        }
    }
}
```

Note: `CommentCreatedEvent` does not carry the comment body text. The `commentBody` variable is set to empty string for v1 — the template renders without it gracefully (Mustache treats missing/empty as empty). Adding the body to the domain event would be a separate enhancement.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo --filter "FullyQualifiedName~EmailMentionedUsersTests"`
Expected: PASS — 8/8

- [ ] **Step 5: Run full test suite for regression**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo`
Expected: All tests pass (previous 53 + new 8 + 3 from Task 2 = 64 total)

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.CommentsActivity/Application/EventHandlers/EmailMentionedUsersOnCommentCreatedHandler.cs \
       boilerplateBE/tests/Starter.Api.Tests/CommentsActivity/Application/EmailMentionedUsersTests.cs
git commit -m "feat(comments): email mentioned users via IMessageDispatcher

New MediatR handler dispatches email notifications to mentioned users via
the Communication module's IMessageDispatcher capability. Self-mentions
excluded, per-user preference checked via INotificationPreferenceReader,
per-recipient error isolation. NullMessageDispatcher handles the
Communication-absent case silently."
```

---

### Task 5: Template registration in `CommentsActivityModule.SeedDataAsync`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/CommentsActivityModule.cs` (add `SeedDataAsync` method)

- [ ] **Step 1: Add `SeedDataAsync` to `CommentsActivityModule`**

The module currently has no `SeedDataAsync` method. Add it after the existing `MigrateAsync` method (after line 96):

```csharp
public async Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
{
    using var scope = services.CreateScope();

    var templateRegistrar = scope.ServiceProvider.GetRequiredService<ITemplateRegistrar>();

    // Register the mention email template. When the Communication module is not
    // installed, NullTemplateRegistrar silently no-ops — the template is never
    // needed because NullMessageDispatcher also no-ops.
    await templateRegistrar.RegisterTemplateAsync(
        name: "notification.comment-mention",
        moduleSource: "CommentsActivity",
        category: "comments",
        description: "Email sent to a user when they are @mentioned in a comment",
        subjectTemplate: "{{mentionerName}} mentioned you in a comment",
        bodyTemplate: "Hi,\n\n{{mentionerName}} mentioned you in a comment on {{entityType}}.\n\n\"{{commentBody}}\"\n\nView it in the app: {{appUrl}}",
        defaultChannel: NotificationChannelType.Email,
        availableChannels: ["Email", "InApp"],
        variableSchema: new()
        {
            ["mentionerName"] = "Display name of the comment author",
            ["entityType"] = "Type of entity the comment is on (e.g. Product)",
            ["entityId"] = "ID of the entity",
            ["commentBody"] = "First 200 characters of the comment",
            ["appUrl"] = "Base URL of the application",
        },
        sampleVariables: new()
        {
            ["mentionerName"] = "Saman Jasim",
            ["entityType"] = "Product",
            ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            ["commentBody"] = "Great progress on this! Let's discuss in the next standup.",
            ["appUrl"] = "https://app.example.com",
        },
        ct: cancellationToken);

    // Register the event so the Communication module's Trigger Rules UI
    // lists it as an available event source.
    await templateRegistrar.RegisterEventAsync(
        eventName: "comment.user-mentioned",
        moduleSource: "CommentsActivity",
        displayName: "User Mentioned in Comment",
        description: "Fires when a user is @mentioned in a comment",
        ct: cancellationToken);
}
```

Also add the required using at the top of the file:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
```

Note: `Microsoft.Extensions.DependencyInjection` is already imported. Only `Starter.Abstractions.Capabilities` needs adding (for `NotificationChannelType` and `ITemplateRegistrar`). Check: `Starter.Abstractions.Capabilities` is already imported on line 5.

- [ ] **Step 2: Build to verify**

Run: `dotnet build boilerplateBE/src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj --nologo`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.CommentsActivity/CommentsActivityModule.cs
git commit -m "feat(comments): seed mention email template via ITemplateRegistrar"
```

---

### Task 6: Frontend — `CommentMention` preference row with Communication awareness

**Files:**
- Modify: `boilerplateFE/src/features/profile/components/NotificationPreferences.tsx`

- [ ] **Step 1: Add the import and label mapping**

In `boilerplateFE/src/features/profile/components/NotificationPreferences.tsx`, add the import for `isModuleActive`:

```typescript
import { isModuleActive } from '@/config/modules.config';
```

Add `CommentMentioned` to the `notificationTypeLabels` map (after line 19):

```typescript
CommentMentioned: 'notifications.types.commentMentioned',
```

- [ ] **Step 2: Add Communication-aware email toggle logic**

Replace the email toggle `<button>` (lines 85-99) with a version that checks `isModuleActive('communication')` for the `CommentMentioned` type:

```tsx
<div className="flex justify-center">
  {pref.notificationType === 'CommentMentioned' && !isModuleActive('communication') ? (
    <div className="relative group">
      <button
        type="button"
        role="switch"
        aria-checked={false}
        disabled
        className="relative inline-flex h-5 w-9 shrink-0 items-center rounded-full border-2 border-transparent bg-input opacity-50 cursor-not-allowed"
      >
        <span className="pointer-events-none block h-4 w-4 rounded-full bg-background shadow-lg ring-0 translate-x-0" />
      </button>
      <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-1.5 text-xs text-popover-foreground bg-popover border rounded-lg shadow-md opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap pointer-events-none z-10">
        {t('notifications.emailRequiresCommunication')}
      </div>
    </div>
  ) : (
    <button
      type="button"
      role="switch"
      aria-checked={pref.emailEnabled}
      onClick={() => togglePref(pref.notificationType, 'emailEnabled')}
      className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ${
        pref.emailEnabled ? 'bg-primary' : 'bg-input'
      }`}
    >
      <span
        className={`pointer-events-none block h-4 w-4 rounded-full bg-background shadow-lg ring-0 transition-transform ${
          pref.emailEnabled ? 'translate-x-4' : 'translate-x-0'
        }`}
      />
    </button>
  )}
</div>
```

- [ ] **Step 3: Add the i18n key**

Find the English translation file (likely `boilerplateFE/src/locales/en.json` or `boilerplateFE/public/locales/en/translation.json`) and add:

```json
"notifications.types.commentMentioned": "Comment Mentions",
"notifications.emailRequiresCommunication": "Email notifications are available when the Communication module is enabled"
```

If the translation system uses nested keys, follow the existing convention.

- [ ] **Step 4: Build frontend to verify**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds with no TypeScript errors

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/profile/components/NotificationPreferences.tsx
# Also add the translation file if modified
git commit -m "feat(profile): CommentMention preference row with Communication awareness

Email toggle disabled with tooltip when Communication module is absent.
InApp toggle always active. Uses isModuleActive('communication') — a
compile-time flag set by rename.ps1, no runtime API call needed."
```

---

### Task 7: Full build + test verification

**Files:** None — verification only

- [ ] **Step 1: Full backend build**

Run: `dotnet build boilerplateBE --nologo`
Expected: 0 errors

- [ ] **Step 2: Full test suite**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo`
Expected: All tests pass (should be ~64 total)

- [ ] **Step 3: Frontend build**

Run: `cd boilerplateFE && npm run build`
Expected: Clean build

- [ ] **Step 4: Isolation test — Comments without Communication**

```bash
rm -rf _testEmailMention
pwsh scripts/rename.ps1 -Name "_testEmailMention" -OutputDir "." -Modules "commentsActivity" -IncludeMobile:$false
dotnet build _testEmailMention/_testEmailMention-BE --nologo
dotnet test _testEmailMention/_testEmailMention-BE/tests/_testEmailMention.Api.Tests/_testEmailMention.Api.Tests.csproj --nologo
```

Expected: Build succeeds, all tests pass. The `EmailMentionedUsersOnCommentCreatedHandler` compiles and resolves `NullMessageDispatcher` + `NullTemplateRegistrar` + `NotificationPreferenceReaderService` (core, always present) without error.

- [ ] **Step 5: Clean up scratch app**

```bash
rm -rf _testEmailMention
```

- [ ] **Step 6: Final commit if any fixups were needed**

If the isolation test surfaced issues that required code fixes, commit those fixes here.
