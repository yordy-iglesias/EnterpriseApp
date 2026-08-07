namespace EnterpriseApp.Domain.Common;

/// <summary>
/// Base class for all domain entities. Holds domain events.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// Entity with strongly-typed Id, soft-delete, and audit support.
/// Implements <see cref="IAuditInfo"/> so Infrastructure interceptors can stamp
/// <c>CreatedAt/By</c> and <c>UpdatedAt/By</c> without referencing the concrete type.
/// </summary>
public abstract class AuditableEntity<TId> : BaseEntity, IAuditInfo where TId : notnull
{
    protected AuditableEntity(TId id) => Id = id;

    public TId Id { get; protected init; }

    // IAuditInfo — setters are public so the interceptor can write them.
    public DateTimeOffset  CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public string?         CreatedBy  { get; set; }
    public DateTimeOffset? UpdatedAt  { get; set; }
    public string?         UpdatedBy  { get; set; }

    // Soft-delete
    public bool            IsDeleted  { get; private set; }
    public DateTimeOffset? DeletedAt  { get; private set; }

    public void SoftDelete(string? userId)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedBy = userId;
    }
}
