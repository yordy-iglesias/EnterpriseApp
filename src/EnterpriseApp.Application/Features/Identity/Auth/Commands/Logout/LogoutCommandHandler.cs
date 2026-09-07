using System.Security.Cryptography;
using System.Text;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Logout;

internal sealed class LogoutCommandHandler(IUnitOfWork uow)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand cmd, CancellationToken ct)
    {
        var hash = ComputeHash(cmd.PlainToken);
        var user = await uow.Users.GetByRefreshTokenHashAsync(hash, ct);
        if (user is null) return Result.Success(); // idempotent

        var token = user.GetActiveRefreshToken(hash);
        token?.Revoke("logout", cmd.IpAddress);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string ComputeHash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToBase64String(bytes);
    }
}
