using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Application.Common.Errors;

/// <summary>Catalogue of authorization-related errors used by Application handlers.</summary>
public static class AuthorizationErrors
{
    public static readonly Error RoleNotFound        = new("Role.NotFound",        "Role not found.");
    public static readonly Error RoleAlreadyExists   = new("Role.AlreadyExists",   "A role with that name already exists in this tenant.");
    public static readonly Error RoleIsSystem        = new("Role.IsSystem",        "System roles cannot be modified or deleted.");
    public static readonly Error PermissionNotFound  = new("Permission.NotFound",  "Permission not found.");
    public static readonly Error PermissionExists    = new("Permission.AlreadyExists", "Permission already exists.");
    public static readonly Error AlreadyGranted      = new("Permission.AlreadyGranted", "Role already has this permission.");
    public static readonly Error NotGranted          = new("Permission.NotGranted",     "Role does not have this permission.");
    public static readonly Error UserAlreadyHasRole  = new("UserRole.AlreadyAssigned",  "User already has this role.");
    public static readonly Error UserDoesNotHaveRole = new("UserRole.NotAssigned",      "User does not have this role.");
    public static readonly Error TenantMismatch      = new("Authorization.TenantMismatch", "Cross-tenant operation not allowed.");
    public static readonly Error StepUpRequired      = new("Authorization.StepUpRequired", "This action requires a recent MFA verification.");
}
