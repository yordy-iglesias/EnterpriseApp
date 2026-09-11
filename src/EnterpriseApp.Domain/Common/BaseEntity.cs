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
    // Parameterless ctor for EF Core materialization — Id stays default!
    // and is written by EF Core immediately after via the private setter.
    protected AuditableEntity() { }

    // Ctor for factory/domain use — caller supplies the new identity.
    protected AuditableEntity(TId id) => Id = id;

    // private set: EF Core can write this post-construction; init-only cannot.
    public TId Id { get; private set; } = default!;

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
