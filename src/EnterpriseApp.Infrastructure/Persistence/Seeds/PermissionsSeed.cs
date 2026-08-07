using EnterpriseApp.Domain.Authorization;
using EnterpriseApp.Domain.Entities.Authorization;

namespace EnterpriseApp.Infrastructure.Persistence.Seeds;

/// <summary>
/// Initial set of permissions and system roles. Loaded at startup by
/// <see cref="DatabaseSeeder"/>. To add a new permission, prefer the
/// <c>/generate-permission</c> command — see <c>.claude/commands/generate-permission.md</c>.
/// </summary>
public static class PermissionsSeed
{
    public sealed record SeedItem(
        string  Code,
        string  DisplayName,
        string? Description,
        bool    IsSensitive,
        IReadOnlyList<string> DefaultRoles);

    public static IReadOnlyList<SeedItem> Items { get; } =
    [
        // ── Roles ────────────────────────────────────────────────────────────
        new(PermissionCodes.Role.View,   "View roles",                 "List and inspect roles",                false, ["tenant-admin", "auditor"]),
        new(PermissionCodes.Role.Create, "Create roles",               null,                                    false, ["tenant-admin"]),
        new(PermissionCodes.Role.Update, "Update roles",               null,                                    false, ["tenant-admin"]),
        new(PermissionCodes.Role.Delete, "Delete roles",               null,                                    false, ["tenant-admin"]),
        new(PermissionCodes.Role.Assign, "Assign role to user",        "Sensitive — requires step-up MFA",      true,  ["tenant-admin"]),
        new(PermissionCodes.Role.Revoke, "Revoke role from user",      "Sensitive — requires step-up MFA",      true,  ["tenant-admin"]),

        // ── Permissions ──────────────────────────────────────────────────────
        new(PermissionCodes.Permission.View,   "View permissions",     null,                                    false, ["tenant-admin", "auditor"]),
        new(PermissionCodes.Permission.Grant,  "Grant permission",     "Sensitive — requires step-up MFA",      true,  ["tenant-admin"]),
        new(PermissionCodes.Permission.Revoke, "Revoke permission",    "Sensitive — requires step-up MFA",      true,  ["tenant-admin"]),

        // ── Users ────────────────────────────────────────────────────────────
        new(PermissionCodes.User.View,       "View users",             null, false, ["tenant-admin", "auditor"]),
        new(PermissionCodes.User.ViewOwn,    "View own profile",       null, false, ["user"]),
        new(PermissionCodes.User.Create,     "Create users",           null, false, ["tenant-admin"]),
        new(PermissionCodes.User.Update,     "Update users",           null, false, ["tenant-admin"]),
        new(PermissionCodes.User.Delete,     "Delete users",           "Soft delete — sensitive", true, ["tenant-admin"]),
        new(PermissionCodes.User.Activate,   "Activate user account",  null, false, ["tenant-admin"]),
        new(PermissionCodes.User.Deactivate, "Deactivate user account", null, false, ["tenant-admin"]),

        // ── Audit ────────────────────────────────────────────────────────────
        new(PermissionCodes.Audit.Read,   "Read audit logs",   "Sensitive — every read is also audited", true, ["auditor"]),
        new(PermissionCodes.Audit.Export, "Export audit logs", "Sensitive — requires step-up MFA",       true, ["auditor"]),
    ];

    /// <summary>
    /// System-level roles — never deleted, applied to every tenant.
    /// </summary>
    public sealed record RoleSeed(string Name, string Description, string[]? PermissionCodes = null);

    public static IReadOnlyList<RoleSeed> SystemRoles { get; } =
    [
        new("super-admin",   "Full cross-tenant access (wildcard).", [PermissionCodes.Wildcard]),
        new("tenant-admin",  "Full access within a tenant."),
        new("auditor",       "Read-only access plus audit log access."),
        new("user",          "Standard authenticated user — no privileges by default."),
    ];
}
