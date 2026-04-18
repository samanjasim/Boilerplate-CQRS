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
                    logger.LogDebug("Skipping mention email for {UserId} — preference disabled", recipient.Id);
                    continue;
                }

                var variables = new Dictionary<string, object>
                {
                    ["mentionerName"] = mentionerName,
                    ["entityType"] = notification.EntityType,
                    ["entityId"] = notification.EntityId.ToString(),
                    ["commentBody"] = "",
                    ["appUrl"] = appUrl,
                };

                await messageDispatcher.SendAsync(
                    TemplateName, recipient.Id, variables, recipient.TenantId, cancellationToken);
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
