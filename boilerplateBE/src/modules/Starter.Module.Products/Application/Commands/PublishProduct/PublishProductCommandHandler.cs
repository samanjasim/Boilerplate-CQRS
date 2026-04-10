using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Domain.Errors;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.PublishProduct;

internal sealed class PublishProductCommandHandler(
    ProductsDbContext context,
    IWebhookPublisher webhookPublisher) : IRequestHandler<PublishProductCommand, Result>
{
    public async Task<Result> Handle(PublishProductCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        if (product.Status == ProductStatus.Active)
            return Result.Failure(ProductErrors.AlreadyPublished);

        product.Publish();
        await context.SaveChangesAsync(cancellationToken);

        await webhookPublisher.PublishAsync(
            "product.published",
            product.TenantId,
            new { product.Id, product.Name },
            cancellationToken);

        return Result.Success();
    }
}
