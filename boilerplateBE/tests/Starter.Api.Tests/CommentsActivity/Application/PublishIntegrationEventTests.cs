using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Api.Tests.CommentsActivity.Infrastructure;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Application.EventHandlers;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Application;

public sealed class PublishIntegrationEventTests
{
    private const string EntityType = "Product";
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid EditorId = Guid.NewGuid();

    [Fact]
    public async Task Created_HandlerReadsBodyFromTrackedComment_AndPublishes()
    {
        using var db = TestDbContextFactory.InMemory();
        var comment = Comment.Create(TenantId, EntityType, EntityId, null, AuthorId, "hello world", null);
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        var publisher = new Mock<IMessagePublisher>();

        var handler = new PublishCommentCreatedIntegrationEvent(
            db, publisher.Object, TimeProvider.System,
            NullLogger<PublishCommentCreatedIntegrationEvent>.Instance);

        await handler.Handle(
            new CommentCreatedEvent(comment.Id, EntityType, EntityId, TenantId, AuthorId, null, null),
            CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<CommentCreatedIntegrationEvent>(e =>
                    e.CommentId == comment.Id &&
                    e.Body == "hello world" &&
                    e.AuthorId == AuthorId &&
                    e.TenantId == TenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Created_CommentMissing_LogsAndSkips()
    {
        using var db = TestDbContextFactory.InMemory();
        var publisher = new Mock<IMessagePublisher>();

        var handler = new PublishCommentCreatedIntegrationEvent(
            db, publisher.Object, TimeProvider.System,
            NullLogger<PublishCommentCreatedIntegrationEvent>.Instance);

        await handler.Handle(
            new CommentCreatedEvent(Guid.NewGuid(), EntityType, EntityId, TenantId, AuthorId, null, null),
            CancellationToken.None);

        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Edited_HandlerReadsEditorIdFromEvent_NotRequestScope()
    {
        using var db = TestDbContextFactory.InMemory();
        var comment = Comment.Create(TenantId, EntityType, EntityId, null, AuthorId, "original", null);
        comment.Edit("edited", null, EditorId);
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        var publisher = new Mock<IMessagePublisher>();

        var handler = new PublishCommentEditedIntegrationEvent(
            db, publisher.Object, TimeProvider.System,
            NullLogger<PublishCommentEditedIntegrationEvent>.Instance);

        await handler.Handle(
            new CommentEditedEvent(comment.Id, EntityType, EntityId, TenantId, EditorId),
            CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<CommentEditedIntegrationEvent>(e =>
                    e.CommentId == comment.Id &&
                    e.EditorId == EditorId &&
                    e.NewBody == "edited"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Deleted_PublishesMinimalShape()
    {
        var publisher = new Mock<IMessagePublisher>();
        var commentId = Guid.NewGuid();
        var deletedBy = Guid.NewGuid();

        var handler = new PublishCommentDeletedIntegrationEvent(publisher.Object, TimeProvider.System);

        await handler.Handle(
            new CommentDeletedEvent(commentId, EntityType, EntityId, TenantId, deletedBy),
            CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<CommentDeletedIntegrationEvent>(e =>
                    e.CommentId == commentId &&
                    e.DeletedBy == deletedBy &&
                    e.TenantId == TenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReactionToggled_HandlerReadsUserIdFromEvent_NotRequestScope()
    {
        var publisher = new Mock<IMessagePublisher>();
        var commentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var handler = new PublishReactionToggledIntegrationEvent(publisher.Object, TimeProvider.System);

        await handler.Handle(
            new ReactionToggledEvent(commentId, EntityType, EntityId, TenantId, userId, "👍", true),
            CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<ReactionToggledIntegrationEvent>(e =>
                    e.CommentId == commentId &&
                    e.UserId == userId &&
                    e.ReactionType == "👍" &&
                    e.IsAdded),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
