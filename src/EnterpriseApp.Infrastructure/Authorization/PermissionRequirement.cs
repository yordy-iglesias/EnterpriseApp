using Microsoft.AspNetCore.Authorization;

namespace EnterpriseApp.Infrastructure.Authorization;

/// <summary>Authorization requirement carrying the permission codes to verify.</summary>
public sealed class PermissionRequirement(IReadOnlyCollection<string> permissions) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> Permissions { get; } = permissions;
}

/// <summary>Authorization requirement that demands a recent MFA step-up.</summary>
public sealed class StepUpMfaRequirement(TimeSpan maxAge) : IAuthorizationRequirement
{
    public TimeSpan MaxAge { get; } = maxAge;
}
