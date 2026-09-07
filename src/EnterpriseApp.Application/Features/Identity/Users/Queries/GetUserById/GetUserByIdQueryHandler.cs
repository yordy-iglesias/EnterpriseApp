using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUserById;

internal sealed class GetUserByIdQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetUserByIdQuery, Result<UserDetailDto>>
{
    public async Task<Result<UserDetailDto>> Handle(GetUserByIdQuery q, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(UserId.From(q.Id), ct);
        if (user is null) return Result.Failure<UserDetailDto>(AuthErrors.UserNotFound);

        var roles = await uow.Roles.GetRoleNamesForUserAsync(user.Id.ToString(), user.TenantId, ct);

        return Result.Success(new UserDetailDto(
            Id:             user.Id.Value,
            Email:          user.Email.Value,
            FirstName:      user.FirstName,
            LastName:       user.LastName,
            FullName:       user.FullName,
            IsActive:       user.IsActive,
            EmailConfirmed: user.EmailConfirmed,
            TenantId:       user.TenantId,
            LastLoginAt:    user.LastLoginAt,
            CreatedAt:      user.CreatedAt,
            Roles:          roles.ToList()));
    }
}
