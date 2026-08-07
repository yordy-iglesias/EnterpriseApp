using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Authorization;

namespace EnterpriseApp.Domain.DomainEvents.Authorization;

/// <summary>Raised when a new role is created.</summary>
public sealed record RoleCreated(
    RoleId  RoleId,
    string  Name,
    Guid?   TenantId,
    string? CreatedBy) : DomainEvent;

/// <summary>Raised when a role is soft-deleted.</summary>
public sealed record RoleDeleted(
    RoleId  RoleId,
    string  Name,
    string? DeletedBy) : DomainEvent;

/// <summary>Raised when a permission is granted to a role.</summary>
public sealed record PermissionGranted(
    RoleId       RoleId,
    PermissionId PermissionId,
    string       PermissionCode,
    string       GrantedBy,
    Guid?        TenantId) : DomainEvent;

/// <summary>Raised when a permission is revoked from a role.</summary>
public sealed record PermissionRevoked(
    RoleId       RoleId,
    PermissionId PermissionId,
    string       RevokedBy,
    Guid?        TenantId) : DomainEvent;

/// <summary>
/// Raised when the permission set of a role changes — triggers
/// regeneration of <see cref="Role.PermissionsSnapshot"/> and downstream cache invalidation.
/// </summary>
public sealed record RolePermissionsChanged(
    RoleId RoleId,
    Guid?  TenantId) : DomainEvent;

/// <summary>Raised when a role is assigned to a user.</summary>
public sealed record UserRoleAssigned(
    string UserId,
    RoleId RoleId,
    Guid?  TenantId,
    string AssignedBy) : DomainEvent;

/// <summary>Raised when a role is removed from a user.</summary>
public sealed record UserRoleUnassigned(
    string UserId,
    RoleId RoleId,
    Guid?  TenantId,
    string UnassignedBy) : DomainEvent;

/// <summary>
/// Raised whenever the resolved permission set of a single user could change.
/// Subscribers must invalidate the user's Redis cache and force a token refresh.
/// </summary>
public sealed record UserPermissionsChanged(
    string UserId,
    Guid?  TenantId) : DomainEvent;
