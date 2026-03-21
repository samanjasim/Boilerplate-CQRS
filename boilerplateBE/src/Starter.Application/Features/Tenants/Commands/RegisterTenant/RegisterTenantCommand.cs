using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.RegisterTenant;

public sealed record RegisterTenantCommand(
    string CompanyName,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword) : IRequest<Result<Guid>>;
