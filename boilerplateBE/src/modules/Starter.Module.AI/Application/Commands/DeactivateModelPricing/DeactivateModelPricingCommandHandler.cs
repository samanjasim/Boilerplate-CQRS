using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeactivateModelPricing;

internal sealed class DeactivateModelPricingCommandHandler(AiDbContext db)
    : IRequestHandler<DeactivateModelPricingCommand, Result>
{
    public async Task<Result> Handle(DeactivateModelPricingCommand request, CancellationToken ct)
    {
        var entry = await db.AiModelPricings.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (entry is null)
            return Result.Failure(Error.NotFound("AiPricing.NotFound", $"Pricing entry '{request.Id}' not found."));

        entry.Deactivate();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
