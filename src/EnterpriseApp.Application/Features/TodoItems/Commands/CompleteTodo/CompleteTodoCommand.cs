using MediatR;

namespace EnterpriseApp.Application.Features.TodoItems.Commands.CompleteTodo;

/// <summary>Marks a TodoItem as Completed.</summary>
public sealed record CompleteTodoCommand(Guid Id) : IRequest;
