using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.DeleteUser;

internal sealed class DeleteUserCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    : IRequestHandler<DeleteUserCommand, Result>
{
    public async Task<Result> Handle(DeleteUserCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(cmd.Id), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);
        user.Delete(cu.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
