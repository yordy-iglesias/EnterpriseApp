using EnterpriseApp.Application.Features.TodoItems.Commands.CompleteTodo;
using EnterpriseApp.Application.Features.TodoItems.Commands.CreateTodo;
using EnterpriseApp.Application.Features.TodoItems.Commands.DeleteTodo;
using EnterpriseApp.Application.Features.TodoItems.Commands.UpdateTodo;
using EnterpriseApp.Application.Features.TodoItems.DTOs;
using EnterpriseApp.Application.Features.TodoItems.Queries.GetPagedTodos;
using EnterpriseApp.Application.Features.TodoItems.Queries.GetTodoById;
using EnterpriseApp.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseApp.API.Controllers;

/// <summary>
/// RESTful CRUD for TodoItem.
/// All responses follow RFC 7807 ProblemDetails on error (via GlobalExceptionMiddleware).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TodosController(ISender sender) : ControllerBase
{
    // GET api/todos?page=1&pageSize=20&status=Pending&priority=High
    [HttpGet]
    [ProducesResponseType<PagedTodosDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int           page     = 1,
        [FromQuery] int           pageSize = 20,
        [FromQuery] TodoStatus?   status   = null,
        [FromQuery] TodoPriority? priority = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetPagedTodosQuery(page, pageSize, status, priority), ct);
        return Ok(result);
    }

    // GET api/todos/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TodoItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetTodoByIdQuery(id), ct);
        return Ok(result);
    }

    // POST api/todos
    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTodoCommand cmd,
        CancellationToken ct = default)
    {
        var id = await sender.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    // PUT api/todos/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTodoCommand cmd,
        CancellationToken ct = default)
    {
        if (id != cmd.Id) return BadRequest("Route id must match body id.");
        await sender.Send(cmd, ct);
        return NoContent();
    }

    // PATCH api/todos/{id}/complete
    [HttpPatch("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct = default)
    {
        await sender.Send(new CompleteTodoCommand(id), ct);
        return NoContent();
    }

    // DELETE api/todos/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await sender.Send(new DeleteTodoCommand(id), ct);
        return NoContent();
    }
}
