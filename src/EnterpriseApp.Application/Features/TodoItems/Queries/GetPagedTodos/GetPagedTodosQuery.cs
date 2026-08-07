using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.TodoItems.DTOs;
using EnterpriseApp.Domain.ValueObjects;

namespace EnterpriseApp.Application.Features.TodoItems.Queries.GetPagedTodos;

/// <summary>
/// Returns a paged, optionally filtered list of todos.
/// <para>Defaults: page = 1, pageSize = 20, no filters.</para>
/// Implements <see cref="ICachedQuery{TResponse}"/> so the pipeline
/// caches the result automatically for 2 minutes.
/// Cache key encodes all filter parameters to avoid stale cross-filter hits.
/// </summary>
public sealed record GetPagedTodosQuery(
    int           Page     = 1,
    int           PageSize = 20,
    TodoStatus?   Status   = null,
    TodoPriority? Priority = null
) : ICachedQuery<PagedTodosDto>
{
    public string    CacheKey   => $"todo:paged:{Page}:{PageSize}:{Status}:{Priority}";
    public TimeSpan? Expiration => TimeSpan.FromMinutes(2);
}
