using MediatR;
using Microsoft.AspNetCore.Http;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.UploadProductImage;

public sealed record UploadProductImageCommand(Guid Id, IFormFile File) : IRequest<Result>;
