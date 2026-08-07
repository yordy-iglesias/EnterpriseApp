using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.DomainEvents.Authorization;

namespace EnterpriseApp.Domain.Entities.Authorization;

/// <summary>
/// Assignment of a <see cref="Role"/> to a user. Decoupled from ASP.NET Core Identity
/// so the template stays usable with any user store. The user is referenced by its
/// string Id (Identity's <c>UserId</c>) to remain compatible with both <c>Guid</c>
/// and <c>string</c> primary keys.
/// </summary>
public sealed class UserRole : BaseEntity
{
    private UserRole() { }

    public static UserRole Create(string userId, RoleId roleId, Guid? tenantId, string assignedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(roleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedBy);

        var ur = new UserRole
        {
            Id         = Guid.NewGuid(),
            UserId     = userId,
            RoleId     = roleId,
            TenantId   = tenantId,
            AssignedBy = assignedBy,
            AssignedAt = DateTimeOffset.UtcNow,
        };
        ur.RaiseDomainEvent(new UserRoleAssigned(userId, roleId, tenantId, assignedBy));
        ur.RaiseDomainEvent(new UserPermissionsChanged(userId, tenantId));
        return ur;
    }

    public Guid           Id         { get; private set; }
    public string         UserId     { get; private set; } = string.Empty;
    public RoleId         RoleId     { get; private set; } = default!;
    public Guid?          TenantId   { get; private set; }
    public string         AssignedBy { get; private set; } = string.Empty;
    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>Marks the assignment as removed and emits the events necessary
    /// to invalidate the user's permission cache.</summary>
    public IEnumerable<IDomainEvent> Unassign(string unassignedBy) =>
    [
        new UserRoleUnassigned(UserId, RoleId, TenantId, unassignedBy),
        new UserPermissionsChanged(UserId, TenantId),
    ];
}
