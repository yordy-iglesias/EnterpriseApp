using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Commands.UpdateTodo;

/// <summary>
/// Updates an existing TodoItem's mutable fields.
/// Returns Unit (void-equivalent in MediatR).
/// </summary>
public sealed record UpdateTodoCommand(
    Guid          Id,
    string        Title,
    string?       Description,
    TodoPriority  Priority,
    DateTimeOffset? DueDate
) : IRequest;
