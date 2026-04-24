using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Products.Application.Commands.CreateProduct;
using Starter.Module.Products.Application.DTOs;
using Starter.Module.Products.Application.Queries.GetProducts;
using Starter.Module.Products.Constants;
using Starter.Shared.Models;

namespace Starter.Module.Products.Controllers;

public sealed class ProductsController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpPost]
    [Authorize(Policy = ProductPermissions.Create)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleCreatedResult(result, nameof(GetById), new { id = result.IsSuccess ? result.Value : (Guid?)null });
    }

    [HttpGet]
    [Authorize(Policy = ProductPermissions.View)]
    [ProducesResponseType(typeof(PagedApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetProductsQuery(pageNumber, pageSize, searchTerm, status, tenantId), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ProductPermissions.View)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new Application.Queries.GetProductById.GetProductByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ProductPermissions.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Application.Commands.UpdateProduct.UpdateProductCommand command, CancellationToken ct = default)
    {
        if (ValidateRouteId(id, command.Id) is { } mismatch) return mismatch;
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = ProductPermissions.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new Application.Commands.PublishProduct.PublishProductCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = ProductPermissions.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new Application.Commands.ArchiveProduct.ArchiveProductCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/image")]
    [Authorize(Policy = ProductPermissions.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new Application.Commands.UploadProductImage.UploadProductImageCommand(id, file), ct);
        return HandleResult(result);
    }
}
