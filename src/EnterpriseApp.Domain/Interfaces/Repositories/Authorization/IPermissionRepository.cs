using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.ValueObjects;

namespace EnterpriseApp.Domain.Interfaces.Repositories.Authorization;

/// <summary>Repository for catalog of permissions. Permissions are reference data — read-mostly.</summary>
public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(PermissionId id, CancellationToken ct = default);
    Task<Permission?> GetByCodeAsync(PermissionCode code, Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetAllAsync(Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByCodesAsync(IReadOnlyCollection<string> codes, Guid? tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(PermissionCode code, Guid? tenantId, CancellationToken ct = default);

    Task AddAsync(Permission permission, CancellationToken ct = default);
    void Update(Permission permission);
    void Remove(Permission permission);
}
