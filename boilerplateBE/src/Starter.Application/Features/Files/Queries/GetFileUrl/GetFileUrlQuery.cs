using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Files.Queries.GetFileUrl;

public sealed record GetFileUrlQuery(Guid Id) : IRequest<Result<string>>;
