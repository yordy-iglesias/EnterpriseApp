using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;

internal sealed class LoginCommandHandler(
    IUnitOfWork           uow,
    IPasswordHasher       hasher,
    ITokenService         tokens,
    IPermissionService    permissions,
    IOptions<AuthOptions> opts)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var o = opts.Value;
        var normalized = cmd.Email.Trim().ToLowerInvariant();

        var user = await uow.Users.GetByNormalizedEmailAsync(normalized, ct);

        // Anti-enumeration: same error for not-found and bad password.
        if (user is null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);

        var verification = hasher.Verify(user.PasswordHash, cmd.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.RecordFailedLogin(o.MaxFailedAccessAttempts, TimeSpan.FromMinutes(o.LockoutMinutes));
            await uow.SaveChangesAsync(ct);
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);
        }

        // Rehash if the hasher algorithm was upgraded.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.ChangePassword(hasher.Hash(cmd.Password), "system");
        }

        // Check lockout and active status AFTER verifying the password.
        if (user.IsLockedOut(DateTimeOffset.UtcNow))
            return Result.Failure<AuthResponse>(AuthErrors.AccountLocked);

        if (!user.IsActive)
            return Result.Failure<AuthResponse>(AuthErrors.AccountInactive);

        user.RecordSuccessfulLogin();

        var userId   = user.Id.ToString();
        var tenantId = user.TenantId;
        var perms    = await permissions.GetPermissionsForUserAsync(userId, tenantId, ct);
        var roles    = await uow.Roles.GetRoleNamesForUserAsync(userId, tenantId, ct);
        var psv      = permissions.ComputeSnapshotVersion(perms);
        var authTime = DateTimeOffset.UtcNow;

        var subject = new TokenSubject(
            UserId:          userId,
            Email:           user.Email.Value,
            FullName:        user.FullName,
            TenantId:        tenantId,
            RoleNames:       roles,
            PermissionCodes: perms,
            SnapshotVersion: psv,
            AuthTime:        authTime);

        var access  = tokens.GenerateAccessToken(subject);
        var refresh = tokens.GenerateRefreshToken();

        user.IssueRefreshToken(refresh.Hash, refresh.ExpiresAt, cmd.IpAddress);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new AuthResponse(access.Token, access.ExpiresAt, refresh.Plain));
    }
}
