using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Identity;

namespace EnterpriseApp.Domain.Interfaces.Repositories;

/// <summary>
/// Unit of Work — coordinates writes across multiple repositories in a single transaction.
/// Dispatches domain events after SaveChangesAsync.
/// </summary>
public interface IUnitOfWork
{
    ITodoRepository Todos { get; }

    // ── Authorization aggregates ───────────────────────────────────────────────
    IRoleRepository                Roles          { get; }
    IPermissionRepository          Permissions    { get; }
    IUserRoleRepository            UserRoles      { get; }
    IPermissionAuditLogRepository  PermissionAuditLogs { get; }

    // ── Identity ──────────────────────────────────────────────────────────────
    IUserRepository Users { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
