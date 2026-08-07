using EnterpriseApp.API.Filters;
using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Application.Features.Authorization.Roles.Commands.AssignRoleToUser;
using EnterpriseApp.Application.Features.Authorization.Roles.Commands.CreateRole;
using EnterpriseApp.Application.Features.Authorization.Roles.Commands.DeleteRole;
using EnterpriseApp.Application.Features.Authorization.Roles.Commands.GrantPermission;
using EnterpriseApp.Application.Features.Authorization.Roles.Commands.RevokePermission;
using EnterpriseApp.Application.Features.Authorization.Roles.Commands.UnassignRoleFromUser;
using EnterpriseApp.Application.Features.Authorization.Roles.Queries.GetRoleById;
using EnterpriseApp.Application.Features.Authorization.Roles.Queries.GetRoles;
using EnterpriseApp.Domain.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseApp.API.Controllers;

/// <summary>
/// CRUD + assignments for roles. Permission codes follow
/// <c>{module}.{action}[.{qualifier}]</c> (see <c>.claude/rules/authorization.md</c>).
/// </summary>
[ApiController]
[Route("api/v1/roles")]
public sealed class RolesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Role.View)]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetRolesQuery(), ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Role.View)]
    [ProducesResponseType<RoleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRoleByIdQuery(id), ct);
        if (result.IsFailure) return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.Role.Create)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoleCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd, ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 409, title: result.Error.Code);
        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.Role.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteRoleCommand(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }

    // ── Permission grants on the role ────────────────────────────────────────
    [HttpPost("{id:guid}/permissions")]
    [RequirePermission(PermissionCodes.Permission.Grant)]
    [RequireStepUpMfa]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Grant(
        Guid id,
        [FromBody] GrantPermissionRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GrantPermissionCommand(id, body.PermissionCode, body.ExpiresAt), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(result.Error.Description, statusCode: 400, title: result.Error.Code);
    }

    [HttpDelete("{id:guid}/permissions/{permissionCode}")]
    [RequirePermission(PermissionCodes.Permission.Revoke)]
    [RequireStepUpMfa]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid id, string permissionCode, CancellationToken ct)
    {
        var result = await sender.Send(new RevokePermissionCommand(id, permissionCode), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
    }

    // ── Assigning the role to a user ─────────────────────────────────────────
    [HttpPost("{id:guid}/users/{userId}")]
    [RequirePermission(PermissionCodes.Role.Assign)]
    [RequireStepUpMfa]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignToUser(
        Guid id, string userId, CancellationToken ct)
    {
        var result = await sender.Send(new AssignRoleToUserCommand(userId, id), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(result.Error.Description, statusCode: 409, title: result.Error.Code);
    }

    [HttpDelete("{id:guid}/users/{userId}")]
    [RequirePermission(PermissionCodes.Role.Revoke)]
    [RequireStepUpMfa]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignFromUser(
        Guid id, string userId, CancellationToken ct)
    {
        var result = await sender.Send(new UnassignRoleFromUserCommand(userId, id), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
    }
}

public sealed record GrantPermissionRequest(
    string PermissionCode,
    DateTimeOffset? ExpiresAt);
