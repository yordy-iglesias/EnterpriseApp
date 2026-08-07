using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Commands.DeleteTodo;

/// <summary>Soft-deletes a TodoItem by its Guid.</summary>
public sealed record DeleteTodoCommand(Guid Id) : IRequest;
