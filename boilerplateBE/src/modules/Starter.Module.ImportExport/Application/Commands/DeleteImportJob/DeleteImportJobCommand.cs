using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Commands.DeleteImportJob;

public sealed record DeleteImportJobCommand(Guid Id) : IRequest<Result<Unit>>;
