using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUsers;

internal sealed class GetUsersQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetUsersQuery, Result<PagedUsersDto>>
{
    public async Task<Result<PagedUsersDto>> Handle(GetUsersQuery q, CancellationToken ct)
    {
        var paged = await uow.Users.GetPagedAsync(q.Page, q.PageSize, ct);
        var data  = paged.Items.Select(u => new UserSummaryDto(
            u.Id.Value, u.Email.Value, u.FullName, u.IsActive, u.EmailConfirmed, u.CreatedAt)).ToList();

        return Result.Success(new PagedUsersDto(data, paged.TotalCount, q.Page, q.PageSize));
    }
}
