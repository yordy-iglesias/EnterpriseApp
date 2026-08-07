namespace EnterpriseApp.Application.Common.Authorization;

/// <summary>
/// Cache key conventions for the authorization module.
/// Centralised here so invalidation handlers cannot drift from the producers.
/// </summary>
public static class AuthorizationCacheKeys
{
    public const string UserPermissionsPrefix = "perms:user:";
    public const string RoleSnapshotPrefix    = "perms:role:";

    public static string UserPermissions(string userId, Guid? tenantId) =>
        $"{UserPermissionsPrefix}{userId}:tenant:{tenantId?.ToString() ?? "global"}";

    public static string RoleSnapshot(Guid roleId) =>
        $"{RoleSnapshotPrefix}{roleId}";
}
