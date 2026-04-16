using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetRegisteredEvents;

public sealed record GetRegisteredEventsQuery : IRequest<Result<List<EventRegistrationDto>>>;
