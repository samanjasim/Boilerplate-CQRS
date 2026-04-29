using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.Widgets;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.CreateWidgetCredential;

internal sealed class CreateWidgetCredentialCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<CreateWidgetCredentialCommand, Result<CreateAiWidgetCredentialResponse>>
{
    public async Task<Result<CreateAiWidgetCredentialResponse>> Handle(CreateWidgetCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Result.Failure<CreateAiWidgetCredentialResponse>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to create AI widget credentials."));

        var widgetExists = await db.AiPublicWidgets
            .IgnoreQueryFilters()
            .AnyAsync(w => w.Id == request.WidgetId && w.TenantId == currentUser.TenantId.Value, ct);
        if (!widgetExists)
            return Result.Failure<CreateAiWidgetCredentialResponse>(AiSettingsErrors.WidgetNotFound);

        var generated = AiWidgetCredentialFactory.Generate();
        var credential = AiWidgetCredential.Create(
            currentUser.TenantId.Value,
            request.WidgetId,
            generated.KeyPrefix,
            generated.KeyHash,
            request.ExpiresAt,
            currentUser.UserId);

        db.AiWidgetCredentials.Add(credential);
        await db.SaveChangesAsync(ct);

        return Result.Success(new CreateAiWidgetCredentialResponse(
            AiPublicWidgetMappings.ToDto(credential),
            generated.FullKey));
    }
}
