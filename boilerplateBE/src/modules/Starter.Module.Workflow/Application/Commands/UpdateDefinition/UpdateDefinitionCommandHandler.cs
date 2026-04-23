using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.UpdateDefinition;

internal sealed class UpdateDefinitionCommandHandler(
    WorkflowDbContext context) : IRequestHandler<UpdateDefinitionCommand, Result>
{
    public async Task<Result> Handle(UpdateDefinitionCommand request, CancellationToken cancellationToken)
    {
        var definition = await context.WorkflowDefinitions
            .FirstOrDefaultAsync(d => d.Id == request.DefinitionId, cancellationToken);

        if (definition is null)
            return Result.Failure(WorkflowErrors.DefinitionNotFoundById(request.DefinitionId));

        if (definition.IsTemplate)
            return Result.Failure(WorkflowErrors.CannotEditTemplate());

        definition.Update(
            request.DisplayName ?? definition.DisplayName,
            request.Description,
            request.StatesJson ?? definition.StatesJson,
            request.TransitionsJson ?? definition.TransitionsJson);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
