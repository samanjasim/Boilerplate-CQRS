using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Constants;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetConversations;

[AiTool(
    Name = "list_my_conversations",
    Description = "List the current user's recent AI conversations with title, message count, and last-message timestamp.",
    Category = "AI",
    RequiredPermission = AiPermissions.ViewConversations,
    IsReadOnly = true)]
public sealed record GetConversationsQuery(
    [property: Description("Page number (1-based). Default 1.")]
    int PageNumber = 1,
    [property: Description("Page size (1–100). Default 20.")]
    int PageSize = 20,
    [property: Description("Free-text search across conversation title.")]
    string? SearchTerm = null,
    [property: Description("Filter to a single assistant by id.")]
    Guid? AssistantId = null)
    : IRequest<Result<PaginatedList<AiConversationDto>>>;
