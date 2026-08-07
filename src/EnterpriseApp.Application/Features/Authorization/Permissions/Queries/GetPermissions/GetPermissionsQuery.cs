using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Permissions.Queries.GetPermissions;

public sealed record GetPermissionsQuery(string? Module) : IRequest<Result<IReadOnlyList<PermissionDto>>>;
