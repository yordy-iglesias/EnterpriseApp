using EnterpriseApp.Application.Features.Identity.Auth.DTOs;
using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string  PlainToken,
    string? IpAddress) : IRequest<Result<AuthResponse>>;
