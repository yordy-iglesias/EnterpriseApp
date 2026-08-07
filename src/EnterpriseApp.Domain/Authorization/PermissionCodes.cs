namespace EnterpriseApp.Domain.Authorization;

/// <summary>
/// Canonical permission codes — the source of truth for permission strings.
/// <para>Add new permissions here AND in <c>PermissionsSeed.cs</c>.</para>
/// <para>Format: <c>{module}.{action}[.{qualifier}]</c>. See
/// <c>.claude/rules/authorization.md §2</c> and the <c>/generate-permission</c> command.</para>
/// </summary>
public static class PermissionCodes
{
    /// <summary>System-level wildcards (only granted to <c>super-admin</c>).</summary>
    public const string Wildcard = "*";

    public static class Role
    {
        public const string View   = "role.view";
        public const string Create = "role.create";
        public const string Update = "role.update";
        public const string Delete = "role.delete";
        public const string Assign = "role.assign";   // sensitive
        public const string Revoke = "role.revoke";   // sensitive
    }

    public static class Permission
    {
        public const string View   = "permission.view";
        public const string Grant  = "permission.grant";   // sensitive
        public const string Revoke = "permission.revoke";  // sensitive
    }

    public static class User
    {
        public const string View       = "user.view";
        public const string ViewOwn    = "user.view.own";
        public const string Create     = "user.create";
        public const string Update     = "user.update";
        public const string Delete     = "user.delete";
        public const string Activate   = "user.activate";
        public const string Deactivate = "user.deactivate";
    }

    public static class Audit
    {
        public const string Read   = "audit.read";    // sensitive — see audit-logging.md §7
        public const string Export = "audit.export";  // sensitive
    }

    /// <summary>
    /// Returns every permission code as a flat list — used by tests and seed validators.
    /// </summary>
    public static IReadOnlyList<string> All =>
    [
        Role.View, Role.Create, Role.Update, Role.Delete, Role.Assign, Role.Revoke,
        Permission.View, Permission.Grant, Permission.Revoke,
        User.View, User.ViewOwn, User.Create, User.Update, User.Delete, User.Activate, User.Deactivate,
        Audit.Read, Audit.Export,
    ];
}
