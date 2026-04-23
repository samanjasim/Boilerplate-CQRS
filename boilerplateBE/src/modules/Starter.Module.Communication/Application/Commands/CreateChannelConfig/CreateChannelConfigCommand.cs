using System.Text.Json.Serialization;
using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.CreateChannelConfig;

public sealed record CreateChannelConfigCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel Channel,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ChannelProvider Provider,
    string DisplayName,
    Dictionary<string, string> Credentials,
    bool IsDefault) : IRequest<Result<ChannelConfigDto>>;
