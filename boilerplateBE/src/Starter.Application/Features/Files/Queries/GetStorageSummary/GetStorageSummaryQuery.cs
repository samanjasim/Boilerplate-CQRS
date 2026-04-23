using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Files.Queries.GetStorageSummary;

public sealed record GetStorageSummaryQuery(bool AllTenants = false) : IRequest<Result<StorageSummaryDto>>;
