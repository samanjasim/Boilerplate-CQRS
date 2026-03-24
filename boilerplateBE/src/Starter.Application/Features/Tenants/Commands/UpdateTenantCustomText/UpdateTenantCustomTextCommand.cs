using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantCustomText;

public sealed record UpdateTenantCustomTextCommand(
    Guid Id,
    string? LoginPageTitle,
    string? LoginPageSubtitle,
    string? EmailFooterText) : IRequest<Result>;
