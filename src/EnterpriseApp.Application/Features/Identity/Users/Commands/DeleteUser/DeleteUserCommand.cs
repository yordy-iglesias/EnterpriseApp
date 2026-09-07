using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.DeleteUser;

public sealed record DeleteUserCommand(Guid Id) : IRequest<Result>;
