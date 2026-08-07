using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Domain.ValueObjects;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Repositories.Authorization;

public sealed class PermissionRepository(AppDbContext db) : IPermissionRepository
{
    public Task<Permission?> GetByIdAsync(PermissionId id, CancellationToken ct = default) =>
        db.Permissions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Permission?> GetByCodeAsync(PermissionCode code, Guid? tenantId, CancellationToken ct = default)
    {
        var raw = code.Value;
        return db.Permissions.FirstOrDefaultAsync(
            p => EF.Property<string>(p, "Code") == raw && p.TenantId == tenantId, ct);
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync(Guid? tenantId, CancellationToken ct = default) =>
        await db.Permissions
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .OrderBy(p => EF.Property<string>(p, "Code"))
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Permission>> GetByCodesAsync(
        IReadOnlyCollection<string> codes,
        Guid?                       tenantId,
        CancellationToken           ct = default)
    {
        if (codes.Count == 0) return [];

        // Compare using EF.Property to bypass the value-object converter inside the SQL translator.
        return await db.Permissions
                       .AsNoTracking()
                       .Where(p => p.TenantId == tenantId &&
                                   codes.Contains(EF.Property<string>(p, "Code")))
                       .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(PermissionCode code, Guid? tenantId, CancellationToken ct = default)
    {
        var raw = code.Value;
        return db.Permissions.AnyAsync(
            p => EF.Property<string>(p, "Code") == raw && p.TenantId == tenantId, ct);
    }

    public async Task AddAsync(Permission permission, CancellationToken ct = default) =>
        await db.Permissions.AddAsync(permission, ct);

    public void Update(Permission permission) => db.Permissions.Update(permission);
    public void Remove(Permission permission) => db.Permissions.Remove(permission);
}
