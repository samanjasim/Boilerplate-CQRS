using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantBranding;

public sealed record UpdateTenantBrandingCommand(
    Guid Id,
    Guid? LogoFileId,
    Guid? FaviconFileId,
    bool RemoveLogo,
    bool RemoveFavicon,
    string? PrimaryColor,
    string? SecondaryColor,
    string? Description) : IRequest<Result>;
