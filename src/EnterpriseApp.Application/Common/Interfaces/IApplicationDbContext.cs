using EnterpriseApp.Domain.Entities;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;


namespace EnterpriseApp.Application.Common.Interfaces;

/// <summary>
/// Database context abstraction — Application references this interface only.
/// Keeps Application free of EF Core; Infrastructure provides the concrete type.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<TodoItem> TodoItems { get; }

    // ── Authorization aggregates ───────────────────────────────────────────────
    DbSet<Permission>         Permissions         { get; }
    DbSet<Role>               Roles               { get; }
    DbSet<RolePermission>     RolePermissions     { get; }
    DbSet<UserRole>           UserRoles           { get; }
    DbSet<PermissionAuditLog> PermissionAuditLogs { get; }

    // ── Identity ──────────────────────────────────────────────────────────────
    DbSet<User>         Users         { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
