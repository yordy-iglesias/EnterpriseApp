using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;

namespace EnterpriseApp.Domain.Interfaces.Repositories.Identity;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default);
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<bool>  ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<PagedList<User>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
