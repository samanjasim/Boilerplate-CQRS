using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Domain.Errors;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.ArchiveProduct;

internal sealed class ArchiveProductCommandHandler(
    ProductsDbContext context,
    IWebhookPublisher webhookPublisher) : IRequestHandler<ArchiveProductCommand, Result>
{
    public async Task<Result> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        if (product.Status == ProductStatus.Draft)
            return Result.Failure(ProductErrors.CannotArchiveDraft);

        if (product.Status == ProductStatus.Archived)
            return Result.Failure(ProductErrors.AlreadyArchived);

        product.Archive();
        await context.SaveChangesAsync(cancellationToken);

        await webhookPublisher.PublishAsync(
            "product.archived",
            product.TenantId,
            new { product.Id, product.Name },
            cancellationToken);

        return Result.Success();
    }
}
