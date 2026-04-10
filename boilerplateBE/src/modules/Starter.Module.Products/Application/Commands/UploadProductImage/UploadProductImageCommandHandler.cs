using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Enums;
using Starter.Module.Products.Domain.Errors;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.UploadProductImage;

internal sealed class UploadProductImageCommandHandler(
    ProductsDbContext context,
    IFileService fileService) : IRequestHandler<UploadProductImageCommand, Result>
{
    public async Task<Result> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        await using var stream = request.File.OpenReadStream();
        var file = await fileService.UploadAsync(
            stream,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            FileCategory.Attachment,
            entityId: product.Id,
            entityType: "Product",
            ct: cancellationToken);

        var oldFileId = product.ImageFileId;
        product.SetImage(file.Id);
        await context.SaveChangesAsync(cancellationToken);

        if (oldFileId.HasValue)
            await fileService.DeleteAsync(oldFileId.Value, cancellationToken);

        return Result.Success();
    }
}
