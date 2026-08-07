namespace EnterpriseApp.Application.Common.Authorization;

/// <summary>
/// Policy-name constants shared between the API attributes and the Infrastructure
/// policy provider so the two sides cannot drift. No ASP.NET Core dependency here.
/// </summary>
public static class AuthorizationPolicyConstants
{
    public const string PermissionPolicyPrefix  = "perm:";
    public const string StepUpMfaPolicyName     = "step-up-mfa";
    public static readonly TimeSpan StepUpMfaMaxAge = TimeSpan.FromMinutes(5);
}
