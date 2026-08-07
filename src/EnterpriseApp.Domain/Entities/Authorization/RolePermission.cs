using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Domain.Entities.Authorization;

/// <summary>
/// Join entity between <see cref="Role"/> and <see cref="Permission"/>.
/// Carries audit metadata (who granted the permission, when, optional expiration).
/// </summary>
public sealed class RolePermission : BaseEntity
{
    private RolePermission() { }

    internal static RolePermission Create(
        RoleId          roleId,
        PermissionId    permissionId,
        string          grantedBy,
        DateTimeOffset? expiresAt) =>
        new()
        {
            Id            = Guid.NewGuid(),
            RoleId        = roleId,
            PermissionId  = permissionId,
            GrantedBy     = grantedBy,
            GrantedAt     = DateTimeOffset.UtcNow,
            ExpiresAt     = expiresAt,
        };

    public Guid           Id            { get; private set; }
    public RoleId         RoleId        { get; private set; } = default!;
    public PermissionId   PermissionId  { get; private set; } = default!;
    public string         GrantedBy     { get; private set; } = string.Empty;
    public DateTimeOffset GrantedAt     { get; private set; }
    public DateTimeOffset? ExpiresAt    { get; private set; }

    public bool IsExpired(DateTimeOffset now) =>
        ExpiresAt.HasValue && ExpiresAt.Value <= now;
}
