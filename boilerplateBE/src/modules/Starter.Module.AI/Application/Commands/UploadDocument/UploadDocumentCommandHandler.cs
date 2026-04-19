using System.Security.Cryptography;
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
    IApplicationDbContext appDb,
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
        var safeName = SanitizeFileName(file.FileName);
        var key = $"ai/documents/{Guid.NewGuid():N}/{safeName}";

        await using (var s = file.OpenReadStream())
            await storage.UploadAsync(s, key, file.ContentType, ct);

        string contentHash;
        await using (var hashStream = file.OpenReadStream())
        {
            var hashBytes = await SHA256.HashDataAsync(hashStream, ct);
            contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        var doc = AiDocument.Create(
            tenantId: currentUser.TenantId,
            name: string.IsNullOrWhiteSpace(request.Name) ? safeName : request.Name!,
            fileName: safeName,
            fileRef: key,
            contentType: file.ContentType,
            sizeBytes: file.Length,
            uploadedByUserId: userId);
        doc.SetContentHash(contentHash);

        db.AiDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        await bus.Publish(new ProcessDocumentMessage(doc.Id, doc.TenantId, userId), ct);
        await appDb.SaveChangesAsync(ct);

        return Result.Success(doc.ToDto());
    }

    private static string SanitizeFileName(string raw)
    {
        var withoutPath = Path.GetFileName(raw.Replace('\\', '/'));
        var sanitized = new string(withoutPath
            .Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or ' ')
            .ToArray())
            .Trim();
        return string.IsNullOrEmpty(sanitized) ? "upload" : sanitized;
    }
}
