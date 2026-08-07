using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.DeleteRole;

public sealed class DeleteRoleCommandHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand cmd, CancellationToken ct)
    {
        var role = await uow.Roles.GetByIdAsync(RoleId.From(cmd.RoleId), ct);
        if (role is null)              return Result.Failure(AuthorizationErrors.RoleNotFound);
        if (role.IsSystem)             return Result.Failure(AuthorizationErrors.RoleIsSystem);
        if (role.TenantId != currentUser.TenantId && currentUser.TenantId is not null)
            return Result.Failure(AuthorizationErrors.TenantMismatch);

        role.Delete(currentUser.UserId);
        uow.Roles.Update(role);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
