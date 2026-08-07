using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITodoRepository"/>.
/// All queries automatically exclude soft-deleted rows via the global query filter.
/// </summary>
public sealed class TodoRepository(AppDbContext db) : ITodoRepository
{
    public async Task<TodoItem?> GetByIdAsync(TodoId id, CancellationToken ct = default) =>
        await db.TodoItems.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default) =>
        await db.TodoItems
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(ct);

    public async Task<PagedList<TodoItem>> GetPagedAsync(
        int           page,
        int           pageSize,
        TodoStatus?   status,
        TodoPriority? priority,
        CancellationToken ct = default)
    {
        var query = db.TodoItems.AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        query = query.OrderByDescending(t => t.CreatedAt);

        // Execute both async to avoid blocking the thread pool.
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedList<TodoItem>.Create(items, total, page, pageSize);
    }

    public async Task AddAsync(TodoItem todo, CancellationToken ct = default) =>
        await db.TodoItems.AddAsync(todo, ct);

    public void Update(TodoItem todo) =>
        db.TodoItems.Update(todo);

    public void Remove(TodoItem todo) =>
        db.TodoItems.Remove(todo);

    public async Task<bool> ExistsAsync(TodoId id, CancellationToken ct = default) =>
        await db.TodoItems.AnyAsync(t => t.Id == id, ct);
}
