using EnterpriseApp.Domain.Entities.Authorization;

namespace EnterpriseApp.Domain.Interfaces.Repositories.Authorization;

/// <summary>
/// Append-only repository for <see cref="PermissionAuditLog"/>.
/// <para><b>By contract</b> only exposes <c>AddAsync</c> and queries — no Update or Remove.
/// See <c>.claude/rules/audit-logging.md §3</c>.</para>
/// </summary>
public interface IPermissionAuditLogRepository
{
    Task AddAsync(PermissionAuditLog log, CancellationToken ct = default);

    Task<IReadOnlyList<PermissionAuditLog>> QueryAsync(
        Guid?           tenantId,
        DateTimeOffset  from,
        DateTimeOffset  to,
        string?         performedBy,
        string?         targetUserId,
        int             page,
        int             pageSize,
        CancellationToken ct = default);

    Task<int> CountAsync(
        Guid?           tenantId,
        DateTimeOffset  from,
        DateTimeOffset  to,
        string?         performedBy,
        string?         targetUserId,
        CancellationToken ct = default);
}
