using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUsers;

public sealed record GetUsersQuery(int Page = 1, int PageSize = 20) : IRequest<Result<PagedUsersDto>>;
