using AutoMapper;
using EnterpriseApp.Application.Features.TodoItems.DTOs;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Queries.GetPagedTodos;

public sealed class GetPagedTodosQueryHandler(
    IUnitOfWork unitOfWork,
    IMapper     mapper)
    : IRequestHandler<GetPagedTodosQuery, PagedTodosDto>
{
    public async Task<PagedTodosDto> Handle(GetPagedTodosQuery query, CancellationToken ct)
    {
        var paged = await unitOfWork.Todos.GetPagedAsync(
            query.Page,
            query.PageSize,
            query.Status,
            query.Priority,
            ct);

        return mapper.Map<PagedTodosDto>(paged);
    }
}
