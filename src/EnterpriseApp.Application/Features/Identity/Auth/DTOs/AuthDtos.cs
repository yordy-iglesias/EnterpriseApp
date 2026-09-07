namespace EnterpriseApp.Application.Features.Identity.Auth.DTOs;

public sealed record AuthResponse(
    string         AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string         RefreshToken);

public sealed record CurrentUserDto(
    Guid              Id,
    string            Email,
    string            FullName,
    Guid?             TenantId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
