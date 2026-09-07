using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Identity;
using EnterpriseApp.Infrastructure.Persistence;

namespace EnterpriseApp.Infrastructure.Repositories;

/// <summary>
/// Coordinates all repository writes within a single EF Core transaction.
/// Domain events are dispatched after SaveChangesAsync via
/// <see cref="Interceptors.DomainEventDispatcherInterceptor"/>.
/// </summary>
public sealed class UnitOfWork(
    AppDbContext                   db,
    ITodoRepository                todos,
    IRoleRepository                roles,
    IPermissionRepository          permissions,
    IUserRoleRepository            userRoles,
    IPermissionAuditLogRepository  permissionAuditLogs,
    IUserRepository                users)
    : IUnitOfWork
{
    public ITodoRepository                Todos               => todos;
    public IRoleRepository                Roles               => roles;
    public IPermissionRepository          Permissions         => permissions;
    public IUserRoleRepository            UserRoles           => userRoles;
    public IPermissionAuditLogRepository  PermissionAuditLogs => permissionAuditLogs;
    public IUserRepository                Users               => users;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
