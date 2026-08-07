using EnterpriseApp.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace EnterpriseApp.API.Filters;

/// <summary>
/// Declarative authorization attribute. Equivalent to
/// <c>[Authorize(Policy = "perm:{code}")]</c> but with first-class typing.
/// <para>Usage: <c>[RequirePermission(PermissionCodes.Patient.View)]</c>.</para>
/// <para>Multiple codes are AND-combined when supplied to a single attribute,
/// OR-combined when applied as multiple attributes — matching ASP.NET Core defaults.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Length == 0)
            throw new ArgumentException("At least one permission code is required.", nameof(permissions));

        Permissions = permissions;
        Policy      = $"{AuthorizationPolicyConstants.PermissionPolicyPrefix}{string.Join(',', permissions)}";
    }

    public string[] Permissions { get; }
}

/// <summary>
/// Optional companion attribute for permissions flagged as <c>IsSensitive</c>.
/// Adds a step-up MFA requirement on top of the permission check.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireStepUpMfaAttribute : AuthorizeAttribute
{
    public RequireStepUpMfaAttribute() => Policy = AuthorizationPolicyConstants.StepUpMfaPolicyName;
}
