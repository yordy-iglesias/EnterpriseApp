using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Repositories.Authorization;

public sealed class RoleRepository(AppDbContext db) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default) =>
        db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Role?> GetByNameAsync(string normalizedName, Guid? tenantId, CancellationToken ct = default) =>
        db.Roles.FirstOrDefaultAsync(
            r => r.NormalizedName == normalizedName && r.TenantId == tenantId, ct);

    public Task<Role?> GetWithPermissionsAsync(RoleId id, CancellationToken ct = default) =>
        db.Roles
          .Include(r => r.RolePermissions)
          .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Role>> GetByTenantAsync(Guid? tenantId, CancellationToken ct = default) =>
        await db.Roles
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Role>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var roleIds = db.Set<UserRole>()
                        .Where(ur => ur.UserId == userId)
                        .Select(ur => ur.RoleId);

        return await db.Roles
                       .AsNoTracking()
                       .Where(r => roleIds.Contains(r.Id))
                       .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesForUserAsync(
        string userId, Guid? tenantId, CancellationToken ct = default)
    {
        return await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(RoleId id, CancellationToken ct = default) =>
        db.Roles.AnyAsync(r => r.Id == id, ct);

    public async Task AddAsync(Role role, CancellationToken ct = default) =>
        await db.Roles.AddAsync(role, ct);

    public void Update(Role role) => db.Roles.Update(role);
    public void Remove(Role role) => db.Roles.Remove(role);
}
