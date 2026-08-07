using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Commands.CompleteTodo;

/// <summary>
/// Invalidates the cached entry for the specific todo and all paged-list results.
/// </summary>
public sealed class CompleteTodoCommandHandler(
    IUnitOfWork         unitOfWork,
    ICurrentUserService currentUser,
    ICacheService       cache)
    : IRequestHandler<CompleteTodoCommand>
{
    public async Task Handle(CompleteTodoCommand cmd, CancellationToken ct)
    {
        var id   = TodoId.From(cmd.Id);
        var todo = await unitOfWork.Todos.GetByIdAsync(id, ct)
                   ?? throw new NotFoundException(nameof(Domain.Entities.TodoItem), cmd.Id);

        // Domain method enforces invariant: cannot complete an already-completed/cancelled item.
        todo.Complete(currentUser.UserId);

        unitOfWork.Todos.Update(todo);
        await unitOfWork.SaveChangesAsync(ct);

        // State changed → invalidate the specific item and all paged lists.
        await cache.RemoveAsync($"todo:id:{cmd.Id}", ct);
        await cache.RemoveByPrefixAsync("todo:paged:", ct);
    }
}
