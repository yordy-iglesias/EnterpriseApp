using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.DomainEvents;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.ValueObjects;

namespace EnterpriseApp.Domain.Entities;

/// <summary>
/// TodoItem — the canonical example aggregate for this template.
/// Replace / complement with your domain's aggregates.
/// </summary>
public sealed class TodoItem : AuditableEntity<TodoId>
{
    // ── Private constructor for EF Core ─────────────────────────────────────
    private TodoItem() { }                           // EF Core
    private TodoItem(TodoId id) : base(id) { }      // factory path

    // ── Factory method ───────────────────────────────────────────────────────
    public static TodoItem Create(
        string          title,
        string?         description,
        TodoPriority    priority,
        DateTimeOffset? dueDate,
        string?         createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var todo = new TodoItem(TodoId.New())
        {
            Title       = title.Trim(),
            Description = description?.Trim(),
            Priority    = priority,
            Status      = TodoStatus.Pending,
            DueDate     = dueDate,
            CreatedBy   = createdByUserId,
            CreatedAt   = DateTimeOffset.UtcNow,
        };

        todo.RaiseDomainEvent(new TodoItemCreatedEvent(todo.Id, todo.Title, createdByUserId));
        return todo;
    }

    // ── Properties ───────────────────────────────────────────────────────────
    public string          Title       { get; private set; } = string.Empty;
    public string?         Description { get; private set; }
    public TodoPriority    Priority    { get; private set; }
    public TodoStatus      Status      { get; private set; }
    public DateTimeOffset? DueDate     { get; private set; }

    // ── Behaviour ────────────────────────────────────────────────────────────
    public void Update(
        string          title,
        string?         description,
        TodoPriority    priority,
        DateTimeOffset? dueDate,
        string?         updatedBy)
    {
        if (Status == TodoStatus.Cancelled)
            throw new DomainException("Cannot update a cancelled todo item.");

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title       = title.Trim();
        Description = description?.Trim();
        Priority    = priority;
        DueDate     = dueDate;
        UpdatedBy   = updatedBy;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void Complete(string? completedByUserId)
    {
        if (Status == TodoStatus.Completed)
            throw new DomainException("Todo item is already completed.");
        if (Status == TodoStatus.Cancelled)
            throw new DomainException("Cannot complete a cancelled todo item.");

        Status    = TodoStatus.Completed;
        UpdatedBy = completedByUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new TodoItemCompletedEvent(Id, Title, completedByUserId));
    }

    public void Cancel(string? cancelledByUserId)
    {
        if (Status == TodoStatus.Completed)
            throw new DomainException("Cannot cancel a completed todo item.");

        Status    = TodoStatus.Cancelled;
        UpdatedBy = cancelledByUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void StartProgress(string? userId)
    {
        if (Status != TodoStatus.Pending)
            throw new DomainException("Only pending items can be started.");

        Status    = TodoStatus.InProgress;
        UpdatedBy = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>Strongly-typed Id for TodoItem. Prevents accidental Id swaps.</summary>
public sealed record TodoId(Guid Value)
{
    public static TodoId New()           => new(Guid.NewGuid());
    public static TodoId From(Guid value) => new(value);
    public override string ToString()    => Value.ToString();
}
