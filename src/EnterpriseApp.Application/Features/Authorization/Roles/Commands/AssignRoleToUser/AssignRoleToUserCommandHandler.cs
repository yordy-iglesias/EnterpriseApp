using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.AssignRoleToUser;

public sealed class AssignRoleToUserCommandHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : IRequestHandler<AssignRoleToUserCommand, Result>
{
    public async Task<Result> Handle(AssignRoleToUserCommand cmd, CancellationToken ct)
    {
        var roleId = RoleId.From(cmd.RoleId);
        var role   = await uow.Roles.GetByIdAsync(roleId, ct);
        if (role is null)
            return Result.Failure(AuthorizationErrors.RoleNotFound);

        if (role.TenantId != currentUser.TenantId && currentUser.TenantId is not null)
            return Result.Failure(AuthorizationErrors.TenantMismatch);

        if (await uow.UserRoles.ExistsAsync(cmd.UserId, roleId, ct))
            return Result.Failure(AuthorizationErrors.UserAlreadyHasRole);

        var assignedBy = currentUser.UserId ?? "system";
        var userRole   = UserRole.Create(cmd.UserId, roleId, role.TenantId, assignedBy);

        await uow.UserRoles.AddAsync(userRole, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
