using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Products.Domain.Errors;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.UpdateProduct;

internal sealed class UpdateProductCommandHandler(
    ProductsDbContext context,
    ICurrentUserService currentUser,
    IWebhookPublisher webhookPublisher) : IRequestHandler<UpdateProductCommand, Result>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        product.Update(request.Name, request.Description, request.Price, request.Currency);

        // Only superadmin (null tenantId) can reassign a product to a different tenant.
        // Tenant users are silently scoped — any TenantId in the command is ignored.
        // Unsetting a tenant (TenantId = null) is intentionally unsupported to prevent
        // orphaned products that no tenant user can access.
        if (currentUser.TenantId is null && request.TenantId.HasValue)
            product.SetTenant(request.TenantId.Value);

        await context.SaveChangesAsync(cancellationToken);

        await webhookPublisher.PublishAsync(
            "product.updated",
            product.TenantId,
            new { product.Id, product.Name, product.Price, product.Currency },
            cancellationToken);

        return Result.Success();
    }
}
