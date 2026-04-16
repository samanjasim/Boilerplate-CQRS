using System.Text.Json.Serialization;
using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.SetRequiredNotification;

public sealed record SetRequiredNotificationCommand(
    string Category,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel Channel) : IRequest<Result<RequiredNotificationDto>>;
