using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories.Identity;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseApp.Infrastructure.Repositories.Identity;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>.
/// All queries automatically exclude soft-deleted users via the global query filter.
/// </summary>
public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        await db.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        await db.Users
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public async Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        await db.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(
                    u => u.RefreshTokens.Any(t => t.TokenHash == tokenHash), ct);

    public async Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public async Task<PagedList<User>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking().OrderBy(u => u.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedList<User>.Create(items, total, page, pageSize);
    }

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);
}
