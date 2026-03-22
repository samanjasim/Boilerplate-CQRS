using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Files.Commands.DeleteFile;

public sealed record DeleteFileCommand(Guid Id) : IRequest<Result>;
