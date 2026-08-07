namespace EnterpriseApp.Domain.Common;

/// <summary>Marker interface for all domain events.</summary>
public interface IDomainEvent
{
    Guid EventId    { get; }
    DateTime OccurredAt { get; }
}

/// <summary>Base record for domain events — use as base for concrete events.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId     { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
