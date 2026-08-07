using AutoMapper;
using EnterpriseApp.Application.Features.TodoItems.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities;

namespace EnterpriseApp.Application.Common.Mappings;

/// <summary>
/// AutoMapper profile for the TodoItem aggregate.
/// Keep mapping logic here — handlers should never contain manual property assignment.
/// </summary>
public sealed class TodoItemMappingProfile : Profile
{
    public TodoItemMappingProfile()
    {
        CreateMap<TodoItem, TodoItemDto>()
            .ConstructUsing(src => new TodoItemDto(
                src.Id.Value,
                src.Title,
                src.Description,
                src.Status,
                src.Priority,
                src.DueDate,
                src.CreatedAt,
                src.CreatedBy,
                src.UpdatedAt,
                src.UpdatedBy));

        // PagedList<TodoItem> → PagedTodosDto
        // Note: AutoMapper ConstructUsing is used so we can call Map<> for the items list.
        CreateMap<PagedList<TodoItem>, PagedTodosDto>()
            .ConstructUsing((src, ctx) => new PagedTodosDto(
                Items:          ctx.Mapper.Map<IReadOnlyList<TodoItemDto>>(src.Items),
                TotalCount:     src.TotalCount,
                Page:           src.Page,
                PageSize:       src.PageSize,
                TotalPages:     src.TotalPages,
                HasPreviousPage: src.HasPreviousPage,
                HasNextPage:    src.HasNextPage));
    }
}
