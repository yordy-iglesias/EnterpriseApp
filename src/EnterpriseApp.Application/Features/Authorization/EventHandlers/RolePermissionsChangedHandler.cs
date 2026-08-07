using EnterpriseApp.Application.Common;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.DomainEvents.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.EventHandlers;

/// <summary>
/// When a role's permission set changes, evict the role's snapshot cache and
/// fan-out a <see cref="UserPermissionsChanged"/> event for every user holding
/// the role. This guarantees no user is left with a stale resolved-permissions cache.
/// </summary>
public sealed class RolePermissionsChangedHandler(
    IUnitOfWork   uow,
    ICacheService cache,
    IPublisher    publisher)
    : INotificationHandler<DomainEventNotification<RolePermissionsChanged>>
{
    public async Task Handle(DomainEventNotification<RolePermissionsChanged> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        await cache.RemoveAsync(AuthorizationCacheKeys.RoleSnapshot(@event.RoleId.Value), ct);

        var assignments = await uow.UserRoles.GetByRoleIdAsync(@event.RoleId, ct);
        foreach (var ur in assignments)
        {
            await publisher.Publish(
                new DomainEventNotification<UserPermissionsChanged>(new UserPermissionsChanged(ur.UserId, ur.TenantId)), ct);
        }
    }
}
