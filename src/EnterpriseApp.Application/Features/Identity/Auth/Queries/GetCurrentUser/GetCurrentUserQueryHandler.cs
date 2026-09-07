using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Queries.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(
    IUnitOfWork        uow,
    ICurrentUser       currentUser,
    IPermissionService permissions)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery _, CancellationToken ct)
    {
        if (!Guid.TryParse(currentUser.UserId, out var guid))
            return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        var user = await uow.Users.GetByIdAsync(UserId.From(guid), ct);
        if (user is null) return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        var userId   = user.Id.ToString();
        var tenantId = user.TenantId;
        var roles    = await uow.Roles.GetRoleNamesForUserAsync(userId, tenantId, ct);
        var perms    = await permissions.GetPermissionsForUserAsync(userId, tenantId, ct);

        return Result.Success(new CurrentUserDto(
            Id:          user.Id.Value,
            Email:       user.Email.Value,
            FullName:    user.FullName,
            TenantId:    tenantId,
            Roles:       roles.ToList(),
            Permissions: perms.ToList()));
    }
}
