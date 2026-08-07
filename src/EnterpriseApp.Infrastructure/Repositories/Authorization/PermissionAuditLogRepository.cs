using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Repositories.Authorization;

/// <summary>
/// Append-only repository — does NOT expose Update or Remove. The DB-level
/// trigger created in the migration enforces immutability even if the repo
/// is bypassed.
/// </summary>
public sealed class PermissionAuditLogRepository(AppDbContext db) : IPermissionAuditLogRepository
{
    public async Task AddAsync(PermissionAuditLog log, CancellationToken ct = default) =>
        await db.PermissionAuditLogs.AddAsync(log, ct);

    public async Task<IReadOnlyList<PermissionAuditLog>> QueryAsync(
        Guid?           tenantId,
        DateTimeOffset  from,
        DateTimeOffset  to,
        string?         performedBy,
        string?         targetUserId,
        int             page,
        int             pageSize,
        CancellationToken ct = default)
    {
        var q = BuildQuery(tenantId, from, to, performedBy, targetUserId);
        return await q.OrderByDescending(l => l.PerformedAt)
                      .Skip((page - 1) * pageSize)
                      .Take(pageSize)
                      .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Guid?           tenantId,
        DateTimeOffset  from,
        DateTimeOffset  to,
        string?         performedBy,
        string?         targetUserId,
        CancellationToken ct = default) =>
        await BuildQuery(tenantId, from, to, performedBy, targetUserId).CountAsync(ct);

    private IQueryable<PermissionAuditLog> BuildQuery(
        Guid? tenantId, DateTimeOffset from, DateTimeOffset to,
        string? performedBy, string? targetUserId)
    {
        var q = db.PermissionAuditLogs.AsNoTracking()
                  .Where(l => l.PerformedAt >= from && l.PerformedAt < to);

        if (tenantId is not null)     q = q.Where(l => l.TenantId == tenantId);
        if (performedBy is not null)  q = q.Where(l => l.PerformedBy == performedBy);
        if (targetUserId is not null) q = q.Where(l => l.TargetUserId == targetUserId);
        return q;
    }
}
