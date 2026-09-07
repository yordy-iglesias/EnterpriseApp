using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IUnitOfWork        uow,
    ITokenService      tokens,
    IPermissionService permissions,
    IOptions<AuthOptions> opts)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var hash = ComputeHash(cmd.PlainToken);
        var user = await uow.Users.GetByRefreshTokenHashAsync(hash, ct);
        if (user is null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        var _ = opts.Value;
        var refresh = tokens.GenerateRefreshToken();

        var rotated = user.RotateRefreshToken(hash, refresh.Hash, refresh.ExpiresAt, cmd.IpAddress);
        if (!rotated)
        {
            await uow.SaveChangesAsync(ct);
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenReused);
        }

        var userId   = user.Id.ToString();
        var tenantId = user.TenantId;
        var perms    = await permissions.GetPermissionsForUserAsync(userId, tenantId, ct);
        var roles    = await uow.Roles.GetRoleNamesForUserAsync(userId, tenantId, ct);
        var psv      = permissions.ComputeSnapshotVersion(perms);

        var subject = new TokenSubject(
            UserId:          userId,
            Email:           user.Email.Value,
            FullName:        user.FullName,
            TenantId:        tenantId,
            RoleNames:       roles,
            PermissionCodes: perms,
            SnapshotVersion: psv,
            AuthTime:        DateTimeOffset.UtcNow);

        var access = tokens.GenerateAccessToken(subject);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new AuthResponse(access.Token, access.ExpiresAt, refresh.Plain));
    }

    private static string ComputeHash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToBase64String(bytes);
    }
}
