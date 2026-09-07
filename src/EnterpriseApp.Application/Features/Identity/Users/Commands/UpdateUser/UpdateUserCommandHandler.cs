using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(cmd.Id), ct);
        if (user is null) return Result.Failure(AuthErrors.UserNotFound);

        user.UpdateProfile(cmd.FirstName, cmd.LastName, currentUser.UserId);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
