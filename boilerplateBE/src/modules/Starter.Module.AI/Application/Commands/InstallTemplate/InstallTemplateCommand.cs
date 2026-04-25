using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.InstallTemplate;

public sealed record InstallTemplateCommand(
    string TemplateSlug,
    Guid? TargetTenantId = null,
    Guid? CreatedByUserIdOverride = null) : IRequest<Result<Guid>>;
