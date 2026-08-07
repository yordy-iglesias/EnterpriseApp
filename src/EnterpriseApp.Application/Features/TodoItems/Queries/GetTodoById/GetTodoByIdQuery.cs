using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.TodoItems.DTOs;

namespace EnterpriseApp.Application.Features.TodoItems.Queries.GetTodoById;

/// <summary>
/// Returns a single TodoItem DTO, or throws NotFoundException.
/// Implements <see cref="ICachedQuery{TResponse}"/> so the pipeline
/// caches the result automatically for 5 minutes.
/// Cache key: <c>todo:id:{Id}</c>
/// </summary>
public sealed record GetTodoByIdQuery(Guid Id) : ICachedQuery<TodoItemDto>
{
    public string    CacheKey   => $"todo:id:{Id}";
    public TimeSpan? Expiration => TimeSpan.FromMinutes(5);
}
