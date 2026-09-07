using EnterpriseApp.Application.Features.Identity.Auth.Commands.ChangePassword;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.Logout;
using EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Application.Features.Identity.Auth.Queries.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EnterpriseApp.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new LoginCommand(body.Email, body.Password, ip), ct);
        if (result.IsFailure)
        {
            var status = result.Error.Code switch
            {
                "Auth.AccountLocked"   => 423,
                "Auth.AccountInactive" => 403,
                _                      => 401,
            };
            return Problem(result.Error.Description, statusCode: status, title: result.Error.Code);
        }
        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken ct)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new RefreshTokenCommand(body.RefreshToken, ip), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 401, title: result.Error.Code);
        return Ok(result.Value);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest body, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await sender.Send(new LogoutCommand(body.RefreshToken, ip), ct);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await sender.Send(new GetCurrentUserQuery(), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return Ok(result.Value);
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 400, title: result.Error.Code);
        return NoContent();
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
