using Starter.Abstractions.Paging;
using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryLogs;

public sealed record GetDeliveryLogsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    DeliveryStatus? Status = null,
    NotificationChannel? Channel = null,
    string? TemplateName = null,
    DateTime? From = null,
    DateTime? To = null) : IRequest<Result<PaginatedList<DeliveryLogDto>>>;
