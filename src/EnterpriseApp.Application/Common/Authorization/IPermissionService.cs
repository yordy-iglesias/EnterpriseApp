namespace EnterpriseApp.Application.Common.Authorization;

/// <summary>
/// Resolves the effective permission set of a user — combining:
/// <list type="bullet">
///   <item>Direct role assignments (<c>UserRole</c>)</item>
///   <item>Role hierarchy (<c>ParentRoleId</c>) inheritance</item>
///   <item>Wildcard <c>*</c> for super-admin</item>
///   <item>Tenant scoping</item>
/// </list>
/// <para>Implementations should cache the result in Redis with key
/// <c>perms:user:{userId}:tenant:{tenantId}</c>, invalidated on
/// <c>UserPermissionsChanged</c> domain events.</para>
/// </summary>
public interface IPermissionService
{
    /// <summary>Returns the resolved set of permission codes for a user.</summary>
    Task<IReadOnlySet<string>> GetPermissionsForUserAsync(
        string userId,
        Guid?  tenantId,
        CancellationToken ct = default);

    /// <summary>True iff the user holds <paramref name="code"/> directly or via wildcard.</summary>
    Task<bool> HasPermissionAsync(
        string userId,
        Guid?  tenantId,
        string code,
        CancellationToken ct = default);

    /// <summary>
    /// Generates the signed JWT used as the role's <c>PermissionsSnapshot</c>.
    /// Honours the legacy NHCS pattern of caching a JWT-encoded permission list,
    /// but in a dedicated field — not <c>ConcurrencyStamp</c>.
    /// </summary>
    string SignRoleSnapshot(IEnumerable<string> permissionCodes, string roleName);

    /// <summary>Computes a stable version hash of the permission set — published as the JWT <c>psv</c> claim.</summary>
    string ComputeSnapshotVersion(IEnumerable<string> permissionCodes);
}
