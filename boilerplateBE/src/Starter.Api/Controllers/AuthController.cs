using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Auth.Commands.Login;
using Starter.Application.Features.Auth.Commands.Register;
using Starter.Application.Features.Auth.Commands.RefreshToken;
using Starter.Application.Features.Auth.Commands.ChangePassword;
using Starter.Application.Features.Auth.Commands.SendEmailVerification;
using Starter.Application.Features.Auth.Commands.VerifyEmail;
using Starter.Application.Features.Auth.Commands.ForgotPassword;
using Starter.Application.Features.Auth.Commands.ResetPassword;
using Starter.Application.Features.Auth.Commands.Setup2FA;
using Starter.Application.Features.Auth.Commands.Verify2FA;
using Starter.Application.Features.Auth.Commands.Disable2FA;
using Starter.Application.Features.Tenants.Commands.RegisterTenant;
using Starter.Application.Features.Users.Queries.GetCurrentUser;
using Starter.Application.Features.Auth.Commands.InviteUser;
using Starter.Application.Features.Auth.Commands.AcceptInvite;
using Starter.Application.Features.Auth.Commands.RevokeInvite;
using Starter.Application.Features.Auth.Queries.GetInvitations;
using Starter.Application.Features.Auth.Queries.GetSessions;
using Starter.Application.Features.Auth.Queries.GetLoginHistory;
using Starter.Application.Features.Auth.Commands.RevokeSession;
using Starter.Application.Features.Auth.Commands.RevokeAllSessions;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Authentication endpoints.
/// </summary>
public sealed class AuthController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Login with email and password.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    // NOTE: Public self-registration is disabled. In a multi-tenant SaaS, users
    // should register via /register-tenant (creates tenant + owner) or accept an
    // invitation. Re-enable this endpoint when a tenant-aware registration flow
    // is implemented (e.g. registration.self_registration_enabled feature flag).
    //
    // [HttpPost("register")]
    // [AllowAnonymous]
    // public async Task<IActionResult> Register([FromBody] RegisterUserCommand command)
    // {
    //     var result = await Mediator.Send(command);
    //     return HandleResult(result);
    // }

    /// <summary>
    /// Refresh access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Change password for the current authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Send email verification OTP to the specified email address.
    /// </summary>
    [HttpPost("send-email-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendEmailVerification([FromBody] SendEmailVerificationCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Verify email address using OTP code.
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Request a password reset OTP sent to the specified email.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Reset password using OTP code.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Register a new tenant with an admin user.
    /// </summary>
    [HttpPost("register-tenant")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Set up two-factor authentication for the current user.
    /// </summary>
    [HttpPost("2fa/setup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Setup2FA()
    {
        var result = await Mediator.Send(new Setup2FACommand());
        return HandleResult(result);
    }

    /// <summary>
    /// Verify and enable two-factor authentication.
    /// </summary>
    [HttpPost("2fa/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify2FA([FromBody] Verify2FACommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Disable two-factor authentication.
    /// </summary>
    [HttpPost("2fa/disable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable2FA([FromBody] Disable2FACommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Get current authenticated user profile.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await Mediator.Send(new GetCurrentUserQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Invite a user by email to join the current tenant.
    /// </summary>
    [HttpPost("invite")]
    [Authorize(Policy = Permissions.Users.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Accept an invitation and create user account.
    /// </summary>
    [HttpPost("accept-invite")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Get paginated list of invitations for the current tenant.
    /// </summary>
    [HttpGet("invitations")]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvitations([FromQuery] GetInvitationsQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Revoke (delete) a pending invitation.
    /// </summary>
    [HttpDelete("invitations/{id:guid}")]
    [Authorize(Policy = Permissions.Users.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvitation(Guid id)
    {
        var result = await Mediator.Send(new RevokeInviteCommand(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Get active sessions for the current user.
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSessions([FromHeader(Name = "X-Refresh-Token")] string? refreshToken = null)
    {
        var result = await Mediator.Send(new GetSessionsQuery(refreshToken));
        return HandleResult(result);
    }

    /// <summary>
    /// Revoke a specific session.
    /// </summary>
    [HttpDelete("sessions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        var result = await Mediator.Send(new RevokeSessionCommand(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Revoke all sessions except the current one.
    /// </summary>
    [HttpDelete("sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeAllSessions([FromHeader(Name = "X-Refresh-Token")] string? refreshToken = null)
    {
        var result = await Mediator.Send(new RevokeAllSessionsCommand(refreshToken));
        return HandleResult(result);
    }

    /// <summary>
    /// Get login history for the current user.
    /// </summary>
    [HttpGet("login-history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLoginHistory([FromQuery] GetLoginHistoryQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }
}
