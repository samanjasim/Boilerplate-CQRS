using MassTransit;
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UploadDocument;

internal sealed class UploadDocumentCommandHandler(
    AiDbContext db,
    IStorageService storage,
    ICurrentUserService currentUser,
    IPublishEndpoint bus)
    : IRequestHandler<UploadDocumentCommand, Result<AiDocumentDto>>
{
    public async Task<Result<AiDocumentDto>> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<AiDocumentDto>(Domain.Errors.AiErrors.NotAuthenticated);

        var file = request.File;
        var key = $"ai/documents/{Guid.NewGuid():N}/{file.FileName}";

        await using (var s = file.OpenReadStream())
            await storage.UploadAsync(s, key, file.ContentType, ct);

        var doc = AiDocument.Create(
            tenantId: currentUser.TenantId,
            name: string.IsNullOrWhiteSpace(request.Name) ? file.FileName : request.Name!,
            fileName: file.FileName,
            fileRef: key,
            contentType: file.ContentType,
            sizeBytes: file.Length,
            uploadedByUserId: userId);

        db.AiDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        await bus.Publish(new ProcessDocumentMessage(doc.Id), ct);

        return Result.Success(doc.ToDto());
    }
}
