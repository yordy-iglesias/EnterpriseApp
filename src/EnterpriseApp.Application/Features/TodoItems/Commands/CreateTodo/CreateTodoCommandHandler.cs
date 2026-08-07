using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Commands.CreateTodo;

/// <summary>
/// Creates the domain entity, persists it via the Unit of Work,
/// then returns the new todo's Guid to the API layer.
/// Invalidates the paged-list cache so the next list query reflects the new item.
/// </summary>
public sealed class CreateTodoCommandHandler(
    IUnitOfWork         unitOfWork,
    ICurrentUserService currentUser,
    ICacheService       cache)
    : IRequestHandler<CreateTodoCommand, Guid>
{
    public async Task<Guid> Handle(CreateTodoCommand cmd, CancellationToken ct)
    {
        var todo = TodoItem.Create(
            cmd.Title,
            cmd.Description,
            cmd.Priority,
            cmd.DueDate,
            currentUser.UserId);

        await unitOfWork.Todos.AddAsync(todo, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // A new item affects all paged-list results → invalidate by prefix.
        await cache.RemoveByPrefixAsync("todo:paged:", ct);

        return todo.Id.Value;
    }
}
