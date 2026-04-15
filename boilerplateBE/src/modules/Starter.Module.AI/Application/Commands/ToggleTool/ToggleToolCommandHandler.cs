using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ToggleTool;

internal sealed class ToggleToolCommandHandler(AiDbContext context)
    : IRequestHandler<ToggleToolCommand, Result>
{
    public async Task<Result> Handle(
        ToggleToolCommand request,
        CancellationToken cancellationToken)
    {
        var tool = await context.AiTools
            .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);
        if (tool is null)
            return Result.Failure(AiErrors.ToolNotFound);

        tool.Toggle(request.IsEnabled);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
