using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CloneDefinition;

internal sealed class CloneDefinitionCommandHandler(
    WorkflowDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<CloneDefinitionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CloneDefinitionCommand request, CancellationToken cancellationToken)
    {
        var definition = await context.WorkflowDefinitions
            .FirstOrDefaultAsync(d => d.Id == request.DefinitionId, cancellationToken);

        if (definition is null)
            return Result.Failure<Guid>(WorkflowErrors.DefinitionNotFoundById(request.DefinitionId));

        var clone = definition.Clone(currentUser.TenantId);

        // Pre-check the (TenantId, Name) unique constraint so re-cloning a
        // template the tenant already owns returns a clean Conflict error
        // instead of bubbling a raw DbUpdateException from the unique index.
        var conflict = await context.WorkflowDefinitions
            .AnyAsync(d => d.TenantId == currentUser.TenantId && d.Name == clone.Name, cancellationToken);
        if (conflict)
            return Result.Failure<Guid>(Error.Conflict(
                "Workflow.DefinitionAlreadyCloned",
                $"A workflow definition named '{clone.Name}' already exists in this tenant."));

        context.WorkflowDefinitions.Add(clone);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(clone.Id);
    }
}
