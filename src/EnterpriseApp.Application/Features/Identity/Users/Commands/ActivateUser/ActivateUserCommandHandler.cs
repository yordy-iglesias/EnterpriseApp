using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.ActivateUser;

internal sealed class ActivateUserCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    : IRequestHandler<ActivateUserCommand, Result>
{
    public async Task<Result> Handle(ActivateUserCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(cmd.Id), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);
        user.Activate(cu.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
