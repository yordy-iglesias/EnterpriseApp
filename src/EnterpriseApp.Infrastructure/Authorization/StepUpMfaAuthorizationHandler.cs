using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace EnterpriseApp.Infrastructure.Authorization;

/// <summary>
/// Enforces a recent MFA verification for sensitive actions. Reads the
/// <c>amr</c> array (RFC 8176) and the <c>auth_time</c> claim from the JWT.
/// </summary>
public sealed class StepUpMfaAuthorizationHandler : AuthorizationHandler<StepUpMfaRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StepUpMfaRequirement        requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        var amr = context.User.FindAll("amr").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!amr.Contains("mfa") && !amr.Contains("otp"))
            return Task.CompletedTask;

        // auth_time in seconds-since-epoch
        var authTimeClaim = context.User.FindFirstValue("auth_time");
        if (!long.TryParse(authTimeClaim, out var unix))
            return Task.CompletedTask;

        var authTime = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (DateTimeOffset.UtcNow - authTime <= requirement.MaxAge)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
