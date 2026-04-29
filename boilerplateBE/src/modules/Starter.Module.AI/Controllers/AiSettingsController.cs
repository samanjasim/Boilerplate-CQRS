using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Commands.Settings.ModelDefaults.UpsertModelDefault;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.CreateProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RevokeProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RotateProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.TestProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;
using Starter.Module.AI.Application.Commands.Settings.Widgets.CreatePublicWidget;
using Starter.Module.AI.Application.Commands.Settings.Widgets.CreateWidgetCredential;
using Starter.Module.AI.Application.Commands.Settings.Widgets.RevokeWidgetCredential;
using Starter.Module.AI.Application.Commands.Settings.Widgets.RotateWidgetCredential;
using Starter.Module.AI.Application.Commands.Settings.Widgets.UpdatePublicWidget;
using Starter.Module.AI.Application.Queries.Settings.GetAiTenantSettings;
using Starter.Module.AI.Application.Queries.Settings.ModelDefaults.GetModelDefaults;
using Starter.Module.AI.Application.Queries.Settings.ProviderCredentials.GetProviderCredentials;
using Starter.Module.AI.Application.Queries.Settings.Widgets.GetPublicWidgets;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/settings")]
public sealed class AiSettingsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> Get([FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAiTenantSettingsQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPut]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> Upsert([FromBody] UpsertAiTenantSettingsCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpGet("provider-credentials")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> GetProviderCredentials([FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetProviderCredentialsQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPost("provider-credentials")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> CreateProviderCredential(
        [FromBody] CreateProviderCredentialCommand command,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPost("provider-credentials/{id:guid}/rotate")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> RotateProviderCredential(
        Guid id,
        [FromBody] RotateProviderCredentialRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RotateProviderCredentialCommand(id, request.Secret), ct);
        return HandleResult(result);
    }

    [HttpPost("provider-credentials/{id:guid}/revoke")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> RevokeProviderCredential(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RevokeProviderCredentialCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("provider-credentials/{id:guid}/test")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> TestProviderCredential(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new TestProviderCredentialCommand(id), ct);
        return HandleResult(result);
    }

    [HttpGet("model-defaults")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> GetModelDefaults([FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetModelDefaultsQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPut("model-defaults/{agentClass}")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> UpsertModelDefault(
        AiAgentClass agentClass,
        [FromBody] UpsertModelDefaultRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UpsertModelDefaultCommand(
            request.TenantId,
            agentClass,
            request.Provider,
            request.Model,
            request.MaxTokens,
            request.Temperature), ct);
        return HandleResult(result);
    }

    [HttpGet("widgets")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> GetWidgets([FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPublicWidgetsQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPost("widgets")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> CreateWidget(
        [FromBody] CreatePublicWidgetCommand command,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("widgets/{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> UpdateWidget(
        Guid id,
        [FromBody] UpdatePublicWidgetRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UpdatePublicWidgetCommand(
            id,
            request.Name,
            request.AllowedOrigins,
            request.DefaultAssistantId,
            request.DefaultPersonaSlug,
            request.MonthlyTokenCap,
            request.DailyTokenCap,
            request.RequestsPerMinute,
            request.Status,
            request.MetadataJson), ct);
        return HandleResult(result);
    }

    [HttpPost("widgets/{id:guid}/credentials")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> CreateWidgetCredential(
        Guid id,
        [FromBody] CreateWidgetCredentialRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new CreateWidgetCredentialCommand(id, request.ExpiresAt), ct);
        return HandleResult(result);
    }

    [HttpPost("widgets/{id:guid}/credentials/{credentialId:guid}/rotate")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> RotateWidgetCredential(
        Guid id,
        Guid credentialId,
        [FromBody] CreateWidgetCredentialRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RotateWidgetCredentialCommand(id, credentialId, request.ExpiresAt), ct);
        return HandleResult(result);
    }

    [HttpPost("widgets/{id:guid}/credentials/{credentialId:guid}/revoke")]
    [Authorize(Policy = AiPermissions.ManageSettings)]
    public async Task<IActionResult> RevokeWidgetCredential(
        Guid id,
        Guid credentialId,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RevokeWidgetCredentialCommand(id, credentialId), ct);
        return HandleResult(result);
    }
}

public sealed record RotateProviderCredentialRequest(string Secret);

public sealed record UpsertModelDefaultRequest(
    Guid? TenantId,
    AiProviderType Provider,
    string Model,
    int? MaxTokens,
    double? Temperature);

public sealed record UpdatePublicWidgetRequest(
    string Name,
    IReadOnlyList<string> AllowedOrigins,
    Guid? DefaultAssistantId,
    string DefaultPersonaSlug,
    int? MonthlyTokenCap,
    int? DailyTokenCap,
    int? RequestsPerMinute,
    AiPublicWidgetStatus Status,
    string? MetadataJson);

public sealed record CreateWidgetCredentialRequest(DateTimeOffset? ExpiresAt);
