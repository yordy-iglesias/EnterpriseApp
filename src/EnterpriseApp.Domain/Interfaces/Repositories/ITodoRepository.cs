using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.ValueObjects;

namespace EnterpriseApp.Domain.Interfaces.Repositories;

/// <summary>
/// Repository contract for TodoItem — defined in Domain, implemented in Infrastructure.
/// The domain never depends on EF Core or any persistence technology.
/// </summary>
public interface ITodoRepository
{
    Task<TodoItem?> GetByIdAsync(TodoId id, CancellationToken ct = default);
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default);
    Task<PagedList<TodoItem>> GetPagedAsync(int page, int pageSize, TodoStatus? status, TodoPriority? priority, CancellationToken ct = default);
    Task AddAsync(TodoItem todo, CancellationToken ct = default);
    void Update(TodoItem todo);
    void Remove(TodoItem todo);
    Task<bool> ExistsAsync(TodoId id, CancellationToken ct = default);
}
