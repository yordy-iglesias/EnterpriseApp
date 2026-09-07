using EnterpriseApp.Application.Features.Identity.Users.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDetailDto>>;
