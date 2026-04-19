using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Infrastructure.Capabilities.NullObjects;
using Starter.Module.CommentsActivity.Application.EventHandlers;
using Starter.Module.CommentsActivity.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Application;

public sealed class EmailMentionedUsersTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    private static IConfiguration BuildConfig(string baseUrl = "https://app.example.com") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:BaseUrl"] = baseUrl,
            })
            .Build();

    private static UserSummary MakeUser(Guid id, Guid? tenantId = null) =>
        new(id, tenantId ?? TenantId, $"user-{id:N}", $"user-{id:N}@test.com", $"User {id:N}", "Active");

    private static CommentCreatedEvent MakeEvent(
        string? mentionsJson, Guid? authorId = null) =>
        new(Guid.NewGuid(), "Product", Guid.NewGuid(), TenantId,
            authorId ?? AuthorId, mentionsJson, null);

    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TwoRecipients_DispatchesEmailToEach()
    {
        var user1 = MakeUser(Guid.NewGuid());
        var user2 = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user1.Id, user2.Id });

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user1, user2 });
        prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            dispatcher.Object, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(MakeEvent(mentionsJson), CancellationToken.None);

        dispatcher.Verify(
            d => d.SendAsync("comment.user-mentioned", user1.Id,
                It.IsAny<Dictionary<string, object>>(), user1.TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
        dispatcher.Verify(
            d => d.SendAsync("comment.user-mentioned", user2.Id,
                It.IsAny<Dictionary<string, object>>(), user2.TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SelfMention_Excluded()
    {
        var otherUser = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { AuthorId, otherUser.Id });

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { otherUser });
        prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            dispatcher.Object, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(MakeEvent(mentionsJson), CancellationToken.None);

        dispatcher.Verify(
            d => d.SendAsync(It.IsAny<string>(), AuthorId,
                It.IsAny<Dictionary<string, object>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        dispatcher.Verify(
            d => d.SendAsync(It.IsAny<string>(), otherUser.Id,
                It.IsAny<Dictionary<string, object>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PreferenceOptOut_SkipsDispatch()
    {
        var user = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user.Id });

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        prefReader.Setup(p => p.IsEmailEnabledAsync(user.Id, "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            dispatcher.Object, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(MakeEvent(mentionsJson), CancellationToken.None);

        dispatcher.Verify(
            d => d.SendAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidMentionsJson_BailsWithoutDispatching()
    {
        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            dispatcher.Object, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(MakeEvent("not-valid-json!!!"), CancellationToken.None);

        dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_NullMentionsJson_BailsEarly()
    {
        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            dispatcher.Object, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(MakeEvent(null), CancellationToken.None);

        dispatcher.VerifyNoOtherCalls();
        userReader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_FirstRecipientThrows_SecondStillDispatched()
    {
        var user1 = MakeUser(Guid.NewGuid());
        var user2 = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user1.Id, user2.Id });

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user1, user2 });
        prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // First recipient throws
        dispatcher.Setup(d => d.SendAsync("comment.user-mentioned", user1.Id,
                It.IsAny<Dictionary<string, object>>(), user1.TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            dispatcher.Object, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        Func<Task> act = () => handler.Handle(MakeEvent(mentionsJson), CancellationToken.None);
        await act.Should().NotThrowAsync();

        dispatcher.Verify(
            d => d.SendAsync("comment.user-mentioned", user2.Id,
                It.IsAny<Dictionary<string, object>>(), user2.TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullObjectDispatcher_CompletesWithoutError()
    {
        var user = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user.Id });

        var realDispatcher = new NullMessageDispatcher(
            NullLogger<NullMessageDispatcher>.Instance);
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            realDispatcher, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        Func<Task> act = () => handler.Handle(MakeEvent(mentionsJson), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_VariablesIncludeCorrectFields()
    {
        var user = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user.Id });
        var authorUser = MakeUser(AuthorId);

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorUser);
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Dictionary<string, object>? capturedVars = null;
        dispatcher.Setup(d => d.SendAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, Dictionary<string, object>, Guid?, CancellationToken>(
                (_, _, vars, _, _) => capturedVars = vars)
            .ReturnsAsync(Guid.NewGuid());

        var evt = MakeEvent(mentionsJson);

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(
            dispatcher.Object, userReader.Object, prefReader.Object,
            BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(evt, CancellationToken.None);

        capturedVars.Should().NotBeNull();
        capturedVars!.Should().ContainKey("entityType").WhoseValue.Should().Be(evt.EntityType);
        capturedVars.Should().ContainKey("mentionerName").WhoseValue.Should().Be(authorUser.DisplayName);
        capturedVars.Should().ContainKey("appUrl").WhoseValue.Should().Be("https://app.example.com");
    }

    [Fact]
    public async Task Handle_CommentBody_IncludedInVariables()
    {
        var user = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user.Id });

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { user });
        prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Dictionary<string, object>? capturedVars = null;
        dispatcher.Setup(d => d.SendAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, Dictionary<string, object>, Guid?, CancellationToken>((_, _, vars, _, _) => capturedVars = vars)
            .ReturnsAsync(Guid.NewGuid());

        var evt = new CommentCreatedEvent(Guid.NewGuid(), "Product", Guid.NewGuid(), TenantId, AuthorId, mentionsJson, null, Body: "This is a short comment body");
        var handler = new EmailMentionedUsersOnCommentCreatedHandler(dispatcher.Object, userReader.Object, prefReader.Object, BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(evt, CancellationToken.None);

        capturedVars.Should().NotBeNull();
        capturedVars!["commentBody"].Should().Be("This is a short comment body");
    }

    [Fact]
    public async Task Handle_LongBody_TruncatedTo200Chars()
    {
        var user = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user.Id });
        var longBody = new string('x', 300);

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { user });
        prefReader.Setup(p => p.IsEmailEnabledAsync(It.IsAny<Guid>(), "CommentMentioned", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Dictionary<string, object>? capturedVars = null;
        dispatcher.Setup(d => d.SendAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, Dictionary<string, object>, Guid?, CancellationToken>((_, _, vars, _, _) => capturedVars = vars)
            .ReturnsAsync(Guid.NewGuid());

        var evt = new CommentCreatedEvent(Guid.NewGuid(), "Product", Guid.NewGuid(), TenantId, AuthorId, mentionsJson, null, Body: longBody);
        var handler = new EmailMentionedUsersOnCommentCreatedHandler(dispatcher.Object, userReader.Object, prefReader.Object, BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        await handler.Handle(evt, CancellationToken.None);

        capturedVars.Should().NotBeNull();
        var body = (string)capturedVars!["commentBody"];
        body.Should().HaveLength(201); // 200 chars + ellipsis "…"
        body.Should().EndWith("…");
    }

    [Fact]
    public async Task Handle_PreferenceReaderThrows_ContinuesToNextRecipient()
    {
        var user1 = MakeUser(Guid.NewGuid());
        var user2 = MakeUser(Guid.NewGuid());
        var mentionsJson = JsonSerializer.Serialize(new[] { user1.Id, user2.Id });

        var dispatcher = new Mock<IMessageDispatcher>();
        var userReader = new Mock<IUserReader>();
        var prefReader = new Mock<INotificationPreferenceReader>();

        userReader.Setup(r => r.GetAsync(AuthorId, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser(AuthorId));
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { user1, user2 });

        // First user's preference check throws
        prefReader.Setup(p => p.IsEmailEnabledAsync(user1.Id, "CommentMentioned", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));
        prefReader.Setup(p => p.IsEmailEnabledAsync(user2.Id, "CommentMentioned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new EmailMentionedUsersOnCommentCreatedHandler(dispatcher.Object, userReader.Object, prefReader.Object, BuildConfig(), NullLogger<EmailMentionedUsersOnCommentCreatedHandler>.Instance);

        Func<Task> act = () => handler.Handle(MakeEvent(mentionsJson), CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Second user should still get their email
        dispatcher.Verify(d => d.SendAsync("comment.user-mentioned", user2.Id, It.IsAny<Dictionary<string, object>>(), user2.TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
