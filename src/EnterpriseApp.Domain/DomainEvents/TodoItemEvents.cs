using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities;

namespace EnterpriseApp.Domain.DomainEvents;

public sealed record TodoItemCreatedEvent(
    TodoId TodoId,
    string Title,
    string? CreatedByUserId) : DomainEvent;

public sealed record TodoItemCompletedEvent(
    TodoId TodoId,
    string Title,
    string? CompletedByUserId) : DomainEvent;

public sealed record TodoItemDeletedEvent(
    TodoId TodoId,
    string? DeletedByUserId) : DomainEvent;
