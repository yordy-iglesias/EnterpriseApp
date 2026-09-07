using EnterpriseApp.API.Filters;
using EnterpriseApp.Application.Features.Identity.Users.Commands.ActivateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.DeactivateUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.DeleteUser;
using EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;
using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Application.Features.Identity.Users.Queries.GetUserById;
using EnterpriseApp.Application.Features.Identity.Users.Queries.GetUsers;
using EnterpriseApp.Domain.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseApp.API.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.User.View)]
    [ProducesResponseType<PagedUsersDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUsersQuery(page, pageSize), ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.User.View)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.User.Create)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateUserCommand(body.Email, body.Password, body.FirstName, body.LastName, body.TenantId), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 409, title: result.Error.Code);
        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.User.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateUserCommand(id, body.FirstName, body.LastName), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    [RequirePermission(PermissionCodes.User.Activate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivateUserCommand(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    [RequirePermission(PermissionCodes.User.Deactivate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateUserCommand(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.User.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteUserCommand(id), ct);
        if (result.IsFailure)
            return Problem(result.Error.Description, statusCode: 404, title: result.Error.Code);
        return NoContent();
    }
}

public sealed record CreateUserRequest(string Email, string Password, string FirstName, string LastName, Guid? TenantId);
public sealed record UpdateUserRequest(string FirstName, string LastName);
