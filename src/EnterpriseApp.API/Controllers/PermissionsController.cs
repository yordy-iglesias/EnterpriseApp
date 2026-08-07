using EnterpriseApp.API.Filters;
using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Application.Features.Authorization.Permissions.Queries.GetPermissions;
using EnterpriseApp.Application.Features.Authorization.Users.Queries.GetUserPermissions;
using EnterpriseApp.Domain.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseApp.API.Controllers;

/// <summary>Read-only catalog of permissions and per-user resolved permission sets.</summary>
[ApiController]
[Route("api/v1/permissions")]
public sealed class PermissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Permission.View)]
    [ProducesResponseType<IReadOnlyList<PermissionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? module, CancellationToken ct)
    {
        var result = await sender.Send(new GetPermissionsQuery(module), ct);
        return Ok(result.Value);
    }

    [HttpGet("users/{userId}")]
    [RequirePermission(PermissionCodes.User.View)]
    [ProducesResponseType<UserPermissionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForUser(string userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserPermissionsQuery(userId), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
    }
}
