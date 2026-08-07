using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Users.Queries.GetUserPermissions;

public sealed record GetUserPermissionsQuery(string UserId) : IRequest<Result<UserPermissionsDto>>;
