using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Commands.CreateTodo;

/// <summary>
/// Creates a new TodoItem and returns its new Guid id.
/// </summary>
public sealed record CreateTodoCommand(
    string        Title,
    string?       Description,
    TodoPriority  Priority,
    DateTimeOffset? DueDate
) : IRequest<Guid>;
