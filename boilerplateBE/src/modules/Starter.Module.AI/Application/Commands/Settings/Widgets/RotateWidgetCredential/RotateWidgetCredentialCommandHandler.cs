using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.Widgets;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.RotateWidgetCredential;

internal sealed class RotateWidgetCredentialCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<RotateWidgetCredentialCommand, Result<CreateAiWidgetCredentialResponse>>
{
    public async Task<Result<CreateAiWidgetCredentialResponse>> Handle(RotateWidgetCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Result.Failure<CreateAiWidgetCredentialResponse>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to rotate AI widget credentials."));

        var credential = await db.AiWidgetCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.Id == request.CredentialId &&
                c.WidgetId == request.WidgetId &&
                c.TenantId == currentUser.TenantId.Value &&
                c.Status == AiWidgetCredentialStatus.Active, ct);

        if (credential is null)
            return Result.Failure<CreateAiWidgetCredentialResponse>(AiSettingsErrors.WidgetNotFound);

        credential.Revoke();
        var generated = AiWidgetCredentialFactory.Generate();
        var replacement = AiWidgetCredential.Create(
            currentUser.TenantId.Value,
            request.WidgetId,
            generated.KeyPrefix,
            generated.KeyHash,
            request.ExpiresAt,
            currentUser.UserId);

        db.AiWidgetCredentials.Add(replacement);
        await db.SaveChangesAsync(ct);

        return Result.Success(new CreateAiWidgetCredentialResponse(
            AiPublicWidgetMappings.ToDto(replacement),
            generated.FullKey));
    }
}
