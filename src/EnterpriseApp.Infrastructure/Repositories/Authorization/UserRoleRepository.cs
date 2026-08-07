using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Repositories.Authorization;

public sealed class UserRoleRepository(AppDbContext db) : IUserRoleRepository
{
    public async Task<IReadOnlyList<UserRole>> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        await db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .ToListAsync(ct);

    public async Task<IReadOnlyList<UserRole>> GetByRoleIdAsync(RoleId roleId, CancellationToken ct = default) =>
        await db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.RoleId == roleId)
                .ToListAsync(ct);

    public Task<UserRole?> GetAsync(string userId, RoleId roleId, CancellationToken ct = default) =>
        db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);

    public Task<bool> ExistsAsync(string userId, RoleId roleId, CancellationToken ct = default) =>
        db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);

    public async Task AddAsync(UserRole userRole, CancellationToken ct = default) =>
        await db.UserRoles.AddAsync(userRole, ct);

    public void Remove(UserRole userRole) => db.UserRoles.Remove(userRole);
}
