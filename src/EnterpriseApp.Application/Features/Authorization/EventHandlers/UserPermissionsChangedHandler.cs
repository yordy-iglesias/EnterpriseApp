using EnterpriseApp.Application.Common;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.DomainEvents.Authorization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Application.Features.Authorization.EventHandlers;

/// <summary>
/// Invalidates the user's resolved-permissions cache whenever something changes
/// (new role assignment, removed role, role's permissions modified). The next
/// authorization check will repopulate the cache from the DB.
/// </summary>
public sealed class UserPermissionsChangedHandler(
    ICacheService cache,
    ILogger<UserPermissionsChangedHandler> logger)
    : INotificationHandler<DomainEventNotification<UserPermissionsChanged>>
{
    public async Task Handle(DomainEventNotification<UserPermissionsChanged> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        var key = AuthorizationCacheKeys.UserPermissions(@event.UserId, @event.TenantId);
        await cache.RemoveAsync(key, ct);

        logger.LogInformation(
            "Permissions cache evicted for user {UserId} tenant {TenantId}",
            @event.UserId, @event.TenantId);
    }
}
