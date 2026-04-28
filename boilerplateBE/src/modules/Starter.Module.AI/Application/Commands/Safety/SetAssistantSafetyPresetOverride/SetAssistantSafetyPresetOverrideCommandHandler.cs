using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.SetAssistantSafetyPresetOverride;

internal sealed class SetAssistantSafetyPresetOverrideCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<SetAssistantSafetyPresetOverrideCommand, Result>
{
    public async Task<Result> Handle(SetAssistantSafetyPresetOverrideCommand cmd, CancellationToken ct)
    {
        var assistant = await db.AiAssistants.FirstOrDefaultAsync(a => a.Id == cmd.AssistantId, ct);
        if (assistant is null) return Result.Failure(AiErrors.AssistantNotFound);

        if (currentUser.TenantId is { } tenant && assistant.TenantId != tenant)
            return Result.Failure(Error.Forbidden("Cannot manage another tenant's assistant."));

        assistant.SetSafetyPreset(cmd.Preset);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
