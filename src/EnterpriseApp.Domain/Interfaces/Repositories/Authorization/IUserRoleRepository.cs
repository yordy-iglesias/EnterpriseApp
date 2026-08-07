using EnterpriseApp.Domain.Entities.Authorization;

namespace EnterpriseApp.Domain.Interfaces.Repositories.Authorization;

/// <summary>Repository for the <see cref="UserRole"/> assignment join entity.</summary>
public interface IUserRoleRepository
{
    Task<IReadOnlyList<UserRole>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserRole>> GetByRoleIdAsync(RoleId roleId, CancellationToken ct = default);
    Task<UserRole?> GetAsync(string userId, RoleId roleId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string userId, RoleId roleId, CancellationToken ct = default);

    Task AddAsync(UserRole userRole, CancellationToken ct = default);
    void Remove(UserRole userRole);
}
