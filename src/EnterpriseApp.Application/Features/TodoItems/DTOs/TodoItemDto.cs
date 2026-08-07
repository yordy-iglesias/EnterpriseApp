using EnterpriseApp.Domain.ValueObjects;

namespace EnterpriseApp.Application.Features.TodoItems.DTOs;

/// <summary>
/// Read-only projection of a TodoItem returned from queries.
/// IDs are exposed as plain Guid / string so API consumers stay decoupled from domain types.
/// </summary>
public sealed record TodoItemDto(
    Guid          Id,
    string        Title,
    string?       Description,
    TodoStatus    Status,
    TodoPriority  Priority,
    DateTimeOffset? DueDate,
    DateTimeOffset  CreatedAt,
    string?         CreatedBy,
    DateTimeOffset? UpdatedAt,
    string?         UpdatedBy
);
