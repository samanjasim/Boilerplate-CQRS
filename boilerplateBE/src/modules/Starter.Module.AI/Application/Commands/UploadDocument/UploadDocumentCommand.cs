using MediatR;
using Microsoft.AspNetCore.Http;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UploadDocument;

public sealed record UploadDocumentCommand(
    IFormFile File,
    string? Name) : IRequest<Result<AiDocumentDto>>;
