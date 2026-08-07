using EnterpriseApp.Domain.Entities.Authorization;

namespace EnterpriseApp.Domain.Interfaces.Repositories.Authorization;

/// <summary>
/// Aggregate-root repository for <see cref="Role"/>. Returns the role with its
/// <c>RolePermissions</c> collection eagerly loaded for snapshot regeneration.
/// </summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default);
    Task<Role?> GetByNameAsync(string normalizedName, Guid? tenantId, CancellationToken ct = default);
    Task<Role?> GetWithPermissionsAsync(RoleId id, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetByTenantAsync(Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<bool> ExistsAsync(RoleId id, CancellationToken ct = default);

    Task AddAsync(Role role, CancellationToken ct = default);
    void Update(Role role);
    void Remove(Role role);
}
