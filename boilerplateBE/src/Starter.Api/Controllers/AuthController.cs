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
using Starter.Application.Features.Users.Queries.GetCurrentUser;

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

    /// <summary>
    /// Register a new user account.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

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
}
