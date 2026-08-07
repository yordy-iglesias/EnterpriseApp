using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Queries.GetRoleById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IRequest<Result<RoleDetailDto>>;
