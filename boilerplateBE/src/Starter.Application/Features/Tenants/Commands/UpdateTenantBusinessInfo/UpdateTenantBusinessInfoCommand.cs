using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantBusinessInfo;

public sealed record UpdateTenantBusinessInfoCommand(
    Guid Id,
    string? Address,
    string? Phone,
    string? Website,
    string? TaxId) : IRequest<Result>;
