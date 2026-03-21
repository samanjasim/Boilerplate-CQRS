using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenant;

public sealed record UpdateTenantCommand(
    Guid Id,
    string Name,
    string? Slug) : IRequest<Result>;
