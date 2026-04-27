using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SetAgentBudget;

internal sealed class SetAgentBudgetCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    ICostCapResolver capResolver) : IRequestHandler<SetAgentBudgetCommand, Result>
{
    public async Task<Result> Handle(SetAgentBudgetCommand request, CancellationToken ct)
    {
        var assistant = await db.AiAssistants
            .FirstOrDefaultAsync(a => a.Id == request.AssistantId, ct);
        if (assistant is null)
            return Result.Failure(AiErrors.AssistantNotFound);

        try
        {
            assistant.SetBudget(
                request.MonthlyCostCapUsd,
                request.DailyCostCapUsd,
                request.RequestsPerMinute);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result.Failure(Error.Validation("AiAgent.BudgetInvalid",
                "Cap values must be non-negative."));
        }

        await db.SaveChangesAsync(ct);

        if (assistant.TenantId is { } tenantId)
            await capResolver.InvalidateAsync(tenantId, assistant.Id, ct);

        return Result.Success();
    }
}
