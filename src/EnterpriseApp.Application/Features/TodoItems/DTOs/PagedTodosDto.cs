namespace EnterpriseApp.Application.Features.TodoItems.DTOs;

/// <summary>Paginated response envelope for todo list queries.</summary>
public sealed record PagedTodosDto(
    IReadOnlyList<TodoItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage
);
