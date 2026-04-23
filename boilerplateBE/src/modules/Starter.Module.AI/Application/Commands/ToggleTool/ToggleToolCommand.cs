using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ToggleTool;

public sealed record ToggleToolCommand(string Name, bool IsEnabled) : IRequest<Result>;
