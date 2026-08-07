using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Common;

/// <summary>
/// Wraps a domain event (which lives in the dependency-free Domain layer) as a
/// MediatR <see cref="INotification"/> so that Application handlers can subscribe
/// to it without forcing Domain to reference MediatR.
/// <para>
/// The Infrastructure dispatcher creates instances via reflection:
/// <c>DomainEventNotification&lt;TEvent&gt;</c> where <c>TEvent</c> is the concrete
/// domain event type, ensuring MediatR resolves the correct typed handlers.
/// </para>
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : IDomainEvent;
