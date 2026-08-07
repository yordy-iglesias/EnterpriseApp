using System.Diagnostics;
using EnterpriseApp.Application.Common;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Domain.DomainEvents.Authorization;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.EventHandlers;

/// <summary>
/// Audit-log handlers — one per authorization-changing domain event.
/// <para><b>Persistence note:</b> domain events are dispatched <i>after</i> the
/// originating <c>SaveChangesAsync</c> (see <c>DomainEventDispatcherInterceptor</c>),
/// so each handler must call <c>SaveChangesAsync</c> on its own. The audit row is
/// committed in a follow-up transaction and is allowed to be eventually-consistent
/// with the business mutation it describes — but never lost.</para>
/// See <c>.claude/rules/audit-logging.md</c>.
/// </summary>
internal static class AuditExtensions
{
    public static string? CurrentTraceId() => Activity.Current?.TraceId.ToString();
}

public sealed class AuditPermissionGrantedHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : INotificationHandler<DomainEventNotification<PermissionGranted>>
{
    public async Task Handle(DomainEventNotification<PermissionGranted> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        await uow.PermissionAuditLogs.AddAsync(PermissionAuditLog.Record(
            action:             PermissionAuditAction.PermissionGranted,
            performedBy:        @event.GrantedBy,
            performedByEmail:   currentUser.Email,
            targetUserId:       null,
            targetRoleId:       @event.RoleId,
            targetPermissionId: @event.PermissionId,
            tenantId:           @event.TenantId,
            ipAddress:          null,
            userAgent:          null,
            correlationId:      AuditExtensions.CurrentTraceId(),
            reason:             null,
            metadataJson:       $"{{\"code\":\"{@event.PermissionCode}\"}}"), ct);
        await uow.SaveChangesAsync(ct);
    }
}

public sealed class AuditPermissionRevokedHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : INotificationHandler<DomainEventNotification<PermissionRevoked>>
{
    public async Task Handle(DomainEventNotification<PermissionRevoked> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        await uow.PermissionAuditLogs.AddAsync(PermissionAuditLog.Record(
            action:             PermissionAuditAction.PermissionRevoked,
            performedBy:        @event.RevokedBy,
            performedByEmail:   currentUser.Email,
            targetUserId:       null,
            targetRoleId:       @event.RoleId,
            targetPermissionId: @event.PermissionId,
            tenantId:           @event.TenantId,
            ipAddress:          null,
            userAgent:          null,
            correlationId:      AuditExtensions.CurrentTraceId(),
            reason:             null,
            metadataJson:       null), ct);
        await uow.SaveChangesAsync(ct);
    }
}

public sealed class AuditUserRoleAssignedHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : INotificationHandler<DomainEventNotification<UserRoleAssigned>>
{
    public async Task Handle(DomainEventNotification<UserRoleAssigned> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        await uow.PermissionAuditLogs.AddAsync(PermissionAuditLog.Record(
            action:             PermissionAuditAction.UserRoleAssigned,
            performedBy:        @event.AssignedBy,
            performedByEmail:   currentUser.Email,
            targetUserId:       @event.UserId,
            targetRoleId:       @event.RoleId,
            targetPermissionId: null,
            tenantId:           @event.TenantId,
            ipAddress:          null,
            userAgent:          null,
            correlationId:      AuditExtensions.CurrentTraceId(),
            reason:             null,
            metadataJson:       null), ct);
        await uow.SaveChangesAsync(ct);
    }
}

public sealed class AuditUserRoleUnassignedHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : INotificationHandler<DomainEventNotification<UserRoleUnassigned>>
{
    public async Task Handle(DomainEventNotification<UserRoleUnassigned> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        await uow.PermissionAuditLogs.AddAsync(PermissionAuditLog.Record(
            action:             PermissionAuditAction.UserRoleUnassigned,
            performedBy:        @event.UnassignedBy,
            performedByEmail:   currentUser.Email,
            targetUserId:       @event.UserId,
            targetRoleId:       @event.RoleId,
            targetPermissionId: null,
            tenantId:           @event.TenantId,
            ipAddress:          null,
            userAgent:          null,
            correlationId:      AuditExtensions.CurrentTraceId(),
            reason:             null,
            metadataJson:       null), ct);
        await uow.SaveChangesAsync(ct);
    }
}

public sealed class AuditRoleCreatedHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : INotificationHandler<DomainEventNotification<RoleCreated>>
{
    public async Task Handle(DomainEventNotification<RoleCreated> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        await uow.PermissionAuditLogs.AddAsync(PermissionAuditLog.Record(
            action:             PermissionAuditAction.RoleCreated,
            performedBy:        @event.CreatedBy ?? "system",
            performedByEmail:   currentUser.Email,
            targetUserId:       null,
            targetRoleId:       @event.RoleId,
            targetPermissionId: null,
            tenantId:           @event.TenantId,
            ipAddress:          null,
            userAgent:          null,
            correlationId:      AuditExtensions.CurrentTraceId(),
            reason:             null,
            metadataJson:       $"{{\"name\":\"{@event.Name}\"}}"), ct);
        await uow.SaveChangesAsync(ct);
    }
}

public sealed class AuditRoleDeletedHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : INotificationHandler<DomainEventNotification<RoleDeleted>>
{
    public async Task Handle(DomainEventNotification<RoleDeleted> notification, CancellationToken ct)
    {
        var @event = notification.Event;
        await uow.PermissionAuditLogs.AddAsync(PermissionAuditLog.Record(
            action:             PermissionAuditAction.RoleDeleted,
            performedBy:        @event.DeletedBy ?? "system",
            performedByEmail:   currentUser.Email,
            targetUserId:       null,
            targetRoleId:       @event.RoleId,
            targetPermissionId: null,
            tenantId:           null,
            ipAddress:          null,
            userAgent:          null,
            correlationId:      AuditExtensions.CurrentTraceId(),
            reason:             null,
            metadataJson:       $"{{\"name\":\"{@event.Name}\"}}"), ct);
        await uow.SaveChangesAsync(ct);
    }
}
