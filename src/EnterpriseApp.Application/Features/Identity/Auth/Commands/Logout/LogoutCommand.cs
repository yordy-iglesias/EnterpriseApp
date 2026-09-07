using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Logout;

public sealed record LogoutCommand(
    string  PlainToken,
    string? IpAddress) : IRequest<Result>;
