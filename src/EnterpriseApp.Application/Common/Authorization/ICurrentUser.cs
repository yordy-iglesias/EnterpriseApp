namespace EnterpriseApp.Application.Common.Authorization;

/// <summary>
/// Rich principal abstraction used by Application handlers and ABAC handlers.
/// Complements <see cref="Interfaces.ICurrentUserService"/> (which only carries identity)
/// with permissions, tenant, and authentication-method-reference (AMR) data.
/// <para>Implemented in the API layer over <c>HttpContext.User</c>.</para>
/// </summary>
public interface ICurrentUser
{
    string?               UserId    { get; }
    string?               UserName  { get; }
    string?               Email     { get; }
    Guid?                 TenantId  { get; }
    bool                  IsAuthenticated { get; }

    /// <summary>Resolved permission codes carried in the JWT (<c>perms</c> claim).</summary>
    IReadOnlySet<string>  Permissions { get; }

    /// <summary>Authentication methods reference — e.g. <c>["pwd","mfa"]</c>.</summary>
    IReadOnlySet<string>  AuthenticationMethods { get; }

    /// <summary>UTC time of the last MFA verification, if any.</summary>
    DateTimeOffset?       MfaVerifiedAt { get; }

    bool HasPermission(string code);
    bool HasAnyPermission(params string[] codes);
    bool HasAllPermissions(params string[] codes);
}
