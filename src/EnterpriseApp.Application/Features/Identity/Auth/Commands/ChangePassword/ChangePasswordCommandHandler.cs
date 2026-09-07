using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    IUnitOfWork    uow,
    IPasswordHasher hasher,
    ICurrentUser   currentUser)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        if (!Guid.TryParse(currentUser.UserId, out var guid))
            return Result.Failure(AuthErrors.UserNotFound);

        var user = await uow.Users.GetByIdAsync(UserId.From(guid), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);

        if (hasher.Verify(user.PasswordHash, cmd.CurrentPassword) == PasswordVerificationResult.Failed)
            return Result.Failure(AuthErrors.InvalidCredentials);

        user.ChangePassword(hasher.Hash(cmd.NewPassword), currentUser.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
