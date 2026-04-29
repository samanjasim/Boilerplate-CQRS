using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.RevokeWidgetCredential;

internal sealed class RevokeWidgetCredentialCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<RevokeWidgetCredentialCommand, Result>
{
    public async Task<Result> Handle(RevokeWidgetCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Result.Failure(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to revoke AI widget credentials."));

        var credential = await db.AiWidgetCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.Id == request.CredentialId &&
                c.WidgetId == request.WidgetId &&
                c.TenantId == currentUser.TenantId.Value &&
                c.Status == AiWidgetCredentialStatus.Active, ct);

        if (credential is null)
            return Result.Failure(AiSettingsErrors.WidgetNotFound);

        credential.Revoke();
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
