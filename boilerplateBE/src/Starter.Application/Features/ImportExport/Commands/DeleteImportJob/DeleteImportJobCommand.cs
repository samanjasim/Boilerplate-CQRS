using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Commands.DeleteImportJob;

public sealed record DeleteImportJobCommand(Guid Id) : IRequest<Result<Unit>>;
