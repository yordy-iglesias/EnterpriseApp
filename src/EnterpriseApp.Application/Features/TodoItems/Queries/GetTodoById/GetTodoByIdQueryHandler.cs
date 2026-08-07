using AutoMapper;
using EnterpriseApp.Application.Features.TodoItems.DTOs;
using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Queries.GetTodoById;

public sealed class GetTodoByIdQueryHandler(
    IUnitOfWork unitOfWork,
    IMapper     mapper)
    : IRequestHandler<GetTodoByIdQuery, TodoItemDto>
{
    public async Task<TodoItemDto> Handle(GetTodoByIdQuery query, CancellationToken ct)
    {
        var id   = TodoId.From(query.Id);
        var todo = await unitOfWork.Todos.GetByIdAsync(id, ct)
                   ?? throw new NotFoundException(nameof(Domain.Entities.TodoItem), query.Id);

        return mapper.Map<TodoItemDto>(todo);
    }
}
