using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Commands.UpdateTodo;

/// <summary>
/// Invalidates the cached entry for the specific todo and all paged-list results.
/// </summary>
public sealed class UpdateTodoCommandHandler(
    IUnitOfWork         unitOfWork,
    ICurrentUserService currentUser,
    ICacheService       cache)
    : IRequestHandler<UpdateTodoCommand>
{
    public async Task Handle(UpdateTodoCommand cmd, CancellationToken ct)
    {
        var id   = TodoId.From(cmd.Id);
        var todo = await unitOfWork.Todos.GetByIdAsync(id, ct)
                   ?? throw new NotFoundException(nameof(Domain.Entities.TodoItem), cmd.Id);

        todo.Update(
            cmd.Title,
            cmd.Description,
            cmd.Priority,
            cmd.DueDate,
            currentUser.UserId);

        unitOfWork.Todos.Update(todo);
        await unitOfWork.SaveChangesAsync(ct);

        // Invalidate the specific item and all paged lists.
        await cache.RemoveAsync($"todo:id:{cmd.Id}", ct);
        await cache.RemoveByPrefixAsync("todo:paged:", ct);
    }
}
