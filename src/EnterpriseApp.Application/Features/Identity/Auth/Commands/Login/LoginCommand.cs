using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.Login;

public sealed record LoginCommand(
    string  Email,
    string  Password,
    string? IpAddress) : IRequest<Result<AuthResponse>>;
